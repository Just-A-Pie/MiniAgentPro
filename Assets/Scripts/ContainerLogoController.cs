using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ContainerLogoController �������ɲ�������Ʒ��Ӧ������ͼ�꣨logo����
/// ���汾�� logo ������� MapManager.Instance.mapContent ��ר�õ� LogoContainer �У�
/// ���� LogoContainer �ڴ���һ�� LogoHolder ���ڽ�������¼���
/// LogoHolder �¹���ʵ����ʾ logo �� LogoImage ����Image.raycastTarget Ϊ false����
/// �������ͣ�� LogoHolder ��ʱ���� LogoImage ��Ϊ��͸����
/// ������� LogoHolder ʱ���¼��ᱻת�������·���Ŀ�꣬
/// ��ʵ�� logo ���赲��Ʒ������Ч����
/// ��ȫ�����ù����� (ContainerLogoConfigManager) ���ڣ���ʹ���������õĲ�����
/// ������ñ��ű��ı���Ĭ��ֵ��
/// </summary>
public class ContainerLogoController : MonoBehaviour
{
    #region �������ã���ȫ������ʱ��Ч��
    [Header("�������ã���ȫ������ʱ��Ч��")]
    [Tooltip("����ͼ��ʹ�õ� Sprite")]
    public Sprite containerLogoSprite;
    [Tooltip("����ͼ��Ĵ�С������ߣ���λ���أ�")]
    public Vector2 logoSize = new Vector2(20, 20);
    [Tooltip("����ͼ���������Ʒ�ϱ�Ե�е��ƫ�ƣ�Y > 0 ��ʾ����������϶")]
    public Vector2 offset = new Vector2(0, 2);
    #endregion

    // �ڲ����� logoHolder ����
    private GameObject logoHolder;

    /// <summary>
    /// ���� hasContainer �������� logo ��ʾ״̬
    /// </summary>
    public void UpdateLogoVisibility(bool hasContainer)
    {
        if (hasContainer)
        {
            if (logoHolder == null)
            {
                CreateLogo();
            }
            logoHolder.SetActive(true);
        }
        else
        {
            if (logoHolder != null)
                logoHolder.SetActive(false);
        }
    }

