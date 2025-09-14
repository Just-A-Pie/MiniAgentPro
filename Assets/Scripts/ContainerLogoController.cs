using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 在物品上方显示“容器气泡”预制体：
/// - 预制体内部包含名为 "icon" 的 Image，显示第一个子物品的缩略图；
/// - 预制体不拉伸，保持自身尺寸；
/// - 外层有一个 holder（只负责定位与接收/转发事件，预制体为纯视觉）；
/// - 悬停半透明、点击/滚轮/右键转发到底部；
/// - 时序：立即刷新 + 下一帧兜底 + 最多重试几帧；
/// - 定位：沿用初版的计算（假定物体 anchors=(0,1)、pivot=(0,1)），X=objectPos.x+0.5*width，Y=objectPos.y。
/// </summary>
public class ContainerLogoController : MonoBehaviour
{
    [Header("备用配置（无全局配置时生效）")]
    [Tooltip("当没有提供 prefab 或 icon 节点缺失时的占位图（一般不会用到）")]
    public Sprite fallbackSprite;
    [Tooltip("相对于物品上边缘中点的偏移（Y > 0 向上）")]
    public Vector2 offset = new Vector2(0, 2);

    // 仅用于定位和接事件；不改变 mapContent 其它子节点
    private GameObject _holder;
    // 气泡预制体实例（纯视觉，不接事件，不拉伸）
    private GameObject _bubble;

    // 时序重试
    private int _pendingTries = 0;
    private const int _maxTries = 5;

    /// <summary>更新显示状态。首次显示会创建 holder + 实例化预制体，并刷新 icon（含时序兜底）。</summary>
    public void UpdateLogoVisibility(bool hasContainer)
    {
        if (hasContainer)
        {
            if (_holder == null)
            {
                CreateHolderAndBubble();
                // 时序：立即 → 下一帧 → 若干帧
                RefreshIconFromFirstChild();
                LateRefresh();
                _pendingTries = _maxTries;
                StartCoroutine(_RetryRefresh());
            }
            _holder.SetActive(true);
            RefreshLogoPosition(); // 防止外部移动后未刷新
        }
        else
        {
            if (_holder != null) _holder.SetActive(false);
        }
    }

