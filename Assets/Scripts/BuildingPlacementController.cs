// 文件: BuildingPlacementController.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static MapManager;

public class BuildingPlacementController : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler
{
    [Header("配置参数")]
    public RectTransform mapContent;
    public float gridSize = 32f;
    public GameObject previewPrefab;

    [Header("交互")]
    [Tooltip("是否连续放置。关闭则为单次放置（放置一次后自动取消选中）。")]
    public bool continuousPlacement = false; // 默认单次放置

    private GameObject previewInstance;
    public PopupManager popupManager;

    // ★ 新增：缓存上一次选中的物品，用于切换时刷新预览
    private EditorItem _lastSelected;

    public void OnPointerMove(PointerEventData eventData)
    {
        UpdatePreviewPosition(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (MapManager.Instance.isMoveMode)
        {
            MoveItem(eventData);
            return;
        }

        PlaceItem(eventData);
    }

    void Update()
    {
        var selected = EditorManager.Instance.currentSelectedItem;
        bool isItem = selected != null &&
                      (selected.category == EditorItemCategory.Building ||
                       selected.category == EditorItemCategory.Object);

        if (isItem)
        {
            if (previewInstance == null && previewPrefab != null)
            {
                previewInstance = Instantiate(previewPrefab, mapContent);
                var prt = previewInstance.GetComponent<RectTransform>();
                prt.pivot = new Vector2(0, 1);
            }

            // ★ 切换选中或首次创建预览时，刷新预览的贴图与尺寸
            if (previewInstance != null && _lastSelected != selected)
            {
                RefreshPreviewVisual(selected);
                _lastSelected = selected;
            }

            if (previewInstance != null)
            {
                UpdatePreviewPosition(null); // 跟随鼠标（吸附格子）
                previewInstance.transform.SetAsLastSibling();
            }
        }
        else
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
            _lastSelected = null;
        }
    }

    // 刷新预览图像与尺寸
    private void RefreshPreviewVisual(EditorItem selected)
    {
        var img = previewInstance.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = selected.thumbnail;
            img.color = new Color(1, 1, 1, 0.6f); // 半透明，区分预览
            float factor = MapManager.Instance?.backgroundScaleFactor ?? 1f;
            img.rectTransform.sizeDelta = new Vector2(
                selected.gridWidth * gridSize * factor,
                selected.gridHeight * gridSize * factor);
        }
    }

    private void UpdatePreviewPosition(PointerEventData eventData)
    {
        var selected = EditorManager.Instance.currentSelectedItem;
        if (selected == null) return;

        Vector2 screenPos = (eventData != null) ? eventData.position : (Vector2)Input.mousePosition;
        Camera screenCam = (eventData != null) ? eventData.pressEventCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapContent, screenPos, screenCam, out var localPoint))
            return;

        float factor = MapManager.Instance.backgroundScaleFactor;
        int gridX = Mathf.FloorToInt(localPoint.x / (gridSize * factor));
        int gridY = Mathf.FloorToInt(-localPoint.y / (gridSize * factor));
        Vector2 snappedPos = new Vector2(gridX * gridSize * factor, -gridY * gridSize * factor);

        if (previewInstance != null)
            previewInstance.GetComponent<RectTransform>().anchoredPosition = snappedPos;
    }

    private void PlaceItem(PointerEventData eventData)
    {
        var selected = EditorManager.Instance.currentSelectedItem;
        if (selected == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapContent, eventData.position, eventData.pressEventCamera, out var localPoint))
            return;

        float factor = MapManager.Instance.backgroundScaleFactor;
        int gridX = Mathf.FloorToInt(localPoint.x / (gridSize * factor));
        int gridY = Mathf.FloorToInt(-localPoint.y / (gridSize * factor));

        ItemCreator.CreateItemInstanceWithClick(
            selected, gridX, gridY, selected.category,
            mapContent, popupManager);

        // ★ 新增：即刻按“新增足迹”做增量标脏（与 ItemCreator 内标脏互不冲突，多一次也安全）
        if (MapManager.Instance != null)
        {
            var r = new RectInt(gridX, gridY, selected.gridWidth, selected.gridHeight);
            MapManager.Instance.MarkDirtyRect(r);
            MapManager.Instance.isDirty = true;
        }

        // 刷新 Overlay：强制走带遮罩协程，避免时序边界
        if (GridOverlayManager.Instance != null &&
            GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
        {
            GridOverlayManager.Instance.RefreshWithOverlay();
        }

        // ―― 单次放置逻辑（默认启用）：放置一次后清理预览并取消选中 ―― //
        if (!continuousPlacement)
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
            _lastSelected = null;
            EditorManager.Instance.SetSelectedItem(null);
        }
    }


    private void MoveItem(PointerEventData eventData)
    {
        var movingItem = MapManager.Instance.movingItem;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapContent, eventData.position, eventData.pressEventCamera, out var localPoint))
            return;

        float factor = MapManager.Instance.backgroundScaleFactor;
        int gridX = Mathf.FloorToInt(localPoint.x / (gridSize * factor));
        int gridY = Mathf.FloorToInt(-localPoint.y / (gridSize * factor));

        movingItem.gridX = gridX;
        movingItem.gridY = gridY;

        foreach (Transform child in mapContent)
        {
            if (child.name == movingItem.uniqueId)
            {
                var rt = child.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(gridX * gridSize * factor, -gridY * gridSize * factor);
                var logo = child.GetComponent<ContainerLogoController>();
                if (logo != null) logo.RefreshLogoPosition();
                break;
            }
        }

        for (int i = 0; i < MapManager.Instance.placedItems.Count; i++)
        {
            if (MapManager.Instance.placedItems[i].uniqueId == movingItem.uniqueId)
            {
                MapManager.Instance.placedItems[i] = movingItem;
                break;
            }
        }

        // 标记脏并刷新 Overlay
        MapManager.Instance.isDirty = true;
        if (GridOverlayManager.Instance != null &&
            GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
        {
            GridOverlayManager.Instance.RefreshOverlay();
        }

        MapManager.Instance.isMoveMode = false;
        MapManager.Instance.movingItem = default(PlacedItem);

        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
        _lastSelected = null;
        EditorManager.Instance.SetSelectedItem(null);

        Debug.Log($"物品移动完成，新位置: ({gridX}, {gridY})");
    }
}
