// 文件：SimulationUIController.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 管理 Simulation 模式下物品的高亮和信息面板显示
/// </summary>
public class SimulationUIController : MonoBehaviour
{
    public static SimulationUIController Instance;

    [Header("信息面板控制器 引用")]
    public InformationPanelController infoPanel;

    // 存放 uniqueId -> Outline
    private Dictionary<string, Outline> outlineMap = new Dictionary<string, Outline>();
    // 自维护一份 Simulation 放置物字典：uniqueId -> PlacedItem
    private Dictionary<string, MapManager.PlacedItem> simPlacedItems = new Dictionary<string, MapManager.PlacedItem>();

    private string hoveredId;
    private string pinnedId;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[SimulationUIController] Instance assigned");
        }
        else
        {
            Destroy(gameObject);
        }

        if (infoPanel == null)
            Debug.LogError("[SimulationUIController] infoPanel 引用未设置！");
    }

    /// <summary>
    /// SimulationMapRenderer 在创建每个实例时调用
    /// </summary>
    public void RegisterItem(string uniqueId, Outline outline, MapManager.PlacedItem placedItem)
    {
        outlineMap[uniqueId] = outline;
        simPlacedItems[uniqueId] = placedItem;
        Debug.Log($"[SimulationUIController] Registered item id={uniqueId}  totalItems={simPlacedItems.Count}");
    }

    public void OnItemPointerEnter(string id)
    {
        Debug.Log($"[SimulationUIController] OnItemPointerEnter id={id}");
        hoveredId = id;
        Refresh();
    }

    public void OnItemPointerExit(string id)
    {
        Debug.Log($"[SimulationUIController] OnItemPointerExit id={id}");
        if (hoveredId == id) hoveredId = null;
        Refresh();
    }

    public void OnItemPointerClick(string id)
    {
        Debug.Log($"[SimulationUIController] OnItemPointerClick id={id}");
        pinnedId = (pinnedId == id) ? null : id;
        Refresh();
    }

    /// <summary>
    /// Refresh 会更新高亮状态并根据 hoveredId/pinnedId 控制信息面板显示。
    /// </summary>
    private void Refresh()
    {
        // 1) 更新描边高亮
        foreach (var kv in outlineMap)
        {
            bool highlight = (kv.Key == pinnedId);
            kv.Value.enabled = highlight;
        }

        // 2) 决定当前要显示哪一个
        string active = pinnedId ?? hoveredId;
        Debug.Log($"[SimulationUIController] Refresh(): hovered={hoveredId}, pinned={pinnedId}, active={active}");

        if (!string.IsNullOrEmpty(active))
        {
            if (simPlacedItems.TryGetValue(active, out var pi))
            {
                Debug.Log($"[SimulationUIController] Showing OtherInfo for id={active}");
                infoPanel.SetOtherInfo(pi);
            }
            else
            {
                Debug.LogWarning($"[SimulationUIController] No simPlacedItems entry for id={active}");
                infoPanel.Clear();
            }
        }
        else
        {
            Debug.Log("[SimulationUIController] Clearing panel (no active id)");
            infoPanel.Clear();
        }
    }

    /// <summary>
    /// 外部若需查询某个 uniqueId 对应的 itemName，可以用这个方法
    /// </summary>
    public bool TryGetItemName(string uniqueId, out string itemName)
    {
        if (simPlacedItems.TryGetValue(uniqueId, out var pi))
        {
            itemName = pi.item.itemName;
            return true;
        }
        itemName = null;
        return false;
    }
}
