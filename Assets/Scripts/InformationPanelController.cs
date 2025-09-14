// 文件：InformationPanelController.cs  ★整份替换
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class InformationPanelController : MonoBehaviour
{
    /*────────── 顶级父物体 ─────────*/
    [Header("Parents")]
    public GameObject agentInfoGroup;     // = AgentInformation
    public GameObject otherInfoGroup;     // 现有 Object/Building 区

    /*────────── Agent 子区块 ─────────*/
    [Header("Agent Sub-Groups")]
    public GameObject overviewGroup;      // ← 新增
    public GameObject historyGroup;       // ← 新增

    [Header("Agent Overview Fields")]
    public TMP_Text nameText;
    public TMP_Text positionText;
    public TMP_Text locationText;
    public TMP_Text activityText;
    public TMP_Text bagText;
    public Image agentIcon;

    [Header("History UI")]
    public Button toggleHistoryButton;  // Header 里的按钮
    public TMP_Text historyText;          // ScrollView/HistoryText

    /*────────── Other 信息 ─────────*/
    [Header("Other Fields")]
    public TMP_Text otherNameText;
    public TMP_Text otherTypeText;
    public TMP_Text otherPositionText;
    public TMP_Text otherAttributesText;
    public Image otherIcon;

    /*────────── 私有状态 ─────────*/
    private bool showingHistory = false;
    private string currentAgentName;

    /*──────────────────────────────*/

    private void Awake()
    {
        if (toggleHistoryButton != null)
            toggleHistoryButton.onClick.AddListener(OnToggleHistoryClicked);
    }

    /*=====  Agent 概览  =====*/
    public void SetAgentInfo(SimulationAgent agent, Vector2Int tile)
    {
        // 若仍是同一人，则保持当前视图；否则回到概览
        bool agentChanged = currentAgentName != agent.name;
        currentAgentName = agent.name;
        if (agentChanged) showingHistory = false;

        agentInfoGroup.SetActive(true);
        otherInfoGroup.SetActive(false);

        overviewGroup.SetActive(!showingHistory);
        historyGroup.SetActive(showingHistory);

        /*── 概览字段仍照常更新 ─────────────────────*/
        nameText.text = agent.name;
        positionText.text = $"{tile.x}, {tile.y}";
        locationText.text = agent.location ?? "";
        activityText.text = agent.activity ?? "";

        // 直接全量显示（跳过 null/空白），并在 bag 为空时显示占位符
        if (agent.bag == null || agent.bag.Length == 0)
        {
            bagText.text = "—";  // 也可以用 "(empty)" 自行决定
        }
        else
        {
            // 去掉空字符串与前后空格，保证显示干净
            var cleaned = agent.bag
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim());
            bagText.text = string.Join(", ", cleaned);
        }


        string baseName = agent.walkingSpriteSheetName ?? agent.name;
        agentIcon.sprite = TryLoadFirstFrameSprite(baseName);

        /*── 历史文本刷新 ───────────────────────────*/
        RefreshHistoryText();

        /*── 切换按钮文字 ───────────────────────────*/
        string btnLabel = showingHistory ? "Back" : "History";
        toggleHistoryButton.GetComponentInChildren<TMP_Text>().text = btnLabel;
    }

    /*=====  History 显示  =====*/
    private void OnToggleHistoryClicked()
    {
        if (string.IsNullOrEmpty(currentAgentName)) return;

        showingHistory = !showingHistory;
        overviewGroup.SetActive(!showingHistory);
        historyGroup.SetActive(showingHistory);

        if (showingHistory)
        {
            RefreshHistoryText();
            toggleHistoryButton.GetComponentInChildren<TMP_Text>().text = "Back";
        }
        else
            toggleHistoryButton.GetComponentInChildren<TMP_Text>().text = "History";
    }

    private void RefreshHistoryText()
    {
        if (historyText == null || AgentHistoryManager.Instance == null) return;
        historyText.text = AgentHistoryManager.Instance.GetHistoryText(currentAgentName);
    }

    /*=====  Object / Building  =====*/
    public void SetOtherInfo(MapManager.PlacedItem placed)
    {
        currentAgentName = null;
        showingHistory = false;

        otherInfoGroup.SetActive(true);
        agentInfoGroup.SetActive(false);

        otherNameText.text = placed.item.itemName;
        otherTypeText.text = placed.category.ToString();
        otherPositionText.text = $"({placed.gridX}, {placed.gridY})";

        // 原来：直接把所有属性拼成 "key: value"
        if (placed.item.attributes != null && placed.item.attributes.Count > 0)
            otherAttributesText.text = string.Join(
                ", ",
                System.Linq.Enumerable.Select(placed.item.attributes,
                    kv => $"{kv.Key}: {kv.Value}"));
        else
            otherAttributesText.text = "";



        otherIcon.sprite = placed.item.thumbnail;
        if (placed.item.thumbnail != null)
        {
            float w = placed.item.thumbnail.rect.width;
            float h = placed.item.thumbnail.rect.height;
            float s = Mathf.Min(75f / w, 75f / h, 1f);
            otherIcon.rectTransform.sizeDelta = new Vector2(w * s, h * s);
        }
    }

    /*=====  清空  =====*/
    public void Clear()
    {
        currentAgentName = null;
        showingHistory = false;

        agentInfoGroup.SetActive(false);
        otherInfoGroup.SetActive(false);

        // 概览文本
        nameText.text = positionText.text = locationText.text =
            activityText.text = bagText.text = "";
        agentIcon.sprite = null;

        // Other
        otherNameText.text = otherTypeText.text = otherPositionText.text =
            otherAttributesText.text = "";
        otherIcon.sprite = null;

        historyText.text = "";
    }

    /*=====  工具  =====*/
    private Sprite TryLoadFirstFrameSprite(string baseName)
    {
        string[] tryNames = { baseName,
            baseName.Contains("_") ? baseName.Replace("_"," ") :
                                     baseName.Replace(" ","_") };
        foreach (var n in tryNames)
        {
            Sprite[] sps = Resources.LoadAll<Sprite>(n);
            if (sps != null && sps.Length > 0)
            {
                foreach (var s in sps)
                    if (s.name.EndsWith("_1")) return s;
                return sps[0];
            }
        }
        return null;
    }
}

