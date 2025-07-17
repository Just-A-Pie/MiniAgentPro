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

    [Header("��ͼ��ʾ����")]
    public RectTransform mapDisplayArea; // MapDisplayPanel

    [Header("��ͼ��������")]
    public RectTransform mapContent;     // ���������������������

    [Header("������Դ�ļ���")]
    public string mapFolder; // ���Ϊ�գ���� GameManager.Instance.resourcePath ��ȡ

    [Header("��ͼ��ͼ�ļ���")]
    public string mapTextureFileName = "texture.png";

    // ȫ�ֵ�ͼԪ���ݣ������ {mapFolder}/map/maze_meta_info.json��
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

    // ÿ�� Building/Object ����Դ���ݰ����ڸ����ļ����µ� maze_meta_info.json��
    // ���ڹ��� typeId ���ļ������Ƶ�ӳ��
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

    // ��ͼ����
    public Image mapImage;

    [Header("��������������")]
    public ToolPanelManager toolPanelManager;

    [Header("��������")]
    public float gridSize = 32f;             // ÿ������32��32
    public float backgroundScaleFactor = 1f; // ��ͼ������������

    // ---------------- �������ƶ�ģʽ����ֶ� ----------------
    [Header("�ƶ�ģʽ���")]
    public bool isMoveMode = false;
    public PlacedItem movingItem;

    // ============== ԭ�����ݣ�����ʱʹ�ã��ɰ棩 ==============
    // �ɰ� Maze ���飺��������ʱ���á����á��жϣ��ɰ��߼���
    private int[,] sectorMaze;         // ��Ӧ�ɰ� sector_maze.csv
    private int[,] objectMaze;         // ��Ӧ�ɰ� game_object_maze.csv

    [Header("CSV �ļ���(�� map/maze Ŀ¼��) - �ɰ�")]
    public string sectorMazeFile = "sector_maze.csv";
    public string objectMazeFile = "game_object_maze.csv";

    // ============== �������ݣ����ڱ���ʱ����ȫ�� Map ���ݣ� ==============
    // �°� Map �� Maze ���ݣ��Ĳ㣩
    public int[,] mapSectorMaze;
    public int[,] mapArenaMaze;
    public int[,] mapGameobjectMaze;
    public int[,] mapCollisionMaze;
    // �°�ȫ�� Maze �ļ���������ʱʹ�ã������ {mapFolder}/map/maze/ �£�
    public string arenaMazeFile = "arena_maze.csv";
    public string gameObjectMazeFileNew = "game_object_maze.csv";
    public string collisionMazeFile = "collision_maze.csv";

    // �°�ȫ�� Block ӳ���ļ�������� {mapFolder}/map/special_blocks/ �£�
    public string sectorBlockFile = "sector_blocks.csv";
    public string arenaBlockFile = "arena_blocks.csv";
    public string gameObjectBlockFile = "game_object_blocks.csv";

    private Dictionary<string, int> mapSectorBlockMapping;
    private Dictionary<string, int> mapArenaBlockMapping;
    private Dictionary<string, int> mapGameobjectBlockMapping;

    // ============== ���������� typeId ������Դ�ļ������Ƶ�ӳ���ֵ� ==============
    // ���ڲ���ʵ����Դ�ļ������ƣ���ֱ��ʹ�� item.itemName�����ǻ��ڸ���Դ�µ� maze_meta_info.json
    private Dictionary<int, string> buildingFolderMapping = new Dictionary<int, string>();
    private Dictionary<int, string> objectFolderMapping = new Dictionary<int, string>();

    // �Ƿ����޸� => ���ڿ������ð�ť
    public bool isDirty = false;

    // �ѷ�����Ŀ��Ϣ������ JSON �� buildings_data / objects_data
    [Serializable]
    public struct PlacedItem
    {
        public string uniqueId;  // ÿ����ƷΨһID
        public EditorItem item;
        public EditorItemCategory category;
        public int typeId;
        public int gridX;
        public int gridY;
        public int gridWidth;
        public int gridHeight;
    }
    public List<PlacedItem> placedItems = new List<PlacedItem>();

    // �����л����������ת�� Dictionary��JsonUtility ��֧��ֱ�����л� Dictionary��
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
                Debug.LogError("δ���� mapFolder ��δ�ҵ� GameManager");
        }

        // ������Դ�ļ���ӳ�䣨���ڸ���Դ�µ� maze_meta_info.json��
        BuildResourceFolderMappings();

        string actualMapFolder = Path.Combine(mapFolder, "map");
        Debug.Log("ʵ�ʵ�ͼ���������ļ��У�" + actualMapFolder);

        // ��ȡȫ�ֵ�ͼԪ����
        string metaPath = Path.Combine(actualMapFolder, "maze_meta_info.json");
        if (File.Exists(metaPath))
        {
            string jsonText = File.ReadAllText(metaPath);
            mapMeta = JsonUtility.FromJson<MapMetaInfo>(jsonText);
            Debug.Log("���ص�ͼԪ���ݣ�" + mapMeta.world_name);
        }
        else
        {
            Debug.LogWarning("δ�ҵ���ͼԪ���ݣ�" + metaPath);
        }

        // ���ر�����ͼ
        string texturePath = Path.Combine(actualMapFolder, mapTextureFileName);
        if (!File.Exists(texturePath))
        {
            Debug.LogWarning("δ�ҵ���ͼ��ͼ��" + texturePath);
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

            // ���ô�С��λ��
            RectTransform imgRT = mapImage.rectTransform;
            imgRT.sizeDelta = newSize;
            imgRT.anchoredPosition = Vector2.zero;
            mapImage.transform.SetSiblingIndex(0);

            // ���� ������ͳһΪ���Ͻ�����ϵ ���� 
            imgRT.anchorMin = new Vector2(0, 1);
            imgRT.anchorMax = new Vector2(0, 1);
            imgRT.pivot = new Vector2(0, 1);
            imgRT.anchoredPosition = Vector2.zero;
            // ���� ���� ���� 

            Debug.Log($"�ɹ����ص�ͼ��ͼ��ԭʼ�ߴ磺{tex.width}x{tex.height}�����ź�{newSize.x}x{newSize.y}");
        }



        // ================= ԭ�в��֣���ʼ���ɰ� Maze ���� =================
        sectorMaze = new int[mapMeta.maze_height, mapMeta.maze_width];
        objectMaze = new int[mapMeta.maze_height, mapMeta.maze_width];

        string mazeDir = Path.Combine(actualMapFolder, "maze");
        LoadMazeCsv(Path.Combine(mazeDir, sectorMazeFile), sectorMaze);
        LoadMazeCsv(Path.Combine(mazeDir, objectMazeFile), objectMaze);

        // ================= �������֣���ʼ���°� Map Maze ���� =================
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

        // ���س�ʼ��������
        string buildingDataPath = Path.Combine(actualMapFolder, "map_data", "buildings_data.json");
        initialBuildingsData = new BuildingsData();
        if (File.Exists(buildingDataPath))
        {
            string bjson = File.ReadAllText(buildingDataPath);
            initialBuildingsData = JsonUtility.FromJson<BuildingsData>(bjson);
        }
        else
        {
            Debug.LogWarning("δ�ҵ����������ļ���" + buildingDataPath);
        }

        // ���س�ʼ��������
        string objectDataPath = Path.Combine(actualMapFolder, "map_data", "objects_data.json");
        initialObjectsData = new ObjectsData();
        if (File.Exists(objectDataPath))
        {
            string ojson = File.ReadAllText(objectDataPath);
            initialObjectsData = JsonUtility.FromJson<ObjectsData>(ojson);
        }
        else
        {
            Debug.LogWarning("δ�ҵ����������ļ���" + objectDataPath);
        }

        // ��ʼ����ͼ�����ؽ��������󵽳����У�
        ReloadMapData();
        // ���� �����������Դ��� ���� 
        // ���� (3,3)��1��1 ���Ӵ�С
        float cellSize = gridSize * backgroundScaleFactor;

        // �½�һ�� UI ����
        GameObject testMarker = new GameObject("TestMarker", typeof(Image));
        testMarker.transform.SetParent(mapContent, false);

        // ��ɫ��ͼ
        var img = testMarker.GetComponent<Image>();
        img.color = Color.red;  // ���� Color.green��Color.blue

        // ��λ�����Ͻǵ� (3,3) ��
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(cellSize, cellSize);
        rt.anchoredPosition = new Vector2(3 * cellSize, -3 * cellSize);
        // ���� ���Դ������ ���� 
        isDirty = false;
    }

    // ---------------- ���������������Ͷ������Դ�ļ���ӳ���ֵ� ----------------
    private void BuildResourceFolderMappings()
    {
        // ����ӳ�䣺ɨ�� {mapFolder}/buildings ��ÿ�����ļ��е� maze_meta_info.json
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
        // ����ӳ�䣺ɨ�� {mapFolder}/objects ��ÿ�����ļ��е� maze_meta_info.json
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

    // ---------------- �޸ģ�GetFolderName �������� typeId �����ļ������� ----------------
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

    // ---------------- ԭ�д��뱣�ֲ��� ----------------

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
            Debug.LogWarning($"δ�ҵ�Ҫɾ������Ʒ: {uniqueId}");
            return;
        }

        // �ݹ�ɾ��������Ʒ��������� container��
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

        // �ڳ��������ٶ�Ӧ�� UI GameObject
        foreach (Transform child in mapContent)
        {
            if (child.gameObject.name == uniqueId)
            {
                Destroy(child.gameObject);
                break;
            }
        }

        // ���б����Ƴ�
        placedItems.Remove(itemToRemove.Value);

        // ����ಢ�� Overlay ģʽ��ˢ��
        isDirty = true;
        if (GridOverlayManager.Instance != null &&
            GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
        {
            GridOverlayManager.Instance.RefreshOverlay();
        }

        Debug.Log($"ɾ����Ʒ�ɹ�: {uniqueId}");
    }


    private void LoadMazeCsv(string path, int[,] mazeData)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("Maze CSV������: " + path);
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

        // �޸ĵ㣺������ "MapImage" �� "LogoContainer"��ר�÷��� logo ��������
        for (int i = mapContent.childCount - 1; i >= 0; i--)
        {
            Transform child = mapContent.GetChild(i);
            if (child.gameObject.name == "MapImage" || child.gameObject.name == "LogoContainer")
                continue;
            Destroy(child.gameObject);
        }

        if (initialBuildingsData == null)
        {
            Debug.LogWarning("initialBuildingsData Ϊ null��");
        }
        else if (initialBuildingsData.buildings == null)
        {
            Debug.LogWarning("initialBuildingsData.buildings Ϊ null��");
        }
        else
        {
            Debug.Log("����������JSON���� " + initialBuildingsData.buildings.Count);
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
                    Debug.LogWarning($"δ�ҵ�����Ϊ Building �� typeId={entry.typeId} ����Ʒ");
                }
            }
        }

        if (initialObjectsData == null)
        {
            Debug.LogWarning("initialObjectsData Ϊ null��");
        }
        else if (initialObjectsData.objects == null)
        {
            Debug.LogWarning("initialObjectsData.objects Ϊ null��");
        }
        else
        {
            Debug.Log("����������JSON���� " + initialObjectsData.objects.Count);
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
        // ȷ�� LogoContainer ʼ�������ϲ�
        Transform logoContainer = mapContent.Find("LogoContainer");
        if (logoContainer != null)
            logoContainer.SetAsLastSibling();

        Debug.Log("��ͼ�����õ���ʼ״̬��");
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

    // �µı��淽�����ع��°� Map ���ݺ󱣴浽 CSV �ļ��У��������°����ݣ���Ӱ������ʱ�ɰ湦�ܣ�
    public void SaveAllCsv()
    {
        Debug.Log("��ʼ�����°� Map CSV ����...");
        // �ع��°� Map ����
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
        Debug.Log("�°� Maze CSV �� Block CSV �ѱ��棬resetButton �ɽ���");
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
        Debug.Log("������ Maze CSV ����Ʒ����");
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
        Debug.Log("��ͼ JSON �ѱ���");
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

    // ---------------- ���������� Block CSV ----------------
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

    // ---------------- ���������¹����°� Map �� Maze ���ݼ� Block ӳ�� ----------------
    public void RebuildMapMazeData()
    {
        Debug.Log("��ʼ�ع��°�ȫ�� Map Maze ����...");
        int height = mapMeta.maze_height;
        int width = mapMeta.maze_width;

        // ��ʼ���°���� Maze ����Ϊ0��0��ʾ�գ�
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
        Debug.Log("�°� Maze �����ʼ�����");

        // ��ʼ���°� Block ӳ���ֵ䣨�� sector��arena��game_object �㣩
        mapSectorBlockMapping = new Dictionary<string, int>();
        mapArenaBlockMapping = new Dictionary<string, int>();
        mapGameobjectBlockMapping = new Dictionary<string, int>();

        // ���������ѷ�����Ʒ�������°���� Maze ����
        foreach (var placed in placedItems)
        {
            int baseX = placed.gridX;
            int baseY = placed.gridY;
            // ���������Ʒ�� gridX �� gridY Ϊ��������������ȫ������
            if (baseX < 0 || baseY < 0)
            {
                Debug.LogWarning($"Skipping placed item {placed.uniqueId} due to negative grid position: ({baseX},{baseY})");
                continue;
            }
            Debug.Log($"���������Ʒ {placed.uniqueId}�����ͣ�{placed.category}��λ�ã�({baseX},{baseY}), �ߴ磺({placed.gridWidth}x{placed.gridHeight})");
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
                        // Sector ��
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
                        // Arena ��
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
                        // Collision �㣨Building ���֣�
                        int buildingCollision = GetProvidedMazeValue(placed.item, "collision", c, r);
                        Debug.Log($"Building {placed.uniqueId} - Collision local ({c},{r}) => value {buildingCollision}");
                        if (buildingCollision != -1)
                        {
                            mapCollisionMaze[globalY, globalX] = CombineCollision(mapCollisionMaze[globalY, globalX], buildingCollision);
                        }
                    }
                    else if (placed.category == EditorItemCategory.Object)
                    {
                        // Game_object ��
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
                        // Collision �㣨Object ���֣�
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
        Debug.Log("�°� Map Maze �����ع����");
    }

    // ---------------- ��������ʵ���ļ��л�ȡ�ֲ� Maze ��ֵ ----------------
    // ���� layer: "sector", "arena", "gameobject", "collision"
    // localX, localY: �������Ʒ���Ͻǵ�����
    // ���� -1 ��ʾ��λ��Ϊ���������������
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

    // ---------------- ��������ʵ���ļ��л�ȡ�ṩ�� Block ���� ----------------
    // ������Ʒ���㼶�� Maze ��ֵ���� Block ����
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

    // ---------------- �������� Block ӳ���в��һ����� Block ���ƶ�Ӧ������ ----------------
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

    // ---------------- �������ϲ� Collision ֵ ----------------
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
