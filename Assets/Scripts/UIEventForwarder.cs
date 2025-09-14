// �ļ���UIEventForwarder.cs
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ���������ӽڵ��ϲ����Ĺ������Ҽ���ק�¼���ת��������� MapZoomController / MapDragController��
/// ���ı�ԭ�е��/hover ��Ϊ���������¼�͸����
/// </summary>
public class UIEventForwarder : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private GameObject zoomTargetGO; // �� MapZoomController �Ķ���ͨ���� mapContent��
    private GameObject dragTargetGO; // �� MapDragController �Ķ���ͨ���� mapContent��

    private void Awake()
    {
        var zoom = GetComponentInParent<MapZoomController>();
        var drag = GetComponentInParent<MapDragController>();
        zoomTargetGO = zoom ? zoom.gameObject : null;
        dragTargetGO = drag ? drag.gameObject : null;
    }

    // ���֣�һ��͸���� MapZoomController
    public void OnScroll(PointerEventData eventData)
    {
        if (zoomTargetGO != null)
            ExecuteEvents.Execute<IScrollHandler>(zoomTargetGO, eventData, ExecuteEvents.scrollHandler);
    }

    // �Ҽ���ק��ʼ�����Ҽ�ʱ͸���� MapDragController
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (dragTargetGO != null)
            ExecuteEvents.Execute<IBeginDragHandler>(dragTargetGO, eventData, ExecuteEvents.beginDragHandler);
    }

    // �Ҽ���ק��
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (dragTargetGO != null)
            ExecuteEvents.Execute<IDragHandler>(dragTargetGO, eventData, ExecuteEvents.dragHandler);
    }

    // �Ҽ���ק����
    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (dragTargetGO != null)
            ExecuteEvents.Execute<IEndDragHandler>(dragTargetGO, eventData, ExecuteEvents.endDragHandler);
    }
}
