// �ļ�: BuildingPlacementController.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static MapManager;

public class BuildingPlacementController : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler
{
    [Header("���ò���")]
    public RectTransform mapContent;
    public float gridSize = 32f;
    public GameObject previewPrefab;

    [Header("����")]
    [Tooltip("�Ƿ��������á��ر���Ϊ���η��ã�����һ�κ��Զ�ȡ��ѡ�У���")]
    public bool continuousPlacement = false; // Ĭ�ϵ��η���

    private GameObject previewInstance;
    public PopupManager popupManager;

    // �� ������������һ��ѡ�е���Ʒ�������л�ʱˢ��Ԥ��
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

            // �� �л�ѡ�л��״δ���Ԥ��ʱ��ˢ��Ԥ������ͼ��ߴ�
            if (previewInstance != null && _lastSelected != selected)
            {
                RefreshPreviewVisual(selected);
                _lastSelected = selected;
            }

            if (previewInstance != null)
            {
                UpdatePreviewPosition(null); // ������꣨�������ӣ�
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

    // ˢ��Ԥ��ͼ����ߴ�
    private void RefreshPreviewVisual(EditorItem selected)
    {
        var img = previewInstance.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = selected.thumbnail;
            img.color = new Color(1, 1, 1, 0.6f); // ��͸��������Ԥ��
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

        // �� ���������̰��������㼣�����������ࣨ�� ItemCreator �ڱ��໥����ͻ����һ��Ҳ��ȫ��
        if (MapManager.Instance != null)
        {
            var r = new RectInt(gridX, gridY, selected.gridWidth, selected.gridHeight);
            MapManager.Instance.MarkDirtyRect(r);
            MapManager.Instance.isDirty = true;
        }

        // ˢ�� Overlay��ǿ���ߴ�����Э�̣�����ʱ��߽�
        if (GridOverlayManager.Instance != null &&
            GridOverlayManager.Instance.currentMode != GridOverlayManager.OverlayMode.None)
        {
            GridOverlayManager.Instance.RefreshWithOverlay();
        }

        // �D�D ���η����߼���Ĭ�����ã�������һ�κ�����Ԥ����ȡ��ѡ�� �D�D //
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

        // ����ಢˢ�� Overlay
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

        Debug.Log($"��Ʒ�ƶ���ɣ���λ��: ({gridX}, {gridY})");
    }
}
