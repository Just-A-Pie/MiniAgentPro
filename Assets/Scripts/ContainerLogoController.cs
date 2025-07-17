using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ContainerLogoController 负责生成并管理物品对应的容器图标（logo）。
/// 本版本将 logo 对象放入 MapManager.Instance.mapContent 下专用的 LogoContainer 中，
/// 并在 LogoContainer 内创建一个 LogoHolder 用于接收鼠标事件，
/// LogoHolder 下挂载实际显示 logo 的 LogoImage 对象（Image.raycastTarget 为 false）。
/// 当鼠标悬停在 LogoHolder 上时，将 LogoImage 设为半透明；
/// 当鼠标点击 LogoHolder 时，事件会被转发到其下方的目标，
/// 以实现 logo 不阻挡物品交互的效果。
/// 若全局配置管理器 (ContainerLogoConfigManager) 存在，则使用其中设置的参数；
/// 否则采用本脚本的备用默认值。
/// </summary>
public class ContainerLogoController : MonoBehaviour
{
    #region 备用配置（无全局配置时生效）
    [Header("备用配置（无全局配置时生效）")]
    [Tooltip("容器图标使用的 Sprite")]
    public Sprite containerLogoSprite;
    [Tooltip("容器图标的大小（宽×高，单位像素）")]
    public Vector2 logoSize = new Vector2(20, 20);
    [Tooltip("容器图标相对于物品上边缘中点的偏移，Y > 0 表示向上留出空隙")]
    public Vector2 offset = new Vector2(0, 2);
    #endregion

    // 内部缓存 logoHolder 对象
    private GameObject logoHolder;

