// 文件：SimulationAgentRenderer.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.EventSystems;

[System.Serializable]
public class AgentStyle
{
    public Color color = Color.white;
}

public class SimulationAgentRenderer : MonoBehaviour
{
    public static SimulationAgentRenderer Instance { get; private set; }

    [Header("格子设置")]
    public float gridSize = 32f;

    [Header("Agent 样式配置")]
    public List<AgentStyle> agentStyles = new List<AgentStyle>();
    public AgentStyle defaultAgentStyle;

    [Header("活动气泡")]
    public GameObject activityBubblePrefab;
    public Vector2 bubbleOffset = new Vector2(0, 6);
    public int defaultFontSize = 24;

    /* ★ 新增：跳转按钮 UI  */
    [Header("Agent 跳转按钮（可选）")]
    [Tooltip("放置列表用的父容器，如一个挂了 VerticalLayoutGroup 的 Panel")]
    public Transform jumpButtonsContainer;
    [Tooltip("按钮预制体。要求有子节点：Icon(Image) 和 Name(TMP_Text 或 Text)")]
    public GameObject jumpButtonPrefab;

    // ====== 物品变更气泡（预制体 + 参数） ======
    [Header("Item Change Bubble (Agent)")]
    [Tooltip("气泡预制体（内部需包含名为 \"icon\" 的 Image；现新增名为 \"label\" 的 TMP_Text）")]
    public GameObject itemChangeBubblePrefab;

    [Tooltip("相对 Agent 左侧的偏移（左负右正，单位：像素，左上坐标系）")]
    public Vector2 itemChangeOffset = new Vector2(-10f, 6f);

    [Tooltip("气泡淡入时长（秒）")]
    public float itemBubbleFadeInSeconds = 2f;

    [Tooltip("整体停留时长（秒）")]
    public float itemBubbleHoldSeconds = 1.0f;

    [Tooltip("气泡淡出时长（秒）")]
    public float itemBubbleFadeOutSeconds = 2f;

    /// <summary>把物品名/ID 转为对应 Sprite 的回调；若未赋值，将使用本类内置的解析（与容器气泡一致）。</summary>
    public Func<string, Sprite> bagItemToSprite;

    private readonly Dictionary<string, TMP_Text> agentBubbleTexts = new Dictionary<string, TMP_Text>();
    private Dictionary<string, Image> agentImages = new Dictionary<string, Image>();
    private Dictionary<string, AgentAnimationController> agentAnimControllers = new Dictionary<string, AgentAnimationController>();
    private Dictionary<string, RectTransform> agentRects = new Dictionary<string, RectTransform>();
    private Dictionary<string, RectTransform> agentBubbleMap = new Dictionary<string, RectTransform>();

    // —— 物品变更气泡的运行期缓存 —— //
    private readonly Dictionary<string, RectTransform> agentItemBubbleRT = new();
    private readonly Dictionary<string, CanvasGroup> agentItemBubbleCG = new();
    private readonly Dictionary<string, Image> agentItemIconImg = new();
    private readonly Dictionary<string, Coroutine> agentItemBubbleCo = new();
    // ★ 新增：label 缓存
    private readonly Dictionary<string, TMP_Text> agentItemLabel = new();

    private RectTransform containerRect;
    private RectTransform bubbleContainer;

    private string hoveredAgent = null;
    private string pinnedAgent = null;
    private Dictionary<string, SimulationAgent> currentStepData;

    private InformationPanelController infoPanel;

    public bool HasPinnedAgent => !string.IsNullOrEmpty(pinnedAgent);

    // ★ 既有：活动气泡总开关（默认开启）
    private bool activityBubblesOn = true;
    public bool AreActivityBubblesEnabled => activityBubblesOn;

    /// <summary>统一开关所有已创建的活动气泡（不销毁，仅隐藏/显示）</summary>
    public void SetActivityBubblesEnabled(bool on)
    {
        activityBubblesOn = on;
        if (agentBubbleMap != null)
        {
            foreach (var kv in agentBubbleMap)
            {
                if (kv.Value != null)
                    kv.Value.gameObject.SetActive(activityBubblesOn);
            }
        }
    }