    /// <summary>当外部修改了 container 后可主动调用（例如弹窗里保存后）。</summary>
    public void LateRefresh()
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(_LateRefreshCo());
    }

    private System.Collections.IEnumerator _LateRefreshCo()
    {
        yield return new WaitForEndOfFrame();
        RefreshIconFromFirstChild();
        RefreshLogoPosition();
    }

    private System.Collections.IEnumerator _RetryRefresh()
    {
        while (_pendingTries-- > 0 && _holder != null)
        {
            yield return null; // 下一帧
            var ok = RefreshIconFromFirstChild();
            if (ok) yield break;
        }
    }

    /// <summary>
    /// 刷新预制体中 "icon" 的 Sprite 为第一个子物品的缩略图。
    /// 返回 true 表示这次设置到了非空 sprite。
    /// </summary>
    public bool RefreshIconFromFirstChild()
    {
        if (_bubble == null) return false;

        var mm = MapManager.Instance;
        if (mm == null || mm.placedItems == null) return false;

        // 自身（uniqueId == gameObject.name）
        var selfId = gameObject.name;
        int selfIdx = mm.placedItems.FindIndex(p => p.uniqueId == selfId);
        if (selfIdx < 0) return false;

        var self = mm.placedItems[selfIdx];
        if (self.item == null || self.item.attributes == null) return false;

        if (!self.item.attributes.TryGetValue("container", out var containerStr)) return false;
        if (string.IsNullOrWhiteSpace(containerStr)) return false;

        var firstChildId = containerStr
            .Split(',')
            .Select(s => s.Trim())
            .FirstOrDefault(s => !string.IsNullOrEmpty(s));
        if (string.IsNullOrEmpty(firstChildId)) return false;

        // 找子物品缩略图
        int childIdx = mm.placedItems.FindIndex(p => p.uniqueId == firstChildId);
        Sprite childThumb = null;
        if (childIdx >= 0)
        {
            var child = mm.placedItems[childIdx];
            if (child.item != null) childThumb = child.item.thumbnail;
        }

        // 写入 icon
        var iconImg = FindDeepChildImageByName(_bubble.transform, "icon");
        if (iconImg == null)
        {
            // 没有名为 icon 的 Image，尽量选一个最可能是图标的 Image
            iconImg = PickBestImageForIcon(_bubble.transform);
        }

        if (iconImg != null)
        {
            iconImg.sprite = childThumb != null ? childThumb : null;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false; // 视觉，不拦事件
            return childThumb != null;
        }

        // 彻底没有 Image，挂一个兜底
        var rootImg = _bubble.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = childThumb != null ? childThumb : fallbackSprite;
            rootImg.preserveAspect = true;
            rootImg.raycastTarget = false;
            return childThumb != null || fallbackSprite != null;
        }

        return false;
    }

    /// <summary>创建 holder（接事件与定位）+ 实例化预制体（视觉）。不拉伸预制体。</summary>
    private void CreateHolderAndBubble()
    {
        Transform logoContainer = GetLogoContainer();

        // 1) holder：锚定地图坐标系，大小由预制体决定；holder 只接事件，不渲染
        _holder = new GameObject("LogoHolder", typeof(RectTransform));
        _holder.transform.SetParent(logoContainer, false);
        var hrt = _holder.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(0, 1);
        hrt.pivot = new Vector2(0.5f, 0f); // 底边中心，贴物体顶部中点

        // 事件：悬停半透明、点击转发、滚轮/右键释放
        var cg = _holder.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;
        cg.alpha = 1f;

        var trigger = _holder.AddComponent<EventTrigger>();
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((_) => SetHolderAlpha(0.5f));
        trigger.triggers.Add(entryEnter);

        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((_) => SetHolderAlpha(1f));
        trigger.triggers.Add(entryExit);

        var entryClick = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entryClick.callback.AddListener((data) => ForwardClickEvent((PointerEventData)data));
        trigger.triggers.Add(entryClick);

        _holder.AddComponent<UIEventForwarder>();

        // 2) 气泡预制体：作为 holder 的子物体；不拉伸，保持自身 sizeDelta
        var prefab = ContainerLogoConfigManager.Instance != null
            ? ContainerLogoConfigManager.Instance.containerBubblePrefab
            : null;

        if (prefab != null)
        {
            _bubble = GameObject.Instantiate(prefab, _holder.transform, false);
            _bubble.name = "Bubble";

            var brt = _bubble.GetComponent<RectTransform>() ?? _bubble.AddComponent<RectTransform>();
            // 预制体不拉伸：保持自身尺寸与布局
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero; // 贴在 holder 底边中心

            // 同步 holder 尺寸 = 预制体尺寸
            SyncHolderSizeToBubble();
        }
        else
        {
            // 没有 prefab：兜底一个小图
            _bubble = new GameObject("Bubble_Fallback", typeof(RectTransform), typeof(Image));
            _bubble.transform.SetParent(_holder.transform, false);
            var brt = _bubble.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0f);
            brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(20, 20);

            var img = _bubble.GetComponent<Image>();
            img.sprite = fallbackSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            SyncHolderSizeToBubble();
        }

        // 预制体内所有 Image 不拦截事件
        foreach (var img in _bubble.GetComponentsInChildren<Image>(true))
            img.raycastTarget = false;

        // 初始定位 & 置顶
        RefreshLogoPosition();
        logoContainer.SetAsLastSibling();
    }

    private void SetHolderAlpha(float a)
    {
        if (_holder == null) return;
        var cg = _holder.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = a;
    }

    /// <summary>把点击转发到 holder 下方真实物体（避免遮挡交互）。</summary>
    private void ForwardClickEvent(PointerEventData data)
    {
        var mapContent = MapManager.Instance != null ? MapManager.Instance.mapContent : null;
        if (mapContent == null)
        {
            Debug.LogWarning("ForwardClickEvent: MapContent 未设置");
            return;
        }
        var raycaster = mapContent.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            Debug.LogWarning("ForwardClickEvent: MapContent 缺少 GraphicRaycaster");
            return;
        }

        var pointerData = new PointerEventData(EventSystem.current) { position = data.position };
        var results = new List<RaycastResult>();
        raycaster.Raycast(pointerData, results);

        foreach (var r in results)
        {
            // 跳过 holder 与其子节点
            if (_holder != null && (r.gameObject == _holder || r.gameObject.transform.IsChildOf(_holder.transform)))
                continue;

            ExecuteEvents.Execute(r.gameObject, data, ExecuteEvents.pointerClickHandler);
            break;
        }
    }

    /// <summary>
    /// 若物品位置变化：更新“顶部中点 + offset”。
    /// （初版等式：X=objectPos.x+0.5*width，Y=objectPos.y）
    /// </summary>
    public void RefreshLogoPosition()
    {
        if (_holder == null) return;

        var objectRT = GetComponent<RectTransform>();
        var holderRT = _holder.GetComponent<RectTransform>();
        if (objectRT == null || holderRT == null) return;

        Vector2 objectPos = objectRT.anchoredPosition;
        float objectWidth = objectRT.sizeDelta.x;

        // —— 回到初版的定位公式 —— //
        Vector2 topCenter = new Vector2(objectPos.x + objectWidth * 0.5f, objectPos.y);

        holderRT.anchoredPosition = topCenter + GetConfigOffset();
    }

    private Vector2 GetConfigOffset()
    {
        if (ContainerLogoConfigManager.Instance != null)
            return ContainerLogoConfigManager.Instance.offset;
        return offset;
    }

    /// <summary>取/建 LogoContainer（挂在 mapContent 下）。</summary>
    private Transform GetLogoContainer()
    {
        var mapContent = MapManager.Instance != null ? MapManager.Instance.mapContent : null;
        if (mapContent == null)
        {
            Debug.LogError("GetLogoContainer: MapManager.Instance.mapContent 未设置！");
            return transform;
        }
        Transform container = mapContent.Find("LogoContainer");
        if (container == null)
        {
            var go = new GameObject("LogoContainer", typeof(RectTransform));
            go.transform.SetParent(mapContent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            go.transform.SetAsLastSibling();
            container = go.transform;
        }
        else
        {
            container.SetAsLastSibling();
        }
        return container;
    }

    /// <summary>把 holder 尺寸同步为 bubble 的尺寸。</summary>
    private bool SyncHolderSizeToBubble()
    {
        if (_holder == null || _bubble == null) return false;
        var hrt = _holder.GetComponent<RectTransform>();
        var brt = _bubble.GetComponent<RectTransform>();
        if (hrt == null || brt == null) return false;
        hrt.sizeDelta = brt.sizeDelta;
        return true;
    }

    /// <summary>递归按名称查找 Image。</summary>
    private Image FindDeepChildImageByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return null;
    }

    /// <summary>没有 "icon" 时，尝试选一个最可能是图标的 Image。</summary>
    private Image PickBestImageForIcon(Transform root)
    {
        // 优先规则：名字包含 "icon"；否则随便取一个能看到的 Image
        Image best = null;
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            var n = img.gameObject.name.ToLowerInvariant();
            if (n.Contains("icon")) return img;
            if (best == null) best = img;
        }
        return best;
    }
}
