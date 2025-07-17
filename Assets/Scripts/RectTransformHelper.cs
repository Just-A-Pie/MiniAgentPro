using UnityEngine;

/// <summary>
/// �����ࣺǿ�ƽ� RectTransform ����Ϊê��= (0,0)-(0,0), pivot=(0,0), anchoredPos= (0,0)
/// ���� (0,0) ���Ǹ����������Ͻ�
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
