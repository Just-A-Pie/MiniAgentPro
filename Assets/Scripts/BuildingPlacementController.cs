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

    private GameObject previewInstance;
    public PopupManager popupManager;

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
                previewInstance.GetComponent<RectTransform>().pivot = new Vector2(0, 1);

                /* ---------- 新增：套上当前选中物品的缩略图 ---------- */
                var img = previewInstance.GetComponent<Image>();
                img.sprite = EditorManager.Instance.currentSelectedItem.thumbnail;
                img.color = new Color(1, 1, 1, 0.6f);          // 半透明看得出是预览
                float factor = MapManager.Instance?.backgroundScaleFactor ?? 1f;
                img.rectTransform.sizeDelta = new Vector2(
                    EditorManager.Instance.currentSelectedItem.gridWidth * gridSize * factor,
                    EditorManager.Instance.currentSelectedItem.gridHeight * gridSize * factor);
                /* ------------------------------------------------------ */
            }
            if (previewInstance != null)
            {
                UpdatePreviewPosition(null); // 重用逻辑
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
        }
    }

    private void UpdatePreviewPosition(PointerEventData eventData)
    {
        var selected = EditorManager.Instance.currentSelectedItem;
        if (selected == null) return;

        /* ---------- 新增：当 Update() 传进来的是 null ---------- */
        Vector2 screenPos = (eventData != null) ? eventData.position : (Vector2)Input.mousePosition;
        Camera screenCam = (eventData != null) ? eventData.pressEventCamera : null;   // UGUI 传 null 也行
        /* ------------------------------------------------------- */

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapContent, screenPos, screenCam, out localPoint))
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

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapContent, eventData.position, eventData.pressEventCamera, out localPoint))
            return;

        float factor = MapManager.Instance.backgroundScaleFactor;
        int gridX = Mathf.FloorToInt(localPoint.x / (gridSize * factor));
        int gridY = Mathf.FloorToInt(-localPoint.y / (gridSize * factor));

        ItemCreator.CreateItemInstanceWithClick(
            selected, gridX, gridY, selected.category,
            mapContent, popupManager);

        MapManager.Instance.isDirty = true;
        if (GridOverlayManager.Instance != null &&
            GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
        {
            GridOverlayManager.Instance.RefreshOverlay();
        }
    }

    private void MoveItem(PointerEventData eventData)
    {
        var movingItem = MapManager.Instance.movingItem;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapContent, eventData.position, eventData.pressEventCamera, out localPoint))
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
        EditorManager.Instance.SetSelectedItem(null);

        Debug.Log($"物品移动完成，新位置: ({gridX}, {gridY})");
    }
}
