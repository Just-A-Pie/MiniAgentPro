using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SimulationMapRenderer : MonoBehaviour
{
    [Header("资源根目录 (留空 = GameManager.resourcePath)")]
    public string mapFolder;

    [Header("UI 引用")]
    public RectTransform mapContent;
    public PopupManager popupManager;

    [Header("交互控制器")]
    public SimulationUIController uiController;

    [System.Serializable]
    private class MazeMetaInfo
    {
        public int typeId;
        public int maze_width;
        public int maze_height;
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(mapFolder))
            mapFolder = GameManager.Instance != null
                        ? GameManager.Instance.resourcePath
                        : "";

        Debug.Log($"[SIMBOOT:S3] SimRenderer Start(), mapFolder={mapFolder}");

        // 等待地图背景加载完毕
        yield return new WaitUntil(() =>
            SimulationMapManager.Instance != null &&
            SimulationMapManager.Instance.mapImage != null
        );
        Debug.Log("[SIMBOOT:S3] Map background ready, begin templating/placement");

        List<EditorItem> templates = BuildTemplateList();
        Debug.Log($"[SIMBOOT:S3] Templates built: {templates.Count}");

        string dataRoot = Path.Combine(mapFolder, "map", "map_data");
        var bData = LoadJson<MapManager.BuildingsData>(Path.Combine(dataRoot, "buildings_data.json"));
        var oData = LoadJson<MapManager.ObjectsData>(Path.Combine(dataRoot, "objects_data.json"));
        Debug.Log($"[SIMBOOT:S3] map_data loaded: buildings={bData.buildings.Count}, objects={oData.objects.Count}");

        int okB = 0, okO = 0;
        foreach (var b in bData.buildings)
            if (Place(templates, EditorItemCategory.Building, b.typeId, b.x, b.y, b.uniqueId, b.itemName, ConvertAttr(b.attributes))) okB++;
        foreach (var o in oData.objects)
            if (Place(templates, EditorItemCategory.Object, o.typeId, o.x, o.y, o.uniqueId, o.itemName, ConvertAttr(o.attributes))) okO++;

        Debug.Log($"[SIMBOOT:S3] Placement finished. Building {okB}/{bData.buildings.Count}, Object {okO}/{oData.objects.Count}");
    }

    private List<EditorItem> BuildTemplateList()
    {
        var list = new List<EditorItem>();
        Scan(Path.Combine(mapFolder, "buildings"), EditorItemCategory.Building, list);
        Scan(Path.Combine(mapFolder, "objects"), EditorItemCategory.Object, list);
        return list;
    }

    private void Scan(string root, EditorItemCategory cat, List<EditorItem> list)
    {
        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"[SimRenderer] 目录不存在 {root}");
            return;
        }
        foreach (string folder in Directory.GetDirectories(root))
        {
            string metaPath = Path.Combine(folder, "maze_meta_info.json");
            if (!File.Exists(metaPath)) continue;

            var meta = JsonUtility.FromJson<MazeMetaInfo>(File.ReadAllText(metaPath));
            var item = new EditorItem
            {
                uniqueId = "",
                typeId = meta.typeId,
                itemName = Path.GetFileName(folder),
                gridWidth = meta.maze_width,
                gridHeight = meta.maze_height,
                category = cat,
                thumbnail = LoadSprite(Path.Combine(folder, "texture.png")),
                attributes = new Dictionary<string, string>()
            };
            list.Add(item);
            Debug.Log($"[SimRenderer] 模板 {cat} {item.itemName} typeId={item.typeId}");
        }
    }

    private Sprite LoadSprite(string path)
    {
        if (!File.Exists(path)) return null;
        var data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2);
        return tex.LoadImage(data)
             ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f))
             : null;
    }

    private bool Place(List<EditorItem> templates,
                       EditorItemCategory cat, int typeId,
                       int gx, int gy, string uid, string name,
                       Dictionary<string, string> attr)
    {
        if (gx == -1 && gy == -1) return false;

        var tpl = templates.Find(t => t.category == cat && t.typeId == typeId);
        if (tpl == null)
        {
            Debug.LogError($"[SimRenderer] 缺少模板 cat={cat} typeId={typeId}");
            return false;
        }

        Debug.Log($"[SimRenderer] Place {tpl.itemName} ({cat}) id={uid} grid=({gx},{gy})");

        // 1) 创建实例
        ItemCreator.CreateItemInstanceWithClick(
            tpl, gx, gy, cat,
            mapContent, popupManager,
            uid, string.IsNullOrEmpty(name) ? tpl.itemName : name,
            attr
        );

        // 2) 找到它的 GameObject
        Transform inst = mapContent.Find(uid);
        if (inst == null)
        {
            Debug.LogWarning($"[SimRenderer] 找不到实例 go name={uid}");
            return true;
        }
        var go = inst.gameObject;

        // 3) 构造一个放置物数据对象
        var placedItem = new MapManager.PlacedItem
        {
            uniqueId = uid,
            item = new EditorItem
            {
                uniqueId = uid,
                typeId = tpl.typeId,
                itemName = string.IsNullOrEmpty(name) ? tpl.itemName : name,
                gridWidth = tpl.gridWidth,
                gridHeight = tpl.gridHeight,
                category = tpl.category,
                thumbnail = tpl.thumbnail,
                attributes = attr
            },
            category = tpl.category,
            typeId = tpl.typeId,
            gridX = gx,
            gridY = gy,
            gridWidth = tpl.gridWidth,
            gridHeight = tpl.gridHeight
        };

        // 4) Outline —— 统一为与 Agent 一致的样式：黑色 + 细描边
        var outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 1f);
        outline.effectDistance = new Vector2(0.2f, 0.2f);
        outline.enabled = false;

        // 5) 注册到 UIController
        if (uiController != null)
        {
            uiController.RegisterItem(uid, outline, placedItem);
        }
        else
        {
            Debug.LogError($"[SimRenderer] uiController 未设置，无法注册 id={uid}");
        }

        // 6) 添加交互回调（保持原逻辑）
        var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var ent = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        ent.callback.AddListener((_) =>
        {
            Debug.Log($"[SimRenderer] PointerEnter -> {uid}");
            uiController.OnItemPointerEnter(uid);
        });
        trigger.triggers.Add(ent);

        var ext = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        ext.callback.AddListener((_) =>
        {
            Debug.Log($"[SimRenderer] PointerExit -> {uid}");
            uiController.OnItemPointerExit(uid);
        });
        trigger.triggers.Add(ext);

        // —— 这里改：仅左键点击才触发
        var clk = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clk.callback.AddListener((dataObj) =>
        {
            var ped = dataObj as PointerEventData;
            if (ped == null || ped.button != PointerEventData.InputButton.Left) return;

            Debug.Log($"[SimRenderer] PointerClick -> {uid}");
            uiController.OnItemPointerClick(uid);
        });
        trigger.triggers.Add(clk);

        return true;
    }

    private T LoadJson<T>(string path) where T : new()
    {
        return File.Exists(path)
            ? JsonUtility.FromJson<T>(File.ReadAllText(path))
            : new T();
    }

    private Dictionary<string, string> ConvertAttr(List<MapManager.AttributeEntry> list)
    {
        var d = new Dictionary<string, string>();
        if (list == null) return d;
        foreach (var e in list)
            if (!d.ContainsKey(e.key))
                d[e.key] = e.value;
        return d;
    }
}
