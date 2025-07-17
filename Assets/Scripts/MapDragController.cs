using UnityEngine;
using UnityEngine.EventSystems;

public class MapDragController : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private Vector2 originalLocalPointerPosition;
    private Vector3 originalLocalPosition;
    private RectTransform rt;
    private RectTransform parentRT;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        parentRT = rt.parent as RectTransform;
    }

    // 开始拖拽
    public void OnBeginDrag(PointerEventData data)
    {
        // 只使用右键
        if (data.button != PointerEventData.InputButton.Right)
            return;

        // 记录当前 localPosition 作为初始位置
        originalLocalPosition = rt.localPosition;

        // 记录拖拽起始时鼠标在父容器中的本地坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, data.position, data.pressEventCamera, out originalLocalPointerPosition);
    }

    // 拖拽进行中
    public void OnDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Right)
            return;

        Vector2 localPointerPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, data.position, data.pressEventCamera, out localPointerPos))
        {
            // 偏移量
            Vector2 offset = localPointerPos - originalLocalPointerPosition;
            rt.localPosition = originalLocalPosition + (Vector3)offset;

            // 如果想有限制，可以再 clamp 
            // rt.localPosition = ClampToWindow(rt.localPosition);
        }
    }

    // 如果想限制拖动范围，可实现
    // private Vector3 ClampToWindow(Vector3 pos)
    // {
    //     // Example: do nothing => free drag
    //     return pos;
    // }
}
