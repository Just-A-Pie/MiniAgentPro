using UnityEngine;

/// <summary>
/// 工具类：强制将 RectTransform 设置为锚点= (0,0)-(0,0), pivot=(0,0), anchoredPos= (0,0)
/// 这样 (0,0) 就是父容器的左上角
/// </summary>
public static class RectTransformHelper
{
    public static void SetToTopLeft(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;   // (0,0)
        rt.anchorMax = Vector2.zero;   // (0,0)
        rt.pivot = Vector2.zero;   // (0,0)
        rt.anchoredPosition = Vector2.zero;
    }
}
