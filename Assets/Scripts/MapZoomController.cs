using UnityEngine;
using UnityEngine.EventSystems;

public class MapZoomController : MonoBehaviour, IScrollHandler
{
    [Header("���Ų���")]
    public float zoomSpeed = 0.5f;   // �����ٶ�
    public float minScale = 0.1f;
    public float maxScale = 8.0f;

    public void OnScroll(PointerEventData eventData)
    {
        float scroll = eventData.scrollDelta.y;  // ������ֵ
        if (Mathf.Abs(scroll) > 0.01f)
        {
            RectTransform rt = transform as RectTransform;
            Vector3 oldScale = rt.localScale;

            // 1) ��¼ "����ǰ" �����MapContent�ڵı�������
            Vector2 oldLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out oldLocalPos);

            // 2) ����
            float scaleFactor = 1 + scroll * zoomSpeed;
            Vector3 newScale = oldScale * scaleFactor;
            newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
            newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
            newScale.z = 1f;
            rt.localScale = newScale;

            // 3) ��¼ "���ź�" ���ı�������
            Vector2 newLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out newLocalPos);

            // 4) offset = (newLocalPos - oldLocalPos)
            Vector2 offset = (newLocalPos - oldLocalPos);

            // === �����޸� ===
            // �� anchoredPosition += offset�����Ա������㲻��
            rt.anchoredPosition += offset;

            // Debug �����۲� offset ֵ
            // Debug.Log($"scroll={scroll} oldLocal={oldLocalPos} newLocal={newLocalPos} offset={offset} => anchoredPos={rt.anchoredPosition}");
        }
    }
}
