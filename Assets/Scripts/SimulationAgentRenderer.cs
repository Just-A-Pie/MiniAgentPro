// 文件: SimulationAgentRenderer.cs

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
    [Header("格子设置")]
    public float gridSize = 32f;

    [Header("Agent 样式配置")]
    [Tooltip("配置一组 Agent 颜色，按照字母顺序分配给各 agent。如果 agent 数量超过配置数，则后续 agent 使用第一个颜色作为默认颜色。")]
    public List<AgentStyle> agentStyles = new List<AgentStyle>();

    public AgentStyle defaultAgentStyle;

    public GameObject activityBubblePrefab;
    public Vector2 bubbleOffset = new Vector2(0, 6);
    public int defaultFontSize = 24;

    private readonly Dictionary<string, TMP_Text> agentBubbleTexts = new Dictionary<string, TMP_Text>();
    private Dictionary<string, Image> agentImages = new Dictionary<string, Image>();
    private Dictionary<string, AgentAnimationController> agentAnimControllers = new Dictionary<string, AgentAnimationController>();
    private Dictionary<string, RectTransform> agentRects = new Dictionary<string, RectTransform>();
    private Dictionary<string, RectTransform> agentBubbleMap = new Dictionary<string, RectTransform>();

    private RectTransform containerRect;
    private RectTransform bubbleContainer;

    private string hoveredAgent = null;
    private string pinnedAgent = null;
    private Dictionary<string, SimulationAgent> currentStepData;

    private InformationPanelController infoPanel;

    private void Awake()
    {
        containerRect = GetComponent<RectTransform>();

        if (containerRect == null)
        {
            Debug.LogError("SimulationAgentRenderer: 无法获取 RectTransform！");
        }

        GameObject obj = GameObject.Find("Canvas/MapDisplayPanel/MapContent/BubbleContainer");
        if (obj != null)
        {
            bubbleContainer = obj.GetComponent<RectTransform>();
            Debug.Log("✅ BubbleContainer 成功绑定: " + bubbleContainer.name);
        }
        else
        {
            Debug.LogError("❌ 找不到 BubbleContainer，请检查路径！");
        }

        GameObject panelObj = GameObject.Find("Canvas/InformationPanel");
        if (panelObj != null)
        {
            infoPanel = panelObj.GetComponent<InformationPanelController>();
        }
        else
        {
            Debug.LogError("❌ 找不到 InformationPanel！");
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

            Vector2 rawPos = (d.curr_tile != null && d.curr_tile.Length >= 2) ? new Vector2(d.curr_tile[0] * gridSize, d.curr_tile[1] * gridSize) : Vector2.zero;
            Vector2 pos = new Vector2(rawPos.x * factor, -rawPos.y * factor);

            AgentStyle style = (agentStyles != null && agentStyles.Count > 0) ? agentStyles[Mathf.Min(agentNames.IndexOf(agentName), agentStyles.Count - 1)] : defaultAgentStyle;

            GameObject go = new GameObject($"Agent_{agentName}", typeof(Image));
            go.transform.SetParent(containerRect, false);

            Image img = go.GetComponent<Image>();
            img.color = style.color;

            RectTransform agentRT = img.rectTransform;
            agentRT.anchorMin = agentRT.anchorMax = new Vector2(0, 1);
            agentRT.pivot = new Vector2(0.5f, 0.5f);
            agentRT.sizeDelta = new Vector2(gridSize * factor, gridSize * factor);
            agentRT.anchoredPosition = pos;

            agentImages[agentName] = img;
            agentRects[agentName] = agentRT;

            AgentAnimationController anim = go.AddComponent<AgentAnimationController>();
            anim.spriteSheetName = !string.IsNullOrEmpty(d.walkingSpriteSheetName) ? d.walkingSpriteSheetName : agentName;
            agentAnimControllers[agentName] = anim;
            anim.UpdateAnimation(AgentAnimationController.Direction.Down, false, 0f);

            //  添加紫色描边
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 1f); // 紫色
            outline.effectDistance = new Vector2(0.2f, 0.2f);
            outline.enabled = false;

            //  记录引用
            AgentVisualHelper helper = go.AddComponent<AgentVisualHelper>();
            helper.outlineComponent = outline;


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
            }
        }

        if (bubbleContainer != null)
        {
            bubbleContainer.SetAsLastSibling();
        }

        UpdateInfoPanelDisplay();
    }

    private void AddHoverHandler(GameObject agentGO, RectTransform bubbleRT, string agentName)
    {
        void AddTrigger(GameObject targetGO)
        {
            EventTrigger trigger = targetGO.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = targetGO.AddComponent<EventTrigger>();

            // Hover
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) =>
            {
                hoveredAgent = agentName;
                bubbleRT.SetAsLastSibling();
                UpdateInfoPanelDisplay();
            });
            trigger.triggers.Add(enter);

            // Exit
            EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener((data) =>
            {
                hoveredAgent = null;
                UpdateInfoPanelDisplay();
            });
            trigger.triggers.Add(exit);

            // Click
            EventTrigger.Entry click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener((data) =>
            {
                if (pinnedAgent == agentName)
                    pinnedAgent = null;
                else
                    pinnedAgent = agentName;

                UpdateInfoPanelDisplay();
            });
            trigger.triggers.Add(click);
        }

        AddTrigger(agentGO);
        AddTrigger(bubbleRT.gameObject);
    }

    private void UpdateInfoPanelDisplay()
    {
        if (infoPanel == null || currentStepData == null)
            return;

        string target = hoveredAgent ?? pinnedAgent;
        if (string.IsNullOrEmpty(target) || !currentStepData.ContainsKey(target))
        {
            infoPanel.Clear();
        }
        else
        {
            SimulationAgent agent = currentStepData[target];
            Vector2Int tile = (agent.curr_tile != null && agent.curr_tile.Length >= 2)
                            ? new Vector2Int(agent.curr_tile[0], agent.curr_tile[1])
                            : Vector2Int.zero;

            infoPanel.SetAgentInfo(agent, tile);
        }

        // ✅ 最后刷新描边显示
        foreach (var kvp in agentImages)
        {
            string name = kvp.Key;
            GameObject go = kvp.Value.gameObject;
            AgentVisualHelper helper = go.GetComponent<AgentVisualHelper>();
            if (helper != null && helper.outlineComponent != null)
            {
                helper.outlineComponent.enabled = (name == pinnedAgent);
            }
        }
    }


    public void Update()
    {
        UpdateBubblePositions();
    }

    private void UpdateBubblePositions()
    {
        foreach (var kvp in agentBubbleMap)
        {
            string agentName = kvp.Key;
            if (agentRects.TryGetValue(agentName, out var agentRT))
            {
                kvp.Value.anchoredPosition = agentRT.anchoredPosition + bubbleOffset;
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

            Vector2 rawPos = (data.curr_tile != null && data.curr_tile.Length >= 2) ? new Vector2(data.curr_tile[0] * gridSize, data.curr_tile[1] * gridSize) : Vector2.zero;
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
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);

        foreach (var kvp in agentBubbleMap)
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);

        agentImages.Clear();
        agentAnimControllers.Clear();
        agentBubbleTexts.Clear();
        agentBubbleMap.Clear();
        agentRects.Clear();
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
        rt.sizeDelta = new Vector2(size + 2, size + 2); // 多出 1px 描边

        Image img = borderGO.GetComponent<Image>();
        img.color = new Color(0.6f, 0f, 1f, 1f); // 紫色
        img.raycastTarget = false; // 不阻挡事件

        return borderGO;
    }
}
