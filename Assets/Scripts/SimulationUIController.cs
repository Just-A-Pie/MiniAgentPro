// 文件：SimulationUIController.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SimulationUIController : MonoBehaviour
{
    public static SimulationUIController Instance;

    [Header("信息面板控制器 引用")]
    public InformationPanelController infoPanel;

    private Dictionary<string, Outline> outlineMap = new Dictionary<string, Outline>();
    private Dictionary<string, MapManager.PlacedItem> simPlacedItems = new Dictionary<string, MapManager.PlacedItem>();

    private string hoveredId;
    private string pinnedId;

    private SimulationAgentRenderer agentRendererRef;

    public bool HasPinnedObject => !string.IsNullOrEmpty(pinnedId);

    public void ClearPinned()
    {
        pinnedId = null;
        Refresh();
    }

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

        agentRendererRef = SimulationAgentRenderer.Instance ?? FindObjectOfType<SimulationAgentRenderer>();
    }

    public void RegisterItem(string uniqueId, Outline outline, MapManager.PlacedItem placedItem)
    {
        outlineMap[uniqueId] = outline;
        simPlacedItems[uniqueId] = placedItem;
        Debug.Log($"[SimulationUIController] Registered item id={uniqueId}  totalItems={simPlacedItems.Count}");
    }

    public void OnItemPointerEnter(string id)
    {
        hoveredId = id;
        Refresh();
    }

    public void OnItemPointerExit(string id)
    {
        if (hoveredId == id) hoveredId = null;
        Refresh();
    }

    public void OnItemPointerClick(string id)
    {
        pinnedId = (pinnedId == id) ? null : id;

        if (pinnedId != null)
        {
            // 固定物体时，清掉 Agent 固定
            (SimulationAgentRenderer.Instance ?? agentRendererRef)?.ClearPinned();
        }

        Refresh();
    }

    private void Refresh()
    {
        // 1) 始终先更新物体的描边（仅固定的那个开启）
        foreach (var kvp in outlineMap)
        {
            bool highlight = (kvp.Key == pinnedId);
            kvp.Value.enabled = highlight;
        }

        // 2) 仲裁：如果 Agent 处于“固定”状态，Object/Building 的“悬浮”不得改写/清空信息面板
        var agentRenderer = SimulationAgentRenderer.Instance ?? agentRendererRef;
        bool agentPinned = agentRenderer != null && agentRenderer.HasPinnedAgent;

        if (agentPinned && string.IsNullOrEmpty(pinnedId))
        {
            // 让 Agent 侧保持/恢复面板显示，不在此改写或清空
            agentRenderer?.ForceRefreshInfoPanel();
            return;
        }

        // 3) 正常逻辑：如果有固定对象，显示固定对象；否则显示悬浮对象；都没有则清空
        string active = pinnedId ?? hoveredId;
        if (!string.IsNullOrEmpty(active))
        {
            if (simPlacedItems.TryGetValue(active, out var pi))
            {
                infoPanel.SetOtherInfo(pi);
            }
            else
            {
                infoPanel.Clear();
            }
        }
        else
        {
            infoPanel.Clear();
        }
    }

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
