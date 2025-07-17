using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class GridHoverDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    // 原始每个格子的尺寸（例如32像素），这是地图设计时的格子尺寸
    public float gridSize = 32f;
    // 用于显示当前鼠标所在格子坐标的 TMP_Text 组件
    public TMP_Text hoverText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        UpdateHoverText(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverText != null)
            hoverText.text = "";
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        UpdateHoverText(eventData);
    }

    private void UpdateHoverText(PointerEventData eventData)
    {
        if (SimulationMapManager.Instance == null || SimulationMapManager.Instance.mapImage == null)
        {
            Debug.LogWarning("SimulationMapManager 或 mapImage 未设置！");
            return;
        }
        // 使用实际渲染的地图背景 Image 的 RectTransform
        RectTransform mapImageRect = SimulationMapManager.Instance.mapImage.rectTransform;
        Vector2 localPoint;
        // 将屏幕坐标转换为 mapImageRect 内的局部坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapImageRect, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            // 计算地图背景左上角的坐标（Rect 的 xMin, yMax）
            Vector2 topLeft = new Vector2(mapImageRect.rect.xMin, mapImageRect.rect.yMax);
            // 调整 localPoint，使得左上角为 (0,0)
            Vector2 adjustedPoint = localPoint - topLeft;
            // 有效格子尺寸 = gridSize * backgroundScaleFactor（即 SimulationMapManager 中计算的缩放因子）
            float effectiveGridSize = gridSize * SimulationMapManager.Instance.backgroundScaleFactor;
            int gridX = Mathf.FloorToInt(adjustedPoint.x / effectiveGridSize);
            int gridY = Mathf.FloorToInt(-adjustedPoint.y / effectiveGridSize);
            if (hoverText != null)
                hoverText.text = $"Grid: ({gridX}, {gridY})";
        }
    }
}