    // ★ 新增：Agent 跳转按钮总开关（默认显示）
    private bool agentJumpButtonsOn = true;
    public bool AreAgentJumpButtonsEnabled => agentJumpButtonsOn;

    /// <summary>统一开关“Agent 跳转按钮列表”的父容器显示/隐藏。</summary>
    public void SetAgentJumpButtonsEnabled(bool on)
    {
        agentJumpButtonsOn = on;
        if (jumpButtonsContainer != null)
            jumpButtonsContainer.gameObject.SetActive(agentJumpButtonsOn);
    }

    // ====== BAG 调试：统一开关与前缀 ======
    [SerializeField] private bool debugBagLogs = true;
    private const string BAGDBG = "[BAGDBG]";

    public void ClearPinned()
    {
        pinnedAgent = null;
        foreach (var kvp in agentImages)
        {
            var helper = kvp.Value != null ? kvp.Value.GetComponent<AgentVisualHelper>() : null;
            if (helper != null && helper.outlineComponent != null)
                helper.outlineComponent.enabled = false;
        }
        UpdateInfoPanelDisplay();
    }

    /* ★ 对外：强制让 Agent 侧根据当前仲裁规则重刷一次信息面板 */
    public void ForceRefreshInfoPanel()
    {
        UpdateInfoPanelDisplay();
    }

    /* ★ 对外：固定并居中到指定 Agent（供按钮点击回调使用） */
    public void PinAndCenter(string agentName)
    {
        // 清除物体固定
        if (SimulationUIController.Instance != null)
            SimulationUIController.Instance.ClearPinned();

        // 固定 Agent
        pinnedAgent = agentName;
        UpdateInfoPanelDisplay();

        // 居中
        CenterViewOn(agentName);
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;

        containerRect = GetComponent<RectTransform>();
        if (containerRect == null)
            Debug.LogError("SimulationAgentRenderer: 无法获取 RectTransform！");

        GameObject obj = GameObject.Find("Canvas/MapDisplayPanel/MapContent/BubbleContainer");
        if (obj != null)
            bubbleContainer = obj.GetComponent<RectTransform>();
        else
            Debug.LogError("❌ 找不到 BubbleContainer，请检查路径！");

        GameObject panelObj = GameObject.Find("Canvas/InformationPanel");
        if (panelObj != null)
            infoPanel = panelObj.GetComponent<InformationPanelController>();
        else
            Debug.LogError("❌ 找不到 InformationPanel！");

        // 若外部未赋回调，则使用与容器气泡一致的默认解析（从 EditorItem.thumbnail 取）
        if (bagItemToSprite == null)
            bagItemToSprite = ResolveItemSpriteFromEditorThumbnails;

        if (debugBagLogs)
        {
            var tpm = FindObjectOfType<ToolPanelManager>();
            int panelCount = (tpm != null && tpm.availableItems != null) ? tpm.availableItems.Count : -1;
            int placedCount = (MapManager.Instance != null && MapManager.Instance.placedItems != null) ? MapManager.Instance.placedItems.Count : -1;
            Debug.Log($"{BAGDBG} Awake scene summary: ToolPanel.items={panelCount} PlacedItems={placedCount} prefab='{itemChangeBubblePrefab?.name}'");
        }
    }

