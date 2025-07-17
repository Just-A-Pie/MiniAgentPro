using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class GridHoverDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    // ԭʼÿ�����ӵĳߴ磨����32���أ������ǵ�ͼ���ʱ�ĸ��ӳߴ�
    public float gridSize = 32f;
    // ������ʾ��ǰ������ڸ�������� TMP_Text ���
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
            Debug.LogWarning("SimulationMapManager �� mapImage δ���ã�");
            return;
        }
        // ʹ��ʵ����Ⱦ�ĵ�ͼ���� Image �� RectTransform
        RectTransform mapImageRect = SimulationMapManager.Instance.mapImage.rectTransform;
        Vector2 localPoint;
        // ����Ļ����ת��Ϊ mapImageRect �ڵľֲ�����
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapImageRect, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            // �����ͼ�������Ͻǵ����꣨Rect �� xMin, yMax��
            Vector2 topLeft = new Vector2(mapImageRect.rect.xMin, mapImageRect.rect.yMax);
            // ���� localPoint��ʹ�����Ͻ�Ϊ (0,0)
            Vector2 adjustedPoint = localPoint - topLeft;
            // ��Ч���ӳߴ� = gridSize * backgroundScaleFactor���� SimulationMapManager �м�����������ӣ�
            float effectiveGridSize = gridSize * SimulationMapManager.Instance.backgroundScaleFactor;
            int gridX = Mathf.FloorToInt(adjustedPoint.x / effectiveGridSize);
            int gridY = Mathf.FloorToInt(-adjustedPoint.y / effectiveGridSize);
            if (hoverText != null)
                hoverText.text = $"Grid: ({gridX}, {gridY})";
        }
    }
}
