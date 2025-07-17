using UnityEngine;

public class EditorManager : MonoBehaviour
{
    public static EditorManager Instance;
    public EditorItem currentSelectedItem;
    public float gridSize = 32f; // 每个格子 32×32

    private void Awake()
    {
        SetSelectedItem(null);
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetSelectedItem(EditorItem item)
    {
        currentSelectedItem = item;
        Debug.Log("当前选中物品：" + (item != null ? item.itemName : "无"));
    }
}