    /// <summary>
    /// 根据 hasContainer 参数更新 logo 显示状态
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
    /// 创建 logo 及其承载对象：
    /// 1. 获取专用 LogoContainer（在 MapManager.mapContent 下）。
    /// 2. 在该容器下创建 LogoHolder，并添加 CanvasGroup 和 EventTrigger 等以处理鼠标事件。
    /// 3. 在 LogoHolder 内创建 LogoImage 对象显示 logo，且其 raycastTarget 设为 false。
    /// 4. 根据物品 RectTransform 计算 LogoHolder 的初始位置：底边紧贴物品上边缘且水平居中。
    /// 5. 调用 SetAsLastSibling 保证 LogoContainer 始终最上层。
    /// </summary>
    private void CreateLogo()
    {
        // 获取专用 LogoContainer
        Transform logoContainer = GetLogoContainer();

        // 创建 LogoHolder 作为 logo 的承载对象
        logoHolder = new GameObject("LogoHolder", typeof(RectTransform));
        logoHolder.transform.SetParent(logoContainer, false);
        RectTransform holderRT = logoHolder.GetComponent<RectTransform>();
        // 设置 anchors 和 pivot 为左上角（便于使用绝对坐标进行定位）
        holderRT.anchorMin = new Vector2(0, 1);
        holderRT.anchorMax = new Vector2(0, 1);
        holderRT.pivot = new Vector2(0.5f, 0f); // 底边中心

        // 添加 CanvasGroup，用于修改透明度；这里允许接收事件
        CanvasGroup cg = logoHolder.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;
        cg.alpha = 1f;

        // 添加 EventTrigger 处理 PointerEnter/Exit/Click
        EventTrigger trigger = logoHolder.AddComponent<EventTrigger>();
        // PointerEnter：将 LogoImage 透明度设置为 0.5
        EventTrigger.Entry entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) =>
        {
            Debug.Log("Logo PointerEnter triggered");
            SetLogoAlpha(0.5f);
        });
        trigger.triggers.Add(entryEnter);
        // PointerExit：恢复透明度为 1
        EventTrigger.Entry entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) =>
        {
            Debug.Log("Logo PointerEnter triggered");
            SetLogoAlpha(1f);
        });
        trigger.triggers.Add(entryExit);
        // PointerClick：转发点击事件
        EventTrigger.Entry entryClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entryClick.callback.AddListener((data) =>
        {
            ForwardClickEvent((PointerEventData)data);
        });
        trigger.triggers.Add(entryClick);

        // 在 LogoHolder 内创建 LogoImage 用于显示实际图标
        GameObject logoImageGO = new GameObject("LogoImage", typeof(Image));
        logoImageGO.transform.SetParent(logoHolder.transform, false);
        Image logoImg = logoImageGO.GetComponent<Image>();
        // 设置 logo 图像使用的 Sprite（全局配置优先）
        if (ContainerLogoConfigManager.Instance != null && ContainerLogoConfigManager.Instance.containerLogoSprite != null)
            logoImg.sprite = ContainerLogoConfigManager.Instance.containerLogoSprite;
        else
            logoImg.sprite = containerLogoSprite;
        logoImg.preserveAspect = true;
        // 将 LogoImage 设为不接收射线（以便点击穿透到 LogoHolder下方的物品）
        logoImg.raycastTarget = false;

        // 设置 LogoImage 的 RectTransform为全尺寸填充 LogoHolder
        RectTransform logoImageRT = logoImageGO.GetComponent<RectTransform>();
        logoImageRT.anchorMin = new Vector2(0, 0);
        logoImageRT.anchorMax = new Vector2(1, 1);
        logoImageRT.offsetMin = Vector2.zero;
        logoImageRT.offsetMax = Vector2.zero;

        // 设置 LogoHolder 的尺寸与 LogoImage 大小相同
        if (ContainerLogoConfigManager.Instance != null)
            holderRT.sizeDelta = ContainerLogoConfigManager.Instance.logoSize;
        else
            holderRT.sizeDelta = logoSize;

        // 计算 LogoHolder 的初始位置：基于物品的 RectTransform
        RectTransform objectRT = GetComponent<RectTransform>();
        if (objectRT != null)
        {
            // 物品 anchoredPosition（假设 anchors=(0,1) 且 pivot=(0,1)）
            Vector2 objectPos = objectRT.anchoredPosition;
            float objectWidth = objectRT.sizeDelta.x;
            // 希望 LogoHolder 水平居中于物品，底边与物品上边缘对齐
            Vector2 logoPos = new Vector2(objectPos.x + objectWidth * 0.5f, objectPos.y);
            if (ContainerLogoConfigManager.Instance != null)
                logoPos += ContainerLogoConfigManager.Instance.offset;
            else
                logoPos += offset;
            holderRT.anchoredPosition = logoPos;
        }
        else
        {
            Debug.LogWarning("ContainerLogoController 未挂载在带有 RectTransform 的物品上，无法计算 logo 位置");
        }

        // 确保 LogoContainer 始终在 mapContent 最上层
        logoContainer.SetAsLastSibling();
    }

    /// <summary>
    /// 设置 LogoImage 的透明度
    /// </summary>
    /// <param name="alpha">目标透明度</param>
    private void SetLogoAlpha(float alpha)
    {
        // LogoImage 在 LogoHolder 的第一个子对象
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
    /// 将点击事件转发到 logo 下方物体
    /// </summary>
    /// <param name="data"></param>
    private void ForwardClickEvent(PointerEventData data)
    {
        // 在 logo所在位置做一次射线检测，获取下方的第一个物体
        // 这里假设 MapManager.Instance.mapContent 上有 GraphicRaycaster
        GraphicRaycaster raycaster = MapManager.Instance.mapContent.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            Debug.LogWarning("ForwardClickEvent: MapContent 缺少 GraphicRaycaster");
            return;
        }
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = data.position
        };
        var results = new System.Collections.Generic.List<RaycastResult>();
        raycaster.Raycast(pointerData, results);
        // 找到第一个不是 logoHolder 的目标（即下方物品）
        foreach (RaycastResult result in results)
        {
            if (result.gameObject != logoHolder)
            {
                // 触发该物体的点击事件
                ExecuteEvents.Execute(result.gameObject, data, ExecuteEvents.pointerClickHandler);
                break;
            }
        }
    }

    /// <summary>
    /// 如果物品位置变化，可调用此方法更新 LogoHolder 位置
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
    /// 获取或创建专用的 LogoContainer，挂在 MapManager.Instance.mapContent 下，
    /// 用于存放所有 logo 对象，并始终保持在最上层。
    /// </summary>
    private Transform GetLogoContainer()
    {
        Transform mapContent = MapManager.Instance.mapContent;
        if (mapContent == null)
        {
            Debug.LogError("GetLogoContainer: MapManager.Instance.mapContent 未设置！");
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