    /// <summary>
    /// ���� logo ������ض���
    /// 1. ��ȡר�� LogoContainer���� MapManager.mapContent �£���
    /// 2. �ڸ������´��� LogoHolder������� CanvasGroup �� EventTrigger ���Դ�������¼���
    /// 3. �� LogoHolder �ڴ��� LogoImage ������ʾ logo������ raycastTarget ��Ϊ false��
    /// 4. ������Ʒ RectTransform ���� LogoHolder �ĳ�ʼλ�ã��ױ߽�����Ʒ�ϱ�Ե��ˮƽ���С�
    /// 5. ���� SetAsLastSibling ��֤ LogoContainer ʼ�����ϲ㡣
    /// </summary>
    private void CreateLogo()
    {
        // ��ȡר�� LogoContainer
        Transform logoContainer = GetLogoContainer();

        // ���� LogoHolder ��Ϊ logo �ĳ��ض���
        logoHolder = new GameObject("LogoHolder", typeof(RectTransform));
        logoHolder.transform.SetParent(logoContainer, false);
        RectTransform holderRT = logoHolder.GetComponent<RectTransform>();
        // ���� anchors �� pivot Ϊ���Ͻǣ�����ʹ�þ���������ж�λ��
        holderRT.anchorMin = new Vector2(0, 1);
        holderRT.anchorMax = new Vector2(0, 1);
        holderRT.pivot = new Vector2(0.5f, 0f); // �ױ�����

        // ��� CanvasGroup�������޸�͸���ȣ�������������¼�
        CanvasGroup cg = logoHolder.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;
        cg.alpha = 1f;

        // ��� EventTrigger ���� PointerEnter/Exit/Click
        EventTrigger trigger = logoHolder.AddComponent<EventTrigger>();
        // PointerEnter���� LogoImage ͸��������Ϊ 0.5
        EventTrigger.Entry entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) =>
        {
            Debug.Log("Logo PointerEnter triggered");
            SetLogoAlpha(0.5f);
        });
        trigger.triggers.Add(entryEnter);
        // PointerExit���ָ�͸����Ϊ 1
        EventTrigger.Entry entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) =>
        {
            Debug.Log("Logo PointerEnter triggered");
            SetLogoAlpha(1f);
        });
        trigger.triggers.Add(entryExit);
        // PointerClick��ת������¼�
        EventTrigger.Entry entryClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entryClick.callback.AddListener((data) =>
        {
            ForwardClickEvent((PointerEventData)data);
        });
        trigger.triggers.Add(entryClick);

        // �� LogoHolder �ڴ��� LogoImage ������ʾʵ��ͼ��
        GameObject logoImageGO = new GameObject("LogoImage", typeof(Image));
        logoImageGO.transform.SetParent(logoHolder.transform, false);
        Image logoImg = logoImageGO.GetComponent<Image>();
        // ���� logo ͼ��ʹ�õ� Sprite��ȫ���������ȣ�
        if (ContainerLogoConfigManager.Instance != null && ContainerLogoConfigManager.Instance.containerLogoSprite != null)
            logoImg.sprite = ContainerLogoConfigManager.Instance.containerLogoSprite;
        else
            logoImg.sprite = containerLogoSprite;
        logoImg.preserveAspect = true;
        // �� LogoImage ��Ϊ���������ߣ��Ա�����͸�� LogoHolder�·�����Ʒ��
        logoImg.raycastTarget = false;

        // ���� LogoImage �� RectTransformΪȫ�ߴ���� LogoHolder
        RectTransform logoImageRT = logoImageGO.GetComponent<RectTransform>();
        logoImageRT.anchorMin = new Vector2(0, 0);
        logoImageRT.anchorMax = new Vector2(1, 1);
        logoImageRT.offsetMin = Vector2.zero;
        logoImageRT.offsetMax = Vector2.zero;

        // ���� LogoHolder �ĳߴ��� LogoImage ��С��ͬ
        if (ContainerLogoConfigManager.Instance != null)
            holderRT.sizeDelta = ContainerLogoConfigManager.Instance.logoSize;
        else
            holderRT.sizeDelta = logoSize;

        // ���� LogoHolder �ĳ�ʼλ�ã�������Ʒ�� RectTransform
        RectTransform objectRT = GetComponent<RectTransform>();
        if (objectRT != null)
        {
            // ��Ʒ anchoredPosition������ anchors=(0,1) �� pivot=(0,1)��
            Vector2 objectPos = objectRT.anchoredPosition;
            float objectWidth = objectRT.sizeDelta.x;
            // ϣ�� LogoHolder ˮƽ��������Ʒ���ױ�����Ʒ�ϱ�Ե����
            Vector2 logoPos = new Vector2(objectPos.x + objectWidth * 0.5f, objectPos.y);
            if (ContainerLogoConfigManager.Instance != null)
                logoPos += ContainerLogoConfigManager.Instance.offset;
            else
                logoPos += offset;
            holderRT.anchoredPosition = logoPos;
        }
        else
        {
            Debug.LogWarning("ContainerLogoController δ�����ڴ��� RectTransform ����Ʒ�ϣ��޷����� logo λ��");
        }

        // ȷ�� LogoContainer ʼ���� mapContent ���ϲ�
        logoContainer.SetAsLastSibling();
    }

    /// <summary>
    /// ���� LogoImage ��͸����
    /// </summary>
    /// <param name="alpha">Ŀ��͸����</param>
    private void SetLogoAlpha(float alpha)
    {
        // LogoImage �� LogoHolder �ĵ�һ���Ӷ���
        if (logoHolder != null && logoHolder.transform.childCount > 0)
        {
            Image logoImg = logoHolder.transform.GetChild(0).GetComponent<Image>();
            if (logoImg != null)
            {
                Color c = logoImg.color;
                c.a = alpha;
                logoImg.color = c;
            }
        }
    }

    /// <summary>
    /// ������¼�ת���� logo �·�����
    /// </summary>
    /// <param name="data"></param>
    private void ForwardClickEvent(PointerEventData data)
    {
        // �� logo����λ����һ�����߼�⣬��ȡ�·��ĵ�һ������
        // ������� MapManager.Instance.mapContent ���� GraphicRaycaster
        GraphicRaycaster raycaster = MapManager.Instance.mapContent.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            Debug.LogWarning("ForwardClickEvent: MapContent ȱ�� GraphicRaycaster");
            return;
        }
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = data.position
        };
        var results = new System.Collections.Generic.List<RaycastResult>();
        raycaster.Raycast(pointerData, results);
        // �ҵ���һ������ logoHolder ��Ŀ�꣨���·���Ʒ��
        foreach (RaycastResult result in results)
        {
            if (result.gameObject != logoHolder)
            {
                // ����������ĵ���¼�
                ExecuteEvents.Execute(result.gameObject, data, ExecuteEvents.pointerClickHandler);
                break;
            }
        }
    }

    /// <summary>
    /// �����Ʒλ�ñ仯���ɵ��ô˷������� LogoHolder λ��
    /// </summary>
    public void RefreshLogoPosition()
    {
        if (logoHolder != null)
        {
            RectTransform holderRT = logoHolder.GetComponent<RectTransform>();
            RectTransform objectRT = GetComponent<RectTransform>();
            if (objectRT != null && holderRT != null)
            {
                Vector2 objectPos = objectRT.anchoredPosition;
                float objectWidth = objectRT.sizeDelta.x;
                Vector2 logoPos = new Vector2(objectPos.x + objectWidth * 0.5f, objectPos.y);
                if (ContainerLogoConfigManager.Instance != null)
                    logoPos += ContainerLogoConfigManager.Instance.offset;
                else
                    logoPos += offset;
                holderRT.anchoredPosition = logoPos;
            }
        }
    }

    /// <summary>
    /// ��ȡ�򴴽�ר�õ� LogoContainer������ MapManager.Instance.mapContent �£�
    /// ���ڴ������ logo ���󣬲�ʼ�ձ��������ϲ㡣
    /// </summary>
    private Transform GetLogoContainer()
    {
        Transform mapContent = MapManager.Instance.mapContent;
        if (mapContent == null)
        {
            Debug.LogError("GetLogoContainer: MapManager.Instance.mapContent δ���ã�");
            return transform;
        }
        Transform container = mapContent.Find("LogoContainer");
        if (container == null)
        {
            GameObject logoContainerGO = new GameObject("LogoContainer", typeof(RectTransform));
            logoContainerGO.transform.SetParent(mapContent, false);
            RectTransform rt = logoContainerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            logoContainerGO.transform.SetAsLastSibling();
            container = logoContainerGO.transform;
        }
        else
        {
            container.SetAsLastSibling();
        }
        return container;
    }
}
