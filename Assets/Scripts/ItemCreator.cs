using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 编辑器 & Simulation 共用的物品实例化工具。
/// * 若处于 EditingPage：依旧写入 MapManager.placedItems，使用 MapManager.backgroundScaleFactor。
/// * 若处于 SimulationPage：MapManager 可能为 null ——
///   1) 从 SimulationMapManager 获取缩放因子；
///   2) 不把数据写入 placedItems；
///   3) 点击仍可弹出 PopupManager（readOnlyMode）。
/// </summary>
public static class ItemCreator
{
    public static void CreateItemInstanceWithClick(
    EditorItem item,
    int gridX,
    int gridY,
    EditorItemCategory cat,
    RectTransform mapContent,
    PopupManager popupManager,
    string uniqueId = null,
    string itemName = null,
    System.Collections.Generic.Dictionary<string, string> attributes = null)
    {
        // 1) 缩放因子 & 坐标
        float factor = 1f;
        if (MapManager.Instance != null)
            factor = MapManager.Instance.backgroundScaleFactor;
        else if (SimulationMapManager.Instance != null)
            factor = SimulationMapManager.Instance.backgroundScaleFactor;

        Vector2 pos = new Vector2(gridX * 32f * factor, -gridY * 32f * factor);
        Debug.Log($"[ItemCreator] Create {item.itemName} typeId={item.typeId} grid=({gridX},{gridY}) factor={factor}");

        // 2) GameObject & Image
        GameObject go = new GameObject($"{cat}_Loaded", typeof(Image));
        go.transform.SetParent(mapContent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = item.thumbnail;
        RectTransform rt = img.rectTransform;
        rt.sizeDelta = new Vector2(item.gridWidth * 32f * factor,
                                   item.gridHeight * 32f * factor);
        rt.pivot = new Vector2(0, 1);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.anchoredPosition = pos;

        // 层级：Object > Building > 背景
        if (cat == EditorItemCategory.Object)
            go.transform.SetAsLastSibling();
        else
            go.transform.SetSiblingIndex(mapContent.childCount - 1);

        // 让滚轮/右键拖拽事件传递到父层控制器
        go.AddComponent<UIEventForwarder>();

        // 3) ID & 副本数据
        if (string.IsNullOrEmpty(uniqueId))
            uniqueId = System.Guid.NewGuid().ToString();
        go.name = uniqueId;
        if (string.IsNullOrEmpty(itemName))
            itemName = item.itemName;

        EditorItem copiedItem = new EditorItem
        {
            uniqueId = uniqueId,
            typeId = item.typeId,
            itemName = itemName,
            gridWidth = item.gridWidth,
            gridHeight = item.gridHeight,
            category = item.category,
            thumbnail = item.thumbnail,
            attributes = attributes != null
                        ? new System.Collections.Generic.Dictionary<string, string>(attributes)
                        : new System.Collections.Generic.Dictionary<string, string>()
        };

        MapManager.PlacedItem placedItem = new MapManager.PlacedItem
        {
            uniqueId = uniqueId,
            item = copiedItem,
            category = cat,
            typeId = item.typeId,
            gridX = gridX,
            gridY = gridY,
            gridWidth = item.gridWidth,
            gridHeight = item.gridHeight
        };

        // 4) 写入 MapManager.placedItems (仅编辑器)
        if (MapManager.Instance != null)
        {
            MapManager.Instance.placedItems.Add(placedItem);
            Debug.Log("[ItemCreator] 写入 MapManager.placedItems");

            // ★ 新增：添加后按物品足迹增量标脏（让重建走增量分支）
            MapManager.Instance.MarkDirtyByPlacedItem(placedItem);
        }

        // 5) 点击弹窗
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => OnItemClicked(placedItem, popupManager));

        // 6) 容器“气泡” (仅编辑器 & 有 container)
        if (MapManager.Instance != null)
        {
            ContainerLogoController logoCtrl = go.GetComponent<ContainerLogoController>();
            if (logoCtrl == null) logoCtrl = go.AddComponent<ContainerLogoController>();

            bool hasContainer = copiedItem.attributes != null &&
                                copiedItem.attributes.ContainsKey("container") &&
                                !string.IsNullOrEmpty(copiedItem.attributes["container"]);

            // 显示气泡（首次会创建并设置 icon）
            logoCtrl.UpdateLogoVisibility(hasContainer);
        }

        // 7) 标记脏并刷新 Overlay（直接走带遮罩协程，避免“未构建/已脏”分支判断）
        if (MapManager.Instance != null)
        {
            MapManager.Instance.isDirty = true;
            if (GridOverlayManager.Instance != null &&
                GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
            {
                GridOverlayManager.Instance.RefreshWithOverlay();
            }
        }
    }


    private static void OnItemClicked(MapManager.PlacedItem selected, PopupManager popupManager)
    {
        if (popupManager == null)
        {
            Debug.LogWarning("[ItemCreator] PopupManager 未绑定，无法弹窗");
            return;
        }

        MapManager.PlacedItem toShow = selected;
        if (MapManager.Instance != null)
        {
            toShow = MapManager.Instance.placedItems.Find(x => x.uniqueId == selected.uniqueId);
        }

        popupManager.ShowPopup(toShow);
    }
}
