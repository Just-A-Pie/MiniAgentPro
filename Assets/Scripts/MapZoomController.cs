using UnityEngine;
using UnityEngine.EventSystems;

public class MapZoomController : MonoBehaviour, IScrollHandler
{
    [Header("缩放参数")]
    public float zoomSpeed = 0.5f;   // 缩放速度
    public float minScale = 0.1f;
    public float maxScale = 8.0f;

    public void OnScroll(PointerEventData eventData)
    {
        float scroll = eventData.scrollDelta.y;  // 鼠标滚轮值
        if (Mathf.Abs(scroll) > 0.01f)
        {
            RectTransform rt = transform as RectTransform;
            Vector3 oldScale = rt.localScale;

            // 1) 记录 "缩放前" 鼠标在MapContent内的本地坐标
            Vector2 oldLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out oldLocalPos);

            // 2) 缩放
            float scaleFactor = 1 + scroll * zoomSpeed;
            Vector3 newScale = oldScale * scaleFactor;
            newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
            newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
            newScale.z = 1f;
            rt.localScale = newScale;

            // 3) 记录 "缩放后" 鼠标的本地坐标
            Vector2 newLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, eventData.position, eventData.pressEventCamera, out newLocalPos);

            // 4) offset = (newLocalPos - oldLocalPos)
            Vector2 offset = (newLocalPos - oldLocalPos);

            // === 核心修改 ===
            // 把 anchoredPosition += offset，尝试保持鼠标点不变
            rt.anchoredPosition += offset;

            // Debug 用来观察 offset 值
            // Debug.Log($"scroll={scroll} oldLocal={oldLocalPos} newLocal={newLocalPos} offset={offset} => anchoredPos={rt.anchoredPosition}");
        }
    }
}
