using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    public PopupManager popupManager;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    [Header("地图显示区域")]
    public RectTransform mapDisplayArea; // MapDisplayPanel

    [Header("地图内容容器")]
    public RectTransform mapContent;     // 包含背景、建筑、对象等

    [Header("基础资源文件夹")]
    public string mapFolder; // 如果为空，则从 GameManager.Instance.resourcePath 获取

    [Header("地图贴图文件名")]
    public string mapTextureFileName = "texture.png";

    // 全局地图元数据（存放于 {mapFolder}/map/maze_meta_info.json）
    [Serializable]
    public class MapMetaInfo
    {
        public string world_name;
        public int maze_width;
        public int maze_height;
        public float sq_tile_size;
        public string special_constraint;
    }
    public MapMetaInfo mapMeta;

    // 每个 Building/Object 的资源数据包含在各自文件夹下的 maze_meta_info.json，
    // 用于构建 typeId 到文件夹名称的映射
    [Serializable]
    public class MazeMetaInfo
    {
        public int typeId;
        public string world_name;
        public int maze_width;
        public int maze_height;
        public float sq_tile_size;
        public string special_constraint;
    }

    // 地图背景
    public Image mapImage;

    [Header("工具栏数据引用")]
    public ToolPanelManager toolPanelManager;

    [Header("基本参数")]
    public float gridSize = 32f;             // 每个格子32×32
    public float backgroundScaleFactor = 1f; // 地图背景缩放因子

    // ---------------- 新增：移动模式相关字段 ----------------
    [Header("移动模式相关")]
    public bool isMoveMode = false;
    public PlacedItem movingItem;

    // ============== 原有数据（运行时使用，旧版） ==============
    // 旧版 Maze 数组：用于运行时放置、重置、判断（旧版逻辑）
    private int[,] sectorMaze;         // 对应旧版 sector_maze.csv
    private int[,] objectMaze;         // 对应旧版 game_object_maze.csv

    [Header("CSV 文件名(在 map/maze 目录下) - 旧版")]
    public string sectorMazeFile = "sector_maze.csv";
    public string objectMazeFile = "game_object_maze.csv";

    // ============== 新增数据（用于保存时更新全局 Map 数据） ==============
    // 新版 Map 的 Maze 数据（四层）
    public int[,] mapSectorMaze;
    public int[,] mapArenaMaze;
    public int[,] mapGameobjectMaze;
    public int[,] mapCollisionMaze;
    // 新版全局 Maze 文件名（保存时使用，存放于 {mapFolder}/map/maze/ 下）
    public string arenaMazeFile = "arena_maze.csv";
    public string gameObjectMazeFileNew = "game_object_maze.csv";
    public string collisionMazeFile = "collision_maze.csv";

    // 新版全局 Block 映射文件（存放于 {mapFolder}/map/special_blocks/ 下）
    public string sectorBlockFile = "sector_blocks.csv";
    public string arenaBlockFile = "arena_blocks.csv";
    public string gameObjectBlockFile = "game_object_blocks.csv";

    private Dictionary<string, int> mapSectorBlockMapping;
    private Dictionary<string, int> mapArenaBlockMapping;
    private Dictionary<string, int> mapGameobjectBlockMapping;

    // ============== 新增：基于 typeId 构建资源文件夹名称的映射字典 ==============
    // 用于查找实际资源文件夹名称，不直接使用 item.itemName，而是基于各资源下的 maze_meta_info.json
    private Dictionary<int, string> buildingFolderMapping = new Dictionary<int, string>();
    private Dictionary<int, string> objectFolderMapping = new Dictionary<int, string>();

    // 是否有修改 => 用于控制重置按钮
    public bool isDirty = false;

    // 已放置条目信息，用于 JSON 的 buildings_data / objects_data
    [Serializable]
    public struct PlacedItem
    {
        public string uniqueId;  // 每个物品唯一ID
        public EditorItem item;
        public EditorItemCategory category;
        public int typeId;
        public int gridX;
        public int gridY;
        public int gridWidth;
        public int gridHeight;
    }
    public List<PlacedItem> placedItems = new List<PlacedItem>();

    // 可序列化属性项：用于转换 Dictionary（JsonUtility 不支持直接序列化 Dictionary）
    [Serializable]
    public class AttributeEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class BuildingEntry
    {
        public string uniqueId;
        public int typeId;
        public int x;
        public int y;
        public string itemName;
        public List<AttributeEntry> attributes;
    }
    [Serializable]
    public class BuildingsData
    {
        public List<BuildingEntry> buildings = new List<BuildingEntry>();
    }
    [Serializable]
    public class ObjectEntry
    {
        public string uniqueId;
        public int typeId;
        public int x;
        public int y;
        public string itemName;
        public List<AttributeEntry> attributes;
    }
    [Serializable]
    public class ObjectsData
    {
        public List<ObjectEntry> objects = new List<ObjectEntry>();
    }

    private BuildingsData initialBuildingsData;
    private ObjectsData initialObjectsData;

    void Start()
    {
        if (string.IsNullOrEmpty(mapFolder))
        {
            if (GameManager.Instance != null)
                mapFolder = GameManager.Instance.resourcePath;
            else
                Debug.LogError("未设置 mapFolder 且未找到 GameManager");
        }

        // 构建资源文件夹映射（基于各资源下的 maze_meta_info.json）
        BuildResourceFolderMappings();

        string actualMapFolder = Path.Combine(mapFolder, "map");
        Debug.Log("实际地图数据所在文件夹：" + actualMapFolder);

        // 读取全局地图元数据
        string metaPath = Path.Combine(actualMapFolder, "maze_meta_info.json");
        if (File.Exists(metaPath))
        {
            string jsonText = File.ReadAllText(metaPath);
            mapMeta = JsonUtility.FromJson<MapMetaInfo>(jsonText);
            Debug.Log("加载地图元数据：" + mapMeta.world_name);
        }
        else
        {
            Debug.LogWarning("未找到地图元数据：" + metaPath);
        }

        // 加载背景贴图
        string texturePath = Path.Combine(actualMapFolder, mapTextureFileName);
        if (!File.Exists(texturePath))
        {
            Debug.LogWarning("未找到地图贴图：" + texturePath);
            return;
        }
        byte[] imgData = File.ReadAllBytes(texturePath);
        Texture2D tex = new Texture2D(2, 2);
        if (tex.LoadImage(imgData))
        {
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            if (mapImage == null)
            {
                GameObject go = new GameObject("MapImage", typeof(Image));
                go.transform.SetParent(mapContent, false);
                mapImage = go.GetComponent<Image>();
            }
            mapImage.sprite = sp;

            float panelW = mapDisplayArea.rect.width;
            float panelH = mapDisplayArea.rect.height;
            backgroundScaleFactor = Mathf.Min(panelW / tex.width, panelH / tex.height, 1f);
            Vector2 newSize = new Vector2(tex.width * backgroundScaleFactor, tex.height * backgroundScaleFactor);

            // 设置大小和位置
            RectTransform imgRT = mapImage.rectTransform;
            imgRT.sizeDelta = newSize;
            imgRT.anchoredPosition = Vector2.zero;
            mapImage.transform.SetSiblingIndex(0);

            // —— 新增：统一为左上角坐标系 —— 
            imgRT.anchorMin = new Vector2(0, 1);
            imgRT.anchorMax = new Vector2(0, 1);
            imgRT.pivot = new Vector2(0, 1);
            imgRT.anchoredPosition = Vector2.zero;
            // —— 结束 —— 

            Debug.Log($"成功加载地图贴图，原始尺寸：{tex.width}x{tex.height}，缩放后：{newSize.x}x{newSize.y}");
        }



        // ================= 原有部分：初始化旧版 Maze 数组 =================
        sectorMaze = new int[mapMeta.maze_height, mapMeta.maze_width];
        objectMaze = new int[mapMeta.maze_height, mapMeta.maze_width];

        string mazeDir = Path.Combine(actualMapFolder, "maze");
        LoadMazeCsv(Path.Combine(mazeDir, sectorMazeFile), sectorMaze);
        LoadMazeCsv(Path.Combine(mazeDir, objectMazeFile), objectMaze);

        // ================= 新增部分：初始化新版 Map Maze 数组 =================
        mapSectorMaze = new int[mapMeta.maze_height, mapMeta.maze_width];
        mapArenaMaze = new int[mapMeta.maze_height, mapMeta.maze_width];
        mapGameobjectMaze = new int[mapMeta.maze_height, mapMeta.maze_width];
        mapCollisionMaze = new int[mapMeta.maze_height, mapMeta.maze_width];
        for (int r = 0; r < mapMeta.maze_height; r++)
        {
            for (int c = 0; c < mapMeta.maze_width; c++)
            {
                mapSectorMaze[r, c] = 0;
                mapArenaMaze[r, c] = 0;
                mapGameobjectMaze[r, c] = 0;
                mapCollisionMaze[r, c] = 0;
            }
        }

        // 加载初始建筑数据
        string buildingDataPath = Path.Combine(actualMapFolder, "map_data", "buildings_data.json");
        initialBuildingsData = new BuildingsData();
        if (File.Exists(buildingDataPath))
        {
            string bjson = File.ReadAllText(buildingDataPath);
            initialBuildingsData = JsonUtility.FromJson<BuildingsData>(bjson);
        }
        else
        {
            Debug.LogWarning("未找到建筑数据文件：" + buildingDataPath);
        }

        // 加载初始对象数据
        string objectDataPath = Path.Combine(actualMapFolder, "map_data", "objects_data.json");
        initialObjectsData = new ObjectsData();
        if (File.Exists(objectDataPath))
        {
            string ojson = File.ReadAllText(objectDataPath);
            initialObjectsData = JsonUtility.FromJson<ObjectsData>(ojson);
        }
        else
        {
            Debug.LogWarning("未找到对象数据文件：" + objectDataPath);
        }

        // 初始化地图（加载建筑、对象到场景中）
        ReloadMapData();
        // —— 在这里插入测试代码 —— 
        // 坐标 (3,3)，1×1 格子大小
        float cellSize = gridSize * backgroundScaleFactor;

        // 新建一个 UI 方块
        GameObject testMarker = new GameObject("TestMarker", typeof(Image));
        testMarker.transform.SetParent(mapContent, false);

        // 纯色贴图
        var img = testMarker.GetComponent<Image>();
        img.color = Color.red;  // 或者 Color.green、Color.blue

        // 定位到左上角第 (3,3) 格
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(cellSize, cellSize);
        rt.anchoredPosition = new Vector2(3 * cellSize, -3 * cellSize);
        // —— 测试代码结束 —— 
        isDirty = false;
    }

    // ---------------- 新增：构建建筑和对象的资源文件夹映射字典 ----------------
    private void BuildResourceFolderMappings()
    {
        // 建筑映射：扫描 {mapFolder}/buildings 下每个子文件夹的 maze_meta_info.json
        string buildingsRoot = Path.Combine(mapFolder, "buildings");
        if (Directory.Exists(buildingsRoot))
        {
            foreach (string dir in Directory.GetDirectories(buildingsRoot))
            {
                string metaFile = Path.Combine(dir, "maze_meta_info.json");
                if (File.Exists(metaFile))
                {
                    string metaJson = File.ReadAllText(metaFile);
                    MazeMetaInfo meta = JsonUtility.FromJson<MazeMetaInfo>(metaJson);
                    if (!buildingFolderMapping.ContainsKey(meta.typeId))
                    {
                        buildingFolderMapping.Add(meta.typeId, Path.GetFileName(dir));
                        Debug.Log($"Building mapping: typeId {meta.typeId} -> {Path.GetFileName(dir)}");
                    }
                }
            }
        }
        // 对象映射：扫描 {mapFolder}/objects 下每个子文件夹的 maze_meta_info.json
        string objectsRoot = Path.Combine(mapFolder, "objects");
        if (Directory.Exists(objectsRoot))
        {
            foreach (string dir in Directory.GetDirectories(objectsRoot))
            {
                string metaFile = Path.Combine(dir, "maze_meta_info.json");
                if (File.Exists(metaFile))
                {
                    string metaJson = File.ReadAllText(metaFile);
                    MazeMetaInfo meta = JsonUtility.FromJson<MazeMetaInfo>(metaJson);
                    if (!objectFolderMapping.ContainsKey(meta.typeId))
                    {
                        objectFolderMapping.Add(meta.typeId, Path.GetFileName(dir));
                        Debug.Log($"Object mapping: typeId {meta.typeId} -> {Path.GetFileName(dir)}");
                    }
                }
            }
        }
    }

    // ---------------- 修改：GetFolderName 方法基于 typeId 构建文件夹名称 ----------------
    private string GetFolderName(EditorItem item)
    {
        if (item.category == EditorItemCategory.Building)
        {
            if (buildingFolderMapping.ContainsKey(item.typeId))
                return buildingFolderMapping[item.typeId];
            else
                return item.itemName;
        }
        else if (item.category == EditorItemCategory.Object)
        {
            if (objectFolderMapping.ContainsKey(item.typeId))
                return objectFolderMapping[item.typeId];
            else
                return item.itemName;
        }
        return item.itemName;
    }

    // ---------------- 原有代码保持不变 ----------------

    public void RemoveItem(string uniqueId)
    {
        PlacedItem? itemToRemove = null;
        foreach (var item in placedItems)
        {
            if (item.uniqueId == uniqueId)
            {
                itemToRemove = item;
                break;
            }
        }

        if (!itemToRemove.HasValue)
        {
            Debug.LogWarning($"未找到要删除的物品: {uniqueId}");
            return;
        }

        // 递归删除其子物品（如果它是 container）
        if (itemToRemove.Value.item.attributes != null && itemToRemove.Value.item.attributes.ContainsKey("container"))
        {
            string containerStr = itemToRemove.Value.item.attributes["container"];
            if (!string.IsNullOrEmpty(containerStr))
            {
                string[] childIds = containerStr.Split(',');
                foreach (string childId in childIds)
                {
                    RemoveItem(childId);
                }
            }
        }

        // 在场景中销毁对应的 UI GameObject
        foreach (Transform child in mapContent)
        {
            if (child.gameObject.name == uniqueId)
            {
                Destroy(child.gameObject);
                break;
            }
        }

        // 从列表中移除
        placedItems.Remove(itemToRemove.Value);

        // 标记脏并在 Overlay 模式下刷新
        isDirty = true;
        if (GridOverlayManager.Instance != null &&
            GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
        {
            GridOverlayManager.Instance.RefreshOverlay();
        }

        Debug.Log($"删除物品成功: {uniqueId}");
    }


    private void LoadMazeCsv(string path, int[,] mazeData)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("Maze CSV不存在: " + path);
            for (int r = 0; r < mapMeta.maze_height; r++)
                for (int c = 0; c < mapMeta.maze_width; c++)
                    mazeData[r, c] = -1;
            return;
        }
        string[] lines = File.ReadAllLines(path);
        for (int r = 0; r < mapMeta.maze_height && r < lines.Length; r++)
        {
            string[] cols = lines[r].Split(',');
            for (int c = 0; c < mapMeta.maze_width && c < cols.Length; c++)
            {
                int val;
                if (int.TryParse(cols[c], out val))
                    mazeData[r, c] = val;
                else
                    mazeData[r, c] = -1;
            }
        }
    }

    public void ReloadMapData()
    {
        placedItems.Clear();

        // 修改点：不销毁 "MapImage" 和 "LogoContainer"（专用放置 logo 的容器）
        for (int i = mapContent.childCount - 1; i >= 0; i--)
        {
            Transform child = mapContent.GetChild(i);
            if (child.gameObject.name == "MapImage" || child.gameObject.name == "LogoContainer")
                continue;
            Destroy(child.gameObject);
        }

        if (initialBuildingsData == null)
        {
            Debug.LogWarning("initialBuildingsData 为 null！");
        }
        else if (initialBuildingsData.buildings == null)
        {
            Debug.LogWarning("initialBuildingsData.buildings 为 null！");
        }
        else
        {
            Debug.Log("建筑数量（JSON）： " + initialBuildingsData.buildings.Count);
            foreach (var entry in initialBuildingsData.buildings)
            {
                EditorItem buildingType = toolPanelManager.availableItems.Find(item =>
                    item.category == EditorItemCategory.Building && item.typeId == entry.typeId);
                if (buildingType != null)
                {
                    string itemName = string.IsNullOrEmpty(entry.itemName) ? buildingType.itemName : entry.itemName;
                    Dictionary<string, string> attrs = ConvertAttributeListToDictionary(entry.attributes);
                    ItemCreator.CreateItemInstanceWithClick(buildingType, entry.x, entry.y, EditorItemCategory.Building, mapContent, popupManager, entry.uniqueId, itemName, attrs);
                }
                else
                {
                    Debug.LogWarning($"未找到类型为 Building 且 typeId={entry.typeId} 的物品");
                }
            }
        }

        if (initialObjectsData == null)
        {
            Debug.LogWarning("initialObjectsData 为 null！");
        }
        else if (initialObjectsData.objects == null)
        {
            Debug.LogWarning("initialObjectsData.objects 为 null！");
        }
        else
        {
            Debug.Log("对象数量（JSON）： " + initialObjectsData.objects.Count);
            foreach (var entry in initialObjectsData.objects)
            {
                if (entry.x == -1 && entry.y == -1)
                {
                    EditorItem objectType = toolPanelManager.availableItems.Find(item =>
                        item.category == EditorItemCategory.Object && item.typeId == entry.typeId);
                    if (objectType != null)
                    {
                        string itemName = string.IsNullOrEmpty(entry.itemName) ? objectType.itemName : entry.itemName;
                        Dictionary<string, string> attrs = ConvertAttributeListToDictionary(entry.attributes);
                        PlacedItem childPlacedItem = new PlacedItem
                        {
                            uniqueId = entry.uniqueId,
                            item = new EditorItem
                            {
                                uniqueId = entry.uniqueId,
                                typeId = objectType.typeId,
                                itemName = itemName,
                                gridWidth = objectType.gridWidth,
                                gridHeight = objectType.gridHeight,
                                category = objectType.category,
                                thumbnail = objectType.thumbnail,
                                attributes = attrs
                            },
                            category = EditorItemCategory.Object,
                            typeId = objectType.typeId,
                            gridX = entry.x,
                            gridY = entry.y,
                            gridWidth = objectType.gridWidth,
                            gridHeight = objectType.gridHeight
                        };
                        placedItems.Add(childPlacedItem);
                    }
                }
                else
                {
                    EditorItem objectType = toolPanelManager.availableItems.Find(item =>
                        item.category == EditorItemCategory.Object && item.typeId == entry.typeId);
                    if (objectType != null)
                    {
                        string itemName = string.IsNullOrEmpty(entry.itemName) ? objectType.itemName : entry.itemName;
                        Dictionary<string, string> attrs = ConvertAttributeListToDictionary(entry.attributes);
                        ItemCreator.CreateItemInstanceWithClick(objectType, entry.x, entry.y, EditorItemCategory.Object, mapContent, popupManager, entry.uniqueId, itemName, attrs);
                    }
                }
            }
        }
        // 确保 LogoContainer 始终在最上层
        Transform logoContainer = mapContent.Find("LogoContainer");
        if (logoContainer != null)
            logoContainer.SetAsLastSibling();

        Debug.Log("地图已重置到初始状态。");
        isDirty = false;
    }


    public bool CanPlaceItem(EditorItem item, int gridX, int gridY)
    {
        int[,] targetMaze = (item.category == EditorItemCategory.Building) ? sectorMaze : objectMaze;
        for (int r = 0; r < item.gridHeight; r++)
        {
            for (int c = 0; c < item.gridWidth; c++)
            {
                int mr = gridY + r;
                int mc = gridX + c;
                if (mr < 0 || mr >= mapMeta.maze_height || mc < 0 || mc >= mapMeta.maze_width)
                    continue;
                if (targetMaze[mr, mc] != -1)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void AddPlacedItem(EditorItem item, int gridX, int gridY)
    {
        int[,] targetMaze = (item.category == EditorItemCategory.Building) ? sectorMaze : objectMaze;
        for (int r = 0; r < item.gridHeight; r++)
        {
            for (int c = 0; c < item.gridWidth; c++)
            {
                int mr = gridY + r;
                int mc = gridX + c;
                if (mr < 0 || mr >= mapMeta.maze_height || mc < 0 || mc >= mapMeta.maze_width)
                    continue;
                if (targetMaze[mr, mc] == -1)
                    targetMaze[mr, mc] = item.typeId;
            }
        }

        string uniqueId = System.Guid.NewGuid().ToString();
        PlacedItem pi = new PlacedItem
        {
            uniqueId = uniqueId,
            category = item.category,
            typeId = item.typeId,
            gridX = gridX,
            gridY = gridY,
            gridWidth = item.gridWidth,
            gridHeight = item.gridHeight,
            item = item
        };
        placedItems.Add(pi);
        Debug.Log("Placed items count: " + placedItems.Count);
        isDirty = true;
    }

    private void SaveMazeCsv(string path, int[,] mazeData)
    {
        string[] lines = new string[mapMeta.maze_height];
        for (int r = 0; r < mapMeta.maze_height; r++)
        {
            string[] cols = new string[mapMeta.maze_width];
            for (int c = 0; c < mapMeta.maze_width; c++)
            {
                cols[c] = mazeData[r, c].ToString();
            }
            lines[r] = string.Join(",", cols);
        }
        File.WriteAllLines(path, lines);
    }

    // 新的保存方法：重构新版 Map 数据后保存到 CSV 文件中（仅保存新版数据，不影响运行时旧版功能）
    public void SaveAllCsv()
    {
        Debug.Log("开始保存新版 Map CSV 数据...");
        // 重构新版 Map 数据
        RebuildMapMazeData();

        string mazeDir = Path.Combine(mapFolder, "map", "maze");
        if (!Directory.Exists(mazeDir))
            Directory.CreateDirectory(mazeDir);

        SaveMazeCsv(Path.Combine(mazeDir, sectorMazeFile), mapSectorMaze);
        SaveMazeCsv(Path.Combine(mazeDir, arenaMazeFile), mapArenaMaze);
        SaveMazeCsv(Path.Combine(mazeDir, gameObjectMazeFileNew), mapGameobjectMaze);
        SaveMazeCsv(Path.Combine(mazeDir, collisionMazeFile), mapCollisionMaze);

        string blockDir = Path.Combine(mapFolder, "map", "special_blocks");
        if (!Directory.Exists(blockDir))
            Directory.CreateDirectory(blockDir);

        SaveBlockCsv(Path.Combine(blockDir, sectorBlockFile), mapSectorBlockMapping);
        SaveBlockCsv(Path.Combine(blockDir, arenaBlockFile), mapArenaBlockMapping);
        SaveBlockCsv(Path.Combine(blockDir, gameObjectBlockFile), mapGameobjectBlockMapping);

        isDirty = false;
        Debug.Log("新版 Maze CSV 和 Block CSV 已保存，resetButton 可禁用");
    }

    public void ResetAllCsv()
    {
        string mazeDir = Path.Combine(mapFolder, "map", "maze");
        LoadMazeCsv(Path.Combine(mazeDir, sectorMazeFile), sectorMaze);
        LoadMazeCsv(Path.Combine(mazeDir, objectMazeFile), sectorMaze);

        string buildingDataPath = Path.Combine(mapFolder, "map", "map_data", "buildings_data.json");
        if (File.Exists(buildingDataPath))
        {
            string bjson = File.ReadAllText(buildingDataPath);
            initialBuildingsData = JsonUtility.FromJson<BuildingsData>(bjson);
        }

        string objectDataPath = Path.Combine(mapFolder, "map", "map_data", "objects_data.json");
        if (File.Exists(objectDataPath))
        {
            string ojson = File.ReadAllText(objectDataPath);
            initialObjectsData = JsonUtility.FromJson<ObjectsData>(ojson);
        }

        ReloadMapData();
        isDirty = false;
        Debug.Log("已重置 Maze CSV 和物品数据");
    }

    public void SaveMapData()
    {
        BuildingsData bData = new BuildingsData();
        ObjectsData oData = new ObjectsData();

        foreach (var pi in placedItems)
        {
            if (pi.category == EditorItemCategory.Building)
            {
                BuildingEntry be = new BuildingEntry
                {
                    uniqueId = pi.uniqueId,
                    typeId = pi.typeId,
                    x = pi.gridX,
                    y = pi.gridY,
                    itemName = pi.item.itemName,
                    attributes = ConvertDictionaryToList(pi.item.attributes)
                };
                bData.buildings.Add(be);
            }
            else if (pi.category == EditorItemCategory.Object)
            {
                ObjectEntry oe = new ObjectEntry
                {
                    uniqueId = pi.uniqueId,
                    typeId = pi.typeId,
                    x = pi.gridX,
                    y = pi.gridY,
                    itemName = pi.item.itemName,
                    attributes = ConvertDictionaryToList(pi.item.attributes)
                };
                oData.objects.Add(oe);
            }
        }

        string actualMapFolder = Path.Combine(mapFolder, "map");
        string buildingDataPath = Path.Combine(actualMapFolder, "map_data", "buildings_data.json");
        string objectDataPath = Path.Combine(actualMapFolder, "map_data", "objects_data.json");

        string buildingJson = JsonUtility.ToJson(bData, true);
        string objectJson = JsonUtility.ToJson(oData, true);
        File.WriteAllText(buildingDataPath, buildingJson);
        File.WriteAllText(objectDataPath, objectJson);

        isDirty = false;
        Debug.Log("地图 JSON 已保存");
    }

    private List<AttributeEntry> ConvertDictionaryToList(Dictionary<string, string> dict)
    {
        List<AttributeEntry> list = new List<AttributeEntry>();
        if (dict == null)
            return list;
        foreach (var kvp in dict)
        {
            AttributeEntry entry = new AttributeEntry { key = kvp.Key, value = kvp.Value };
            list.Add(entry);
        }
        return list;
    }

    private Dictionary<string, string> ConvertAttributeListToDictionary(List<AttributeEntry> list)
    {
        Dictionary<string, string> dict = new Dictionary<string, string>();
        if (list == null)
            return dict;
        foreach (var entry in list)
        {
            if (!dict.ContainsKey(entry.key))
                dict.Add(entry.key, entry.value);
        }
        return dict;
    }

    // ---------------- 新增：保存 Block CSV ----------------
    private void SaveBlockCsv(string path, Dictionary<string, int> mapping)
    {
        List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(mapping);
        list.Sort((a, b) => a.Value.CompareTo(b.Value));
        List<string> lines = new List<string>();
        foreach (var kvp in list)
        {
            lines.Add(kvp.Value + "," + kvp.Key);
        }
        File.WriteAllLines(path, lines);
    }

    // ---------------- 新增：重新构建新版 Map 的 Maze 数据及 Block 映射 ----------------
    public void RebuildMapMazeData()
    {
        Debug.Log("开始重构新版全局 Map Maze 数据...");
        int height = mapMeta.maze_height;
        int width = mapMeta.maze_width;

        // 初始化新版各层 Maze 数组为0（0表示空）
        mapSectorMaze = new int[height, width];
        mapArenaMaze = new int[height, width];
        mapGameobjectMaze = new int[height, width];
        mapCollisionMaze = new int[height, width];
        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                mapSectorMaze[r, c] = 0;
                mapArenaMaze[r, c] = 0;
                mapGameobjectMaze[r, c] = 0;
                mapCollisionMaze[r, c] = 0;
            }
        }
        Debug.Log("新版 Maze 数组初始化完成");

        // 初始化新版 Block 映射字典（仅 sector、arena、game_object 层）
        mapSectorBlockMapping = new Dictionary<string, int>();
        mapArenaBlockMapping = new Dictionary<string, int>();
        mapGameobjectBlockMapping = new Dictionary<string, int>();

        // 遍历所有已放置物品，更新新版各层 Maze 数据
        foreach (var placed in placedItems)
        {
            int baseX = placed.gridX;
            int baseY = placed.gridY;
            // 如果放置物品的 gridX 或 gridY 为负，则跳过更新全局数据
            if (baseX < 0 || baseY < 0)
            {
                Debug.LogWarning($"Skipping placed item {placed.uniqueId} due to negative grid position: ({baseX},{baseY})");
                continue;
            }
            Debug.Log($"处理放置物品 {placed.uniqueId}，类型：{placed.category}，位置：({baseX},{baseY}), 尺寸：({placed.gridWidth}x{placed.gridHeight})");
            for (int r = 0; r < placed.gridHeight; r++)
            {
                for (int c = 0; c < placed.gridWidth; c++)
                {
                    int globalX = baseX + c;
                    int globalY = baseY + r;
                    if (globalX < 0 || globalX >= width || globalY < 0 || globalY >= height)
                    {
                        Debug.LogWarning($"Global coordinate out of range: ({globalX},{globalY}) for placed item {placed.uniqueId}");
                        continue;
                    }
                    if (placed.category == EditorItemCategory.Building)
                    {
                        // Sector 层
                        int sectorVal = GetProvidedMazeValue(placed.item, "sector", c, r);
                        Debug.Log($"Building {placed.uniqueId} - Sector local ({c},{r}) => value {sectorVal}");
                        if (sectorVal != -1)
                        {
                            if (sectorVal == 0)
                            {
                                mapSectorMaze[globalY, globalX] = 0;
                            }
                            else
                            {
                                string blockName = GetProvidedBlockName(placed.item, "sector", sectorVal);
                                Debug.Log($"Building {placed.uniqueId} - Sector local ({c},{r}) => block '{blockName}'");
                                int mappedIndex = GetOrAddBlockIndex(mapSectorBlockMapping, blockName);
                                mapSectorMaze[globalY, globalX] = mappedIndex;
                            }
                        }
                        // Arena 层
                        int arenaVal = GetProvidedMazeValue(placed.item, "arena", c, r);
                        Debug.Log($"Building {placed.uniqueId} - Arena local ({c},{r}) => value {arenaVal}");
                        if (arenaVal != -1)
                        {
                            if (arenaVal == 0)
                            {
                                mapArenaMaze[globalY, globalX] = 0;
                            }
                            else
                            {
                                string blockName = GetProvidedBlockName(placed.item, "arena", arenaVal);
                                Debug.Log($"Building {placed.uniqueId} - Arena local ({c},{r}) => block '{blockName}'");
                                int mappedIndex = GetOrAddBlockIndex(mapArenaBlockMapping, blockName);
                                mapArenaMaze[globalY, globalX] = mappedIndex;
                            }
                        }
                        // Collision 层（Building 部分）
                        int buildingCollision = GetProvidedMazeValue(placed.item, "collision", c, r);
                        Debug.Log($"Building {placed.uniqueId} - Collision local ({c},{r}) => value {buildingCollision}");
                        if (buildingCollision != -1)
                        {
                            mapCollisionMaze[globalY, globalX] = CombineCollision(mapCollisionMaze[globalY, globalX], buildingCollision);
                        }
                    }
                    else if (placed.category == EditorItemCategory.Object)
                    {
                        // Game_object 层
                        int gameobjectVal = GetProvidedMazeValue(placed.item, "gameobject", c, r);
                        Debug.Log($"Object {placed.uniqueId} - Game_object local ({c},{r}) => value {gameobjectVal}");
                        if (gameobjectVal != -1)
                        {
                            if (gameobjectVal == 0)
                            {
                                mapGameobjectMaze[globalY, globalX] = 0;
                            }
                            else
                            {
                                string blockName = GetProvidedBlockName(placed.item, "gameobject", gameobjectVal);
                                Debug.Log($"Object {placed.uniqueId} - Game_object local ({c},{r}) => block '{blockName}'");
                                int mappedIndex = GetOrAddBlockIndex(mapGameobjectBlockMapping, blockName);
                                mapGameobjectMaze[globalY, globalX] = mappedIndex;
                            }
                        }
                        // Collision 层（Object 部分）
                        int objectCollision = GetProvidedMazeValue(placed.item, "collision", c, r);
                        Debug.Log($"Object {placed.uniqueId} - Collision local ({c},{r}) => value {objectCollision}");
                        if (objectCollision != -1)
                        {
                            mapCollisionMaze[globalY, globalX] = CombineCollision(mapCollisionMaze[globalY, globalX], objectCollision);
                        }
                    }
                }
            }
        }
        Debug.Log("新版 Map Maze 数据重构完成");
    }

    // ---------------- 新增：从实际文件中获取局部 Maze 数值 ----------------
    // 参数 layer: "sector", "arena", "gameobject", "collision"
    // localX, localY: 相对于物品左上角的坐标
    // 返回 -1 表示该位置为空气，不参与更新
    private int GetProvidedMazeValue(EditorItem item, string layer, int localX, int localY)
    {
        string folderName = GetFolderName(item);
        string filePath = "";
        if (item.category == EditorItemCategory.Building)
        {
            if (layer == "sector")
                filePath = Path.Combine(mapFolder, "buildings", folderName, "maze", "sector_maze.csv");
            else if (layer == "arena")
                filePath = Path.Combine(mapFolder, "buildings", folderName, "maze", "arena_maze.csv");
            else if (layer == "collision")
                filePath = Path.Combine(mapFolder, "buildings", folderName, "maze", "collision_maze.csv");
        }
        else if (item.category == EditorItemCategory.Object)
        {
            if (layer == "gameobject")
                filePath = Path.Combine(mapFolder, "objects", folderName, "maze", "game_object_maze.csv");
            else if (layer == "collision")
                filePath = Path.Combine(mapFolder, "objects", folderName, "maze", "collision_maze.csv");
        }
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("Maze file not found: " + filePath);
            return -1;
        }
        string[] lines = File.ReadAllLines(filePath);
        if (localY >= lines.Length)
        {
            Debug.LogWarning("LocalY out of range in file: " + filePath);
            return -1;
        }
        string[] cols = lines[localY].Split(',');
        if (localX >= cols.Length)
        {
            Debug.LogWarning("LocalX out of range in file: " + filePath);
            return -1;
        }
        int value;
        if (int.TryParse(cols[localX].Trim(), out value))
            return value;
        else
            return -1;
    }

    // ---------------- 新增：从实际文件中获取提供的 Block 名称 ----------------
    // 根据物品、层级和 Maze 数值返回 Block 名称
    private string GetProvidedBlockName(EditorItem item, string layer, int mazeValue)
    {
        if (mazeValue == 0)
            return "";
        string folderName = GetFolderName(item);
        string filePath = "";
        if (item.category == EditorItemCategory.Building)
        {
            if (layer == "sector")
                filePath = Path.Combine(mapFolder, "buildings", folderName, "special_blocks", "sector_blocks.csv");
            else if (layer == "arena")
                filePath = Path.Combine(mapFolder, "buildings", folderName, "special_blocks", "arena_blocks.csv");
            else if (layer == "collision")
                return "";
        }
        else if (item.category == EditorItemCategory.Object)
        {
            if (layer == "gameobject")
                filePath = Path.Combine(mapFolder, "objects", folderName, "special_blocks", "game_object_blocks.csv");
            else if (layer == "collision")
                return "";
        }
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("Block file not found: " + filePath);
            return "";
        }
        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line))
                continue;
            string[] parts = line.Split(',');
            if (parts.Length < 2)
                continue;
            int index;
            if (int.TryParse(parts[0].Trim(), out index))
            {
                if (index == mazeValue)
                    return parts[1].Trim();
            }
        }
        Debug.LogWarning("No matching block found in file: " + filePath + " for index: " + mazeValue);
        return "";
    }

    // ---------------- 新增：在 Block 映射中查找或新增 Block 名称对应的索引 ----------------
    private int GetOrAddBlockIndex(Dictionary<string, int> mapping, string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return 0;
        if (mapping.ContainsKey(blockName))
            return mapping[blockName];
        else
        {
            int newIndex = mapping.Count + 1;
            mapping[blockName] = newIndex;
            Debug.Log($"New block mapping added: '{blockName}' -> {newIndex}");
            return newIndex;
        }
    }

    // ---------------- 新增：合并 Collision 值 ----------------
    private int CombineCollision(int existing, int newVal)
    {
        if (existing != 0 || newVal != 0)
            return (existing != 0 ? existing : newVal);
        return 0;
    }

    public string GetBlockName(int id, bool isSector)
    {
        var dict = isSector ? mapSectorBlockMapping : mapArenaBlockMapping;
        foreach (var kv in dict)
            if (kv.Value == id)
                return kv.Key;
        return id.ToString();
    }
}
