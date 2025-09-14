// 文件：UIEventForwarder.cs
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 把悬浮在子节点上产生的滚轮与右键拖拽事件，转发给父层的 MapZoomController / MapDragController。
/// 不改变原有点击/hover 行为，仅增加事件透传。
/// </summary>
public class UIEventForwarder : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private GameObject zoomTargetGO; // 带 MapZoomController 的对象（通常是 mapContent）
    private GameObject dragTargetGO; // 带 MapDragController 的对象（通常是 mapContent）

    private void Awake()
    {
        var zoom = GetComponentInParent<MapZoomController>();
        var drag = GetComponentInParent<MapDragController>();
        zoomTargetGO = zoom ? zoom.gameObject : null;
        dragTargetGO = drag ? drag.gameObject : null;
    }

    // 滚轮：一律透传给 MapZoomController
    public void OnScroll(PointerEventData eventData)
    {
        if (zoomTargetGO != null)
            ExecuteEvents.Execute<IScrollHandler>(zoomTargetGO, eventData, ExecuteEvents.scrollHandler);
    }

    // 右键拖拽开始：仅右键时透传给 MapDragController
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (dragTargetGO != null)
            ExecuteEvents.Execute<IBeginDragHandler>(dragTargetGO, eventData, ExecuteEvents.beginDragHandler);
    }

    // 右键拖拽中
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (dragTargetGO != null)
            ExecuteEvents.Execute<IDragHandler>(dragTargetGO, eventData, ExecuteEvents.dragHandler);
    }

    // 右键拖拽结束
    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (dragTargetGO != null)
            ExecuteEvents.Execute<IEndDragHandler>(dragTargetGO, eventData, ExecuteEvents.endDragHandler);
    }
}
