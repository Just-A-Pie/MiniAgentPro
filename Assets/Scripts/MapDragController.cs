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

    // ��ʼ��ק
    public void OnBeginDrag(PointerEventData data)
    {
        // ֻʹ���Ҽ�
        if (data.button != PointerEventData.InputButton.Right)
            return;

        // ��¼��ǰ localPosition ��Ϊ��ʼλ��
        originalLocalPosition = rt.localPosition;

        // ��¼��ק��ʼʱ����ڸ������еı�������
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, data.position, data.pressEventCamera, out originalLocalPointerPosition);
    }

    // ��ק������
    public void OnDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Right)
            return;

        Vector2 localPointerPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT, data.position, data.pressEventCamera, out localPointerPos))
        {
            // ƫ����
            Vector2 offset = localPointerPos - originalLocalPointerPosition;
            rt.localPosition = originalLocalPosition + (Vector3)offset;

            // ����������ƣ������� clamp 
            // rt.localPosition = ClampToWindow(rt.localPosition);
        }
    }

    // ����������϶���Χ����ʵ��
    // private Vector3 ClampToWindow(Vector3 pos)
    // {
    //     // Example: do nothing => free drag
    //     return pos;
    // }
}