    public void RenderAgents(Dictionary<string, SimulationAgent> stepData)
    {
        ClearAgents();
        agentBubbleTexts.Clear();
        agentBubbleMap.Clear();

        currentStepData = stepData;

        List<string> agentNames = stepData.Keys.ToList();
        agentNames.Sort();

        float factor = SimulationMapManager.Instance != null ? SimulationMapManager.Instance.backgroundScaleFactor : 1f;

        foreach (string agentName in agentNames)
        {
            SimulationAgent d = stepData[agentName];

            Vector2 rawPos = (d.curr_tile != null && d.curr_tile.Length >= 2)
                ? new Vector2(d.curr_tile[0] * gridSize, d.curr_tile[1] * gridSize)
                : Vector2.zero;
            Vector2 pos = new Vector2(rawPos.x * factor, -rawPos.y * factor);

            AgentStyle style = (agentStyles != null && agentStyles.Count > 0)
                ? agentStyles[Mathf.Min(agentNames.IndexOf(agentName), agentStyles.Count - 1)]
                : defaultAgentStyle;

            GameObject go = new GameObject($"Agent_{agentName}", typeof(Image));
            go.transform.SetParent(containerRect, false);

            Image img = go.GetComponent<Image>();
            img.color = style.color;

            RectTransform agentRT = img.rectTransform;
            agentRT.anchorMin = agentRT.anchorMax = new Vector2(0, 1);
            agentRT.pivot = new Vector2(0f, 1f);

            agentRT.sizeDelta = new Vector2(gridSize * factor, gridSize * factor);
            agentRT.anchoredPosition = pos;

            agentImages[agentName] = img;
            agentRects[agentName] = agentRT;

            AgentAnimationController anim = go.AddComponent<AgentAnimationController>();
            anim.spriteSheetName = !string.IsNullOrEmpty(d.walkingSpriteSheetName) ? d.walkingSpriteSheetName : agentName;
            agentAnimControllers[agentName] = anim;
            anim.UpdateAnimation(AgentAnimationController.Direction.Down, false, 0f);

            // 统一描边样式：黑色细描边
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 1f);
            outline.effectDistance = new Vector2(0.2f, 0.2f);
            outline.enabled = false;

            AgentVisualHelper helper = go.AddComponent<AgentVisualHelper>();
            helper.outlineComponent = outline;

            // —— 活动气泡 —— //
            if (activityBubblePrefab != null)
            {
                GameObject bubbleGO = Instantiate(activityBubblePrefab, bubbleContainer, false);
                RectTransform bubbleRT = bubbleGO.GetComponent<RectTransform>();
                bubbleRT.anchorMin = bubbleRT.anchorMax = new Vector2(0, 1);
                bubbleRT.pivot = new Vector2(0.5f, 1f);
                bubbleRT.anchoredPosition = pos + bubbleOffset;

                TMP_Text tmp = bubbleRT.GetComponentInChildren<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = d.short_activity ?? string.Empty;
                    if (!tmp.enableAutoSizing) tmp.fontSize = defaultFontSize;
                    agentBubbleTexts[agentName] = tmp;
                }

                agentBubbleMap[agentName] = bubbleRT;
                AddHoverHandler(go, bubbleRT, agentName);
                bubbleRT.gameObject.SetActive(activityBubblesOn); // ★ 对齐总开关
            }

