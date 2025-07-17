using System.Collections.Generic;
using UnityEngine;
using System.IO;

[System.Serializable]
public class MazeMetaInfo
{
    public int typeId;
    public string world_name;
    public int maze_width;
    public int maze_height;
    public float sq_tile_size;
    public string special_constraint;
}

public class DirectoryLoader : MonoBehaviour
{
    [Header("基础资源目录")]
    // 如果未手动设置，则自动从 GameManager.Instance.resourcePath 获取
    public string baseDirectory;

    [Header("引用")]
    public ToolPanelManager toolPanelManager;

    private void Start()
    {
        if (string.IsNullOrEmpty(baseDirectory))
        {
            if (GameManager.Instance != null)
                baseDirectory = GameManager.Instance.resourcePath;
            else
                Debug.LogError("未找到 GameManager 或 resourcePath 未设置！");
        }

        // 加载 Building 类型数据
        string buildingDirectory = Path.Combine(baseDirectory, "buildings");
        Debug.Log("Building Directory: " + buildingDirectory);
        if (Directory.Exists(buildingDirectory))
        {
            string[] buildingFolders = Directory.GetDirectories(buildingDirectory);
            foreach (string folder in buildingFolders)
            {
                string folderName = Path.GetFileName(folder);
                string jsonPath = Path.Combine(folder, "maze_meta_info.json");
                if (File.Exists(jsonPath))
                {
                    string jsonText = File.ReadAllText(jsonPath);
                    MazeMetaInfo meta = JsonUtility.FromJson<MazeMetaInfo>(jsonText);
                    EditorItem newItem = new EditorItem();
                    newItem.typeId = meta.typeId;
                    newItem.itemName = folderName;  // 或 meta.world_name
                    newItem.gridWidth = meta.maze_width;
                    newItem.gridHeight = meta.maze_height;
                    newItem.category = EditorItemCategory.Building;
                    // 使用固定贴图名称 "texture.png"
                    string pngPath = Path.Combine(folder, "texture.png");
                    newItem.thumbnail = LoadSpriteFromFile(pngPath);
                    toolPanelManager.availableItems.Add(newItem);
                }
            }
        }

        // 加载 Object 类型数据
        string objectDirectory = Path.Combine(baseDirectory, "objects");
        Debug.Log("Object Directory: " + objectDirectory);
        if (Directory.Exists(objectDirectory))
        {
            string[] objectFolders = Directory.GetDirectories(objectDirectory);
            foreach (string folder in objectFolders)
            {
                string folderName = Path.GetFileName(folder);
                string jsonPath = Path.Combine(folder, "maze_meta_info.json");
                if (File.Exists(jsonPath))
                {
                    string jsonText = File.ReadAllText(jsonPath);
                    MazeMetaInfo meta = JsonUtility.FromJson<MazeMetaInfo>(jsonText);
                    EditorItem newItem = new EditorItem();
                    newItem.typeId = meta.typeId;
                    newItem.itemName = folderName;
                    newItem.gridWidth = meta.maze_width;
                    newItem.gridHeight = meta.maze_height;
                    newItem.category = EditorItemCategory.Object;
                    string pngPath = Path.Combine(folder, "texture.png");
                    newItem.thumbnail = LoadSpriteFromFile(pngPath);
                    toolPanelManager.availableItems.Add(newItem);
                }
            }
        }

        // 调用 ToolPanelManager 更新显示
        toolPanelManager.PopulateToolItems();
    }

    Sprite LoadSpriteFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("文件不存在: " + filePath);
            return null;
        }
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2);
        if (tex.LoadImage(fileData))
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return null;
    }
}
