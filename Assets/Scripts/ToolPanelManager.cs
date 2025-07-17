using System.Collections.Generic;
using UnityEngine;

public class ToolPanelManager : MonoBehaviour
{
    [Header("������Ʒ�б�")]
    public List<EditorItem> availableItems = new List<EditorItem>();

    [Header("UI ����")]
    // Content ����ScrollRect �ڣ�
    public Transform contentPanel;
    // Ԥ���壺ToolItemButton���� Assets/Prefab �д�����Ԥ���壩
    public GameObject toolItemButtonPrefab;

    [Header("��ǰ��������")]
    public EditorItemCategory filterCategory = EditorItemCategory.All;

    public void PopulateToolItems()
    {
        // ��յ�ǰ Content ����������
        foreach (Transform child in contentPanel)
            Destroy(child.gameObject);

        // ���� availableItems�����ݹ�����������Ԥ����
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