            // —— 物品变更气泡（实例化但默认透明） —— //
            if (itemChangeBubblePrefab != null && !agentItemBubbleRT.ContainsKey(agentName))
            {
                GameObject icGO = Instantiate(itemChangeBubblePrefab, bubbleContainer, false);
                icGO.name = $"ItemChangeBubble_{agentName}";

                var icRT = icGO.GetComponent<RectTransform>();
                if (icRT == null) icRT = icGO.AddComponent<RectTransform>();
                icRT.anchorMin = icRT.anchorMax = new Vector2(0f, 1f);
                icRT.pivot = new Vector2(0.5f, 1f);
                icRT.anchoredPosition = pos + itemChangeOffset;

                var cg = icGO.GetComponent<CanvasGroup>();
                if (cg == null) cg = icGO.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;

                Image icon = FindImageByNameOrBest(icGO.transform, "icon");
                if (icon != null)
                {
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                }
                else
                {
                    if (debugBagLogs) Debug.LogWarning($"{BAGDBG} Prefab '{itemChangeBubblePrefab?.name}' has NO Image named 'icon' (will try best Image).");
                }

                // ★ 新增：label
                TMP_Text label = null;
                var labelTr = icGO.transform.Find("label");
                if (labelTr != null) label = labelTr.GetComponent<TMP_Text>();
                if (label == null)
                {
                    if (debugBagLogs) Debug.LogWarning($"{BAGDBG} Prefab '{itemChangeBubblePrefab?.name}' has NO TMP_Text named 'label'.");
                }

                agentItemBubbleRT[agentName] = icRT;
                agentItemBubbleCG[agentName] = cg;
                agentItemIconImg[agentName] = icon;
                agentItemLabel[agentName] = label;
            }

        }

        if (bubbleContainer != null)
            bubbleContainer.SetAsLastSibling();

        /* ★ 重建跳转按钮列表（仅当引用已赋值时） */
        RebuildJumpButtons(agentNames);

        UpdateInfoPanelDisplay();
    }

    private void RebuildJumpButtons(List<string> agentNames)
    {
        if (jumpButtonPrefab == null || jumpButtonsContainer == null) return;

        // 清空旧按钮
        for (int i = jumpButtonsContainer.childCount - 1; i >= 0; i--)
            Destroy(jumpButtonsContainer.GetChild(i).gameObject);

        foreach (var name in agentNames)
        {
            GameObject go = Instantiate(jumpButtonPrefab, jumpButtonsContainer, false);
            go.name = "JumpBtn_" + name;

            // 设置图标（优先用当前渲染的 idle 帧）
            var iconTr = go.transform.Find("Icon");
            if (iconTr != null)
            {
                var img = iconTr.GetComponent<Image>();
                if (img != null)
                {
                    if (agentImages.TryGetValue(name, out var agentImg) && agentImg != null && agentImg.sprite != null)
                    {
                        img.sprite = agentImg.sprite;
                        img.preserveAspect = true;
                    }
                }
            }

            // 设置名字（★ 使用首字母缩写）
            var nameTr = go.transform.Find("Name");
            if (nameTr != null)
            {
                string shortName = GetInitials(name);

                var tmp = nameTr.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = shortName;
                else
                {
                    var txt = nameTr.GetComponent<Text>();
                    if (txt != null) txt.text = shortName;
                }
            }

            // 绑定点击
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                string agentNameCaptured = name; // 闭包捕获
                btn.onClick.AddListener(() => PinAndCenter(agentNameCaptured));
            }
        }

        // ★ 与总开关同步显隐（不销毁节点）
        if (jumpButtonsContainer != null)
            jumpButtonsContainer.gameObject.SetActive(agentJumpButtonsOn);
    }

    private void AddHoverHandler(GameObject agentGO, RectTransform bubbleRT, string agentName)
    {
        void AddTrigger(GameObject targetGO)
        {
            EventTrigger trigger = targetGO.GetComponent<EventTrigger>() ?? targetGO.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((_) =>
            {
                hoveredAgent = agentName;
                bubbleRT.SetAsLastSibling();
                UpdateInfoPanelDisplay();
            });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener((_) =>
            {
                hoveredAgent = null;
                UpdateInfoPanelDisplay();
            });
            trigger.triggers.Add(exit);

            // —— 仅左键点击才切换固定
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener((dataObj) =>
            {
                var ped = dataObj as PointerEventData;
                if (ped == null || ped.button != PointerEventData.InputButton.Left) return;

                if (pinnedAgent == agentName) pinnedAgent = null;
                else
                {
                    // 固定 Agent 前，清掉物体的固定
                    if (SimulationUIController.Instance != null)
                        SimulationUIController.Instance.ClearPinned();
                    pinnedAgent = agentName;
                }
                UpdateInfoPanelDisplay();
            });
            trigger.triggers.Add(click);
        }

        AddTrigger(agentGO);
        AddTrigger(bubbleRT.gameObject);
    }

    private void UpdateInfoPanelDisplay()
    {
        bool objectPinned = SimulationUIController.Instance != null && SimulationUIController.Instance.HasPinnedObject;
        string target = pinnedAgent ?? (objectPinned ? null : hoveredAgent);

        if (infoPanel != null)
        {
            if (!string.IsNullOrEmpty(target) && currentStepData != null && currentStepData.ContainsKey(target))
            {
                SimulationAgent agent = currentStepData[target];
                Vector2Int tile = (agent.curr_tile != null && agent.curr_tile.Length >= 2)
                                  ? new Vector2Int(agent.curr_tile[0], agent.curr_tile[1])
                                  : Vector2Int.zero;
                infoPanel.SetAgentInfo(agent, tile);
            }
            else
            {
                if (!objectPinned)
                    infoPanel.Clear();
            }
        }

        // 统一刷新描边
        foreach (var kvp in agentImages)
        {
            string name = kvp.Key;
            var helper = kvp.Value != null ? kvp.Value.GetComponent<AgentVisualHelper>() : null;
            if (helper != null && helper.outlineComponent != null)
                helper.outlineComponent.enabled = (name == pinnedAgent);
        }
    }

    public void Update()
    {
        UpdateBubblePositions();
        UpdateItemChangeBubblePositions(); // 物品变更气泡跟随
    }

    private void UpdateBubblePositions()
    {
        foreach (var kvp in agentBubbleMap)
        {
            string agentName = kvp.Key;
            if (agentRects.TryGetValue(agentName, out var agentRT))
                kvp.Value.anchoredPosition = agentRT.anchoredPosition + bubbleOffset;
        }
    }

    // 逐帧让“物品变更气泡”跟随
    private void UpdateItemChangeBubblePositions()
    {
        foreach (var kv in agentItemBubbleRT)
        {
            string name = kv.Key;
            if (agentRects.TryGetValue(name, out var agentRT) && kv.Value != null)
            {
                kv.Value.anchoredPosition = agentRT.anchoredPosition + itemChangeOffset;
            }
        }
    }

    public void UpdateAgentsPositions(Dictionary<string, SimulationAgent> stepData)
    {
        float factor = SimulationMapManager.Instance != null ? SimulationMapManager.Instance.backgroundScaleFactor : 1f;
        currentStepData = stepData;
        foreach (var kvp in stepData)
        {
            string agentName = kvp.Key;
            SimulationAgent data = kvp.Value;

            Vector2 rawPos = (data.curr_tile != null && data.curr_tile.Length >= 2)
                ? new Vector2(data.curr_tile[0] * gridSize, data.curr_tile[1] * gridSize)
                : Vector2.zero;
            Vector2 pos = new Vector2(rawPos.x * factor, -rawPos.y * factor);
            UpdateAgentPosition(agentName, pos);
            if (agentBubbleTexts.TryGetValue(agentName, out var txt))
                txt.text = data.short_activity ?? string.Empty;
        }
        UpdateInfoPanelDisplay();
    }

    public void ClearAgents()
    {
        foreach (var kvp in agentImages)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        foreach (var kvp in agentBubbleMap)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        foreach (var kv in agentItemBubbleRT)
            if (kv.Value != null) Destroy(kv.Value.gameObject);

        agentImages.Clear();
        agentAnimControllers.Clear();
        agentBubbleTexts.Clear();
        agentBubbleMap.Clear();
        agentRects.Clear();

        agentItemBubbleRT.Clear();
        agentItemBubbleCG.Clear();
        agentItemIconImg.Clear();
        agentItemBubbleCo.Clear();
        agentItemLabel.Clear();

        hoveredAgent = null;
        pinnedAgent = null;
    }

    public void UpdateAgentPosition(string agentName, Vector2 pos)
    {
        if (agentImages.ContainsKey(agentName))
            agentImages[agentName].rectTransform.anchoredPosition = pos;
    }

    public Vector2 GetAgentPosition(string agentName)
    {
        if (agentImages.ContainsKey(agentName))
            return agentImages[agentName].rectTransform.anchoredPosition;
        return Vector2.zero;
    }

    public AgentAnimationController GetAnimationController(string agentName)
    {
        if (agentAnimControllers.ContainsKey(agentName))
            return agentAnimControllers[agentName];
        return null;
    }

    private GameObject CreateOutlineBorder(GameObject parentGO, float size)
    {
        GameObject borderGO = new GameObject("OutlineBorder", typeof(Image));
        borderGO.transform.SetParent(parentGO.transform, false);

        RectTransform rt = borderGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size + 2, size + 2);

        Image img = borderGO.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 1f);
        img.raycastTarget = false;

        return borderGO;
    }

    /* ★ 居中到指定 Agent（瞬移，最小实现） */
    private void CenterViewOn(string agentName)
    {
        if (!agentRects.TryGetValue(agentName, out var agentRT)) return;

        RectTransform content = SimulationMapManager.Instance?.mapContent;
        RectTransform display = SimulationMapManager.Instance?.mapDisplayArea;
        if (content == null || display == null) return;

        // agent 的锚点位置（以 mapContent 左上为原点，Y 向下为负）
        Vector2 agentPos = agentRT.anchoredPosition;

        // 当前缩放（假设 x=y）
        float s = content.localScale.x;

        // ✅ 绝对纵向偏移（单位：display 本地像素）
        // 正值 = 向上偏移；负值 = 向下偏移
        const float CENTER_Y_OFFSET = 60f; // ← 按需改

        // 目标居中点（以 display 左上为原点）
        Vector2 center = new Vector2(
            display.rect.width / 2f,
            -display.rect.height / 2f + CENTER_Y_OFFSET
        );

        // 让 agent 的（content 内）位置 * 缩放 + content 的偏移 = center
        Vector2 newAnchored = center - agentPos * s;

        // 同时设置 anchoredPosition / localPosition，兼容你现有的拖拽脚本
        content.anchoredPosition = newAnchored;
        content.localPosition = new Vector3(newAnchored.x, newAnchored.y, content.localPosition.z);
    }

    /* ★ 名字转首字母缩写：如 "Sam Smith" -> "S.M"；"Madonna" -> "M."；"Jean-Luc Picard" -> "J.L.P" */
    private static string GetInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return fullName;

        var tokens = fullName
            .Split(new[] { ' ', '\t', '-', '_' }, System.StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var initials = new List<string>();
        foreach (var t in tokens)
        {
            var ch = t.FirstOrDefault(c => char.IsLetterOrDigit(c));
            if (ch != default(char))
                initials.Add(char.ToUpperInvariant(ch).ToString());
        }

        if (initials.Count >= 2)
            return string.Join(".", initials); // S.M
        if (initials.Count == 1)
            return initials[0] + ".";         // S.
        return fullName;
    }

    // ====== 对外 API —— 播放一次“物品变更气泡”动画（由控制器触发） ======
    /// <summary>
    /// 统一流程：整体（气泡+icon+label）【淡入】→【停留】→【淡出】
    /// </summary>
    public void PlayBagChangeBubble(string agentName, string changedItemName, bool isAdd)
    {
        if (debugBagLogs) Debug.Log($"{BAGDBG} CHANGE agent='{agentName}' item='{changedItemName}' isAdd={isAdd}");

        if (!agentItemBubbleRT.ContainsKey(agentName)) return;

        var cg = agentItemBubbleCG[agentName];
        var icon = agentItemIconImg.ContainsKey(agentName) ? agentItemIconImg[agentName] : null;
        var label = agentItemLabel.ContainsKey(agentName) ? agentItemLabel[agentName] : null;

        // 解析 sprite（bagItemToSprite 可被 RuntimeItemCatalogLoader 接管）
        Sprite sp = (bagItemToSprite != null && !string.IsNullOrEmpty(changedItemName))
                    ? bagItemToSprite(changedItemName) : null;

        if (icon != null) icon.sprite = sp;
        if (label != null) label.text = isAdd ? "+" : "-";

        if (debugBagLogs)
            Debug.Log($"{BAGDBG} RESOLVE result={(sp != null ? "OK" : "NULL")} iconImage={(icon != null ? "OK" : "NULL")} label={(label != null ? label.text : "NULL")}");

        // 停掉上一次的动画，重新播放
        if (agentItemBubbleCo.TryGetValue(agentName, out var co) && co != null)
            StopCoroutine(co);

        agentItemBubbleCo[agentName] = StartCoroutine(CoPlayUnifiedBubble(cg));
    }

    // —— 统一动画实现：整体淡入 → 停留 → 整体淡出 —— //
    private System.Collections.IEnumerator CoPlayUnifiedBubble(CanvasGroup bubble)
    {
        // 安全起点
        bubble.alpha = 0f;

        // 淡入
        yield return Fade01(bubble, itemBubbleFadeInSeconds);

        // 停留
        if (itemBubbleHoldSeconds > 0f)
            yield return new WaitForSeconds(itemBubbleHoldSeconds);

        // 淡出
        yield return Fade10(bubble, itemBubbleFadeOutSeconds);

        bubble.alpha = 0f;
    }

    // —— 淡入/淡出工具（CanvasGroup / Image） —— //
    private System.Collections.IEnumerator Fade01(CanvasGroup cg, float dur)
    {
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Clamp01(t / dur); yield return null; }
        cg.alpha = 1f;
    }
    private System.Collections.IEnumerator Fade10(CanvasGroup cg, float dur)
    {
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; cg.alpha = 1f - Mathf.Clamp01(t / dur); yield return null; }
        cg.alpha = 0f;
    }
    private System.Collections.IEnumerator Fade01(Image img, float dur)
    {
        float t = 0f; var c = img.color;
        while (t < dur) { t += Time.deltaTime; c.a = Mathf.Clamp01(t / dur); img.color = c; yield return null; }
        c.a = 1f; img.color = c;
    }
    private System.Collections.IEnumerator Fade10(Image img, float dur)
    {
        float t = 0f; var c = img.color;
        while (t < dur) { t += Time.deltaTime; c.a = 1f - Mathf.Clamp01(t / dur); img.color = c; yield return null; }
        c.a = 0f; img.color = c;
    }

    // —— 与“容器气泡”一致：从 EditorItem.thumbnail 获取物品图标（含调试日志） —— //
    private Sprite ResolveItemSpriteFromEditorThumbnails(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;

        string Key(string s)
        {
            s = s.Trim().ToLowerInvariant();
            s = s.Replace('_', ' ');
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s;
        }

        string key = Key(rawName);
        if (debugBagLogs) Debug.Log($"{BAGDBG} LOOKUP key='{key}'");

        // A) 优先：MapManager.placedItems（simulation 放置后包含 thumbnail）
        var mm = MapManager.Instance;
        if (mm != null && mm.placedItems != null)
        {
            // 建一份 name->sprite 的查找表
            var dict = new Dictionary<string, Sprite>();
            foreach (var pi in mm.placedItems)
            {
                if (pi.item != null && pi.item.thumbnail != null)
                {
                    var k = Key(pi.item.itemName ?? "");
                    if (!dict.ContainsKey(k)) dict[k] = pi.item.thumbnail;
                }
            }

            if (debugBagLogs) Debug.Log($"{BAGDBG} PLACED count={dict.Count}");

            // 严格匹配
            if (dict.TryGetValue(key, out var spPlaced))
            {
                if (debugBagLogs) Debug.Log($"{BAGDBG} HIT placedItems key='{key}'");
                return spPlaced;
            }

            // 常见单复数容错
            static IEnumerable<string> Variants(string k)
            {
                yield return k;
                if (k.EndsWith("ies")) yield return k[..^3] + "y";
                if (k.EndsWith("es")) yield return k[..^2];
                if (k.EndsWith("s")) yield return k[..^1];
            }
            foreach (var v in Variants(key))
            {
                if (dict.TryGetValue(v, out var spVar))
                {
                    if (debugBagLogs) Debug.Log($"{BAGDBG} HIT placedItems variant='{v}' for key='{key}'");
                    return spVar;
                }
            }

            // 容错：包含/前缀
            foreach (var kv in dict)
            {
                if (kv.Key.Contains(key) || key.Contains(kv.Key) || kv.Key.StartsWith(key))
                {
                    if (debugBagLogs) Debug.Log($"{BAGDBG} HIT placedItems fuzzy='{kv.Key}' for key='{key}'");
                    return kv.Value;
                }
            }
        }
        else
        {
            if (debugBagLogs) Debug.LogWarning($"{BAGDBG} MapManager or placedItems is NULL in this scene.");
        }

        // B) ToolPanelManager.availableItems（DirectoryLoader 读取 texture.png 后填充）
        var tpm = FindObjectOfType<ToolPanelManager>();
        if (tpm != null && tpm.availableItems != null)
        {
            if (debugBagLogs) Debug.Log($"{BAGDBG} TOOLPANEL count={tpm.availableItems.Count}");

            string K(string s) => Key(s ?? "");
            var strict = tpm.availableItems.FirstOrDefault(i => i != null && i.thumbnail != null && K(i.itemName) == key);
            if (strict != null)
            {
                if (debugBagLogs) Debug.Log($"{BAGDBG} HIT toolPanel item='{strict.itemName}'");
                return strict.thumbnail;
            }

            // 变体
            IEnumerable<string> Variants2(string k)
            {
                yield return k;
                if (k.EndsWith("ies")) yield return k[..^3] + "y";
                if (k.EndsWith("es")) yield return k[..^2];
                if (k.EndsWith("s")) yield return k[..^1];
            }
            foreach (var v in Variants2(key))
            {
                var it = tpm.availableItems.FirstOrDefault(i => i != null && i.thumbnail != null && K(i.itemName) == v);
                if (it != null)
                {
                    if (debugBagLogs) Debug.Log($"{BAGDBG} HIT toolPanel variant='{v}' item='{it.itemName}' for key='{key}'");
                    return it.thumbnail;
                }
            }

            // 包含/前缀
            var fuzzy = tpm.availableItems.FirstOrDefault(i =>
                i != null && i.thumbnail != null &&
                (K(i.itemName).Contains(key) || key.Contains(K(i.itemName)) || K(i.itemName).StartsWith(key)));
            if (fuzzy != null)
            {
                if (debugBagLogs) Debug.Log($"{BAGDBG} HIT toolPanel fuzzy='{fuzzy.itemName}' for key='{key}'");
                return fuzzy.thumbnail;
            }
        }
        else
        {
            if (debugBagLogs) Debug.LogWarning($"{BAGDBG} ToolPanelManager/availableItems not present. (Simulation scene may not run DirectoryLoader)");
        }

        // C) 全部 miss —— 打印候选，帮助对齐命名
        if (debugBagLogs)
        {
            var namesPlaced = (mm != null && mm.placedItems != null)
                ? string.Join(", ", mm.placedItems.Where(p => p.item != null).Select(p => p.item.itemName).Distinct().Take(30))
                : "NONE";
            var namesPanel = (tpm != null && tpm.availableItems != null)
                ? string.Join(", ", tpm.availableItems.Where(i => i != null).Select(i => i.itemName).Take(30))
                : "NONE";
            Debug.LogWarning($"{BAGDBG} MISS key='{key}'. placedItems(names sample)=[{namesPlaced}] panel(names sample)=[{namesPanel}]");
        }

        return null;
    }

    // —— Editor 的“icon 命名约定”对齐的查找函数 —— //
    private Image FindImageByNameOrBest(Transform root, string name)
    {
        // 1) 精确名字匹配
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) { var img = t.GetComponent<Image>(); if (img != null) return img; }

        // 2) 名字包含“icon”优先；否则取第一个可见 Image
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
