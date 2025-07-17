// �ļ�: ItemCreator.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// �༭�� & Simulation ���õ���Ʒʵ�������ߡ�
/// * ������ EditingPage������д�� MapManager.placedItems��ʹ�� MapManager.backgroundScaleFactor��
/// * ������ SimulationPage��MapManager ����Ϊ null ����
///   1) �� SimulationMapManager ��ȡ�������ӣ�
///   2) ��������д�� placedItems��
///   3) ����Կɵ��� PopupManager��readOnlyMode����
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
        // 1) �������� & ����
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

        // �㼶��Object > Building > ����
        if (cat == EditorItemCategory.Object)
            go.transform.SetAsLastSibling();
        else
            go.transform.SetSiblingIndex(mapContent.childCount - 1);

        // 3) ID & ��������
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

        // 4) д�� MapManager.placedItems (���༭��)
        if (MapManager.Instance != null)
        {
            MapManager.Instance.placedItems.Add(placedItem);
            Debug.Log("[ItemCreator] д�� MapManager.placedItems");
        }

        // 5) �������
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => OnItemClicked(placedItem, popupManager));

        // 6) ���� Logo (���༭�� & �� container)
        if (MapManager.Instance != null)
        {
            ContainerLogoController logoCtrl = go.GetComponent<ContainerLogoController>();
            if (logoCtrl == null) logoCtrl = go.AddComponent<ContainerLogoController>();
            bool hasContainer = copiedItem.attributes != null &&
                                copiedItem.attributes.ContainsKey("container") &&
                                !string.IsNullOrEmpty(copiedItem.attributes["container"]);
            logoCtrl.UpdateLogoVisibility(hasContainer);
        }

        // 7) ����ಢˢ�� Overlay
        if (MapManager.Instance != null)
        {
            MapManager.Instance.isDirty = true;
            if (GridOverlayManager.Instance != null &&
                GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
            {
                GridOverlayManager.Instance.RefreshOverlay();
            }
        }
    }

    private static void OnItemClicked(MapManager.PlacedItem selected, PopupManager popupManager)
    {
        if (popupManager == null)
        {
            Debug.LogWarning("[ItemCreator] PopupManager δ�󶨣��޷�����");
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
