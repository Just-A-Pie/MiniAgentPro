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
    [Header("������ԴĿ¼")]
    // ���δ�ֶ����ã����Զ��� GameManager.Instance.resourcePath ��ȡ
    public string baseDirectory;

    [Header("����")]
    public ToolPanelManager toolPanelManager;

    private void Start()
    {
        if (string.IsNullOrEmpty(baseDirectory))
        {
            if (GameManager.Instance != null)
                baseDirectory = GameManager.Instance.resourcePath;
            else
                Debug.LogError("δ�ҵ� GameManager �� resourcePath δ���ã�");
        }

        // ���� Building ��������
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
                    newItem.itemName = folderName;  // �� meta.world_name
                    newItem.gridWidth = meta.maze_width;
                    newItem.gridHeight = meta.maze_height;
                    newItem.category = EditorItemCategory.Building;
                    // ʹ�ù̶���ͼ���� "texture.png"
                    string pngPath = Path.Combine(folder, "texture.png");
                    newItem.thumbnail = LoadSpriteFromFile(pngPath);
                    toolPanelManager.availableItems.Add(newItem);
                }
            }
        }

        // ���� Object ��������
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

        // ���� ToolPanelManager ������ʾ
        toolPanelManager.PopulateToolItems();
    }

    Sprite LoadSpriteFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("�ļ�������: " + filePath);
            return null;
        }
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2);
        if (tex.LoadImage(fileData))
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return null;
    }
}
