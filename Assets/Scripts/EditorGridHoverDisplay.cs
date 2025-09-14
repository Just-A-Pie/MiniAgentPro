using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class EditorGridHoverDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("基础设置")]
    public float gridSize = 32f;     // 设计时每格像素
    public TMP_Text hoverText;       // 可空；为空则不显示但仍计算

    private RectTransform mapImageRect;    // 运行时从 MapManager 取
    private float backgroundScale = 1f;
    private Canvas rootCanvas;             // 用于拿正确的 eventCamera

    void OnEnable()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        StartCoroutine(BindWhenReady());
    }

    IEnumerator BindWhenReady()
    {
        while (mapImageRect == null)
        {
            var mm = FindObjectOfType<MapManager>();
            if (mm != null && mm.mapImage != null)
            {
                mapImageRect = mm.mapImage.rectTransform;
                backgroundScale = mm.backgroundScaleFactor;
                // Debug.Log("[EditorGridHoverDisplay] mapImageRect 绑定成功");
                break;
            }
            yield return null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => UpdateHoverText(eventData);

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverText != null) hoverText.text = "";
    }

    public void OnPointerMove(PointerEventData eventData) => UpdateHoverText(eventData);

    private void UpdateHoverText(PointerEventData eventData)
    {
        if (mapImageRect == null)
        {
            // 运行途中再尝试一次绑定
            var mm = FindObjectOfType<MapManager>();
            if (mm == null || mm.mapImage == null) return;
            mapImageRect = mm.mapImage.rectTransform;
            backgroundScale = mm.backgroundScaleFactor;
        }

        // 选一个“正确的相机”：
        // - Overlay：传 null
        // - Camera/World：优先 Canvas.worldCamera；退而求其次用 enterEventCamera
        Camera cam = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : eventData.enterEventCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapImageRect, eventData.position, cam, out var localPoint))
        {
            return; // 转换失败（相机不对/没命中）
        }

        // 左上为 (0,0)，Y 向下为负
        Vector2 topLeft = new Vector2(mapImageRect.rect.xMin, mapImageRect.rect.yMax);
        Vector2 p = localPoint - topLeft;

        float effective = gridSize * backgroundScale;
        int gridX = Mathf.FloorToInt(p.x / effective);
        int gridY = Mathf.FloorToInt(-p.y / effective);

        if (hoverText != null)
            hoverText.text = $"Grid: ({gridX}, {gridY})";
        // else Debug.Log($"Grid: ({gridX}, {gridY})");
    }
}
