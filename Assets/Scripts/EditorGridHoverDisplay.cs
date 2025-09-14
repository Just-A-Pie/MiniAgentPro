using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class EditorGridHoverDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("��������")]
    public float gridSize = 32f;     // ���ʱÿ������
    public TMP_Text hoverText;       // �ɿգ�Ϊ������ʾ���Լ���

    private RectTransform mapImageRect;    // ����ʱ�� MapManager ȡ
    private float backgroundScale = 1f;
    private Canvas rootCanvas;             // ��������ȷ�� eventCamera

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
                // Debug.Log("[EditorGridHoverDisplay] mapImageRect �󶨳ɹ�");
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
            // ����;���ٳ���һ�ΰ�
            var mm = FindObjectOfType<MapManager>();
            if (mm == null || mm.mapImage == null) return;
            mapImageRect = mm.mapImage.rectTransform;
            backgroundScale = mm.backgroundScaleFactor;
        }

        // ѡһ������ȷ���������
        // - Overlay���� null
        // - Camera/World������ Canvas.worldCamera���˶�������� enterEventCamera
        Camera cam = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : eventData.enterEventCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapImageRect, eventData.position, cam, out var localPoint))
        {
            return; // ת��ʧ�ܣ��������/û���У�
        }

        // ����Ϊ (0,0)��Y ����Ϊ��
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
