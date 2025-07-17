using System.Collections.Generic;
using UnityEngine;

public class ToolPanelManager : MonoBehaviour
{
    [Header("所有物品列表")]
    public List<EditorItem> availableItems = new List<EditorItem>();

    [Header("UI 引用")]
    // Content 对象（ScrollRect 内）
    public Transform contentPanel;
    // 预制体：ToolItemButton（在 Assets/Prefab 中创建的预制体）
    public GameObject toolItemButtonPrefab;

    [Header("当前过滤类型")]
    public EditorItemCategory filterCategory = EditorItemCategory.All;

    public void PopulateToolItems()
    {
        // 清空当前 Content 中所有子项
        foreach (Transform child in contentPanel)
            Destroy(child.gameObject);

        // 遍历 availableItems，根据过滤条件生成预制体
        foreach (var item in availableItems)
        {
            if (filterCategory == EditorItemCategory.All || item.category == filterCategory)
            {
                GameObject btnObj = Instantiate(toolItemButtonPrefab, contentPanel);
                ToolItemButton tib = btnObj.GetComponent<ToolItemButton>();
                if (tib != null)
                    tib.Setup(item);
            }
        }
    }

    public void SetFilterCategory(int categoryIndex)
    {
        filterCategory = (EditorItemCategory)categoryIndex;
        PopulateToolItems();
    }
}
