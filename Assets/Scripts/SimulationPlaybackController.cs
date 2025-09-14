// 文件：SimulationPlaybackController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;

public class SimulationPlaybackController : MonoBehaviour
{
    /*──────────────────────── Inspector 字段 ─────────────────────*/
    [Header("播放控制 UI")]
    public Button playButton;
    public Button pauseButton;
    public Button jumpButton;
    public TMP_Dropdown speedDropdown;
    public TMP_InputField jumpStepInput;
    public Button toggleBubbleButton;   // ★ 已有：显示/隐藏“活动气泡”
    public Button toggleJumpButtonsButton; // ★ 新增：显示/隐藏“Agent 跳转按钮列表”

    [Header("Agent 渲染")]
    public SimulationAgentRenderer agentRenderer;

    [Header("播放参数")]
    public float baseStepDuration = 1f;

    [Header("仿真数据路径")]
    public string simFolderPath;

    [Header("异步/同步加载组件（二选一即可）")]
    public LoadingPanelController loadingPanel;
    public AsyncSimulationDataLoader dataLoader;          // 可不拖拽
    public SimulationDataLoaderSync dataLoaderSync;       // 可不拖拽

    [Header("Step Info UI")]
    public TMP_Text stepInfoText;
    public TMP_Text stepTimeText;

    /*────────── 昼夜调色 ─────────*/
    [Header("昼夜调色")]
    public Image mapImage;
    public Color nightColor = new Color32(63, 62, 140, 255);
    public Color dayColor = Color.white;

    /*──────────────────── 运行期变量 ────────────────────*/
    private List<Dictionary<string, SimulationAgent>> simulationSteps;
    private List<string> timeStampList;
    private List<DateTime> stepDateList;
    private List<List<NoteworthyEntry>> stepNoteworthy;   // 每步 noteworthy
    private int currentStepIndex = 0;
    private bool isPaused = false;
    private float speedMultiplier = 1f;
    private Coroutine playbackCoroutine;
    private DateTime simulatedTime;

    private int gridWidth = 1, gridHeight = 1;

    /*──────────────────────── Start ─────────────────────*/
    private void Start()
    {
        Debug.Log("[SIMBOOT:S1] SimPlayback Start()");
        if (AgentHistoryManager.Instance == null)
            new GameObject("HistoryManager").AddComponent<AgentHistoryManager>();

        Time.timeScale = 1f;
        loadingPanel?.Show();
        Debug.Log("[SIMBOOT:S1] LoadingPanel.Show() called");

        // 路径兜底
        if (string.IsNullOrEmpty(simFolderPath))
        {
            simFolderPath = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
                          ? GameManager.Instance.simPath
                          : System.IO.Path.Combine(Application.dataPath, "Sim");
        }
        Debug.Log("[SIMBOOT:S1] Using simFolderPath=" + simFolderPath);

        // 优先使用 Inspector 指定；否则自动发现
        if (dataLoader == null) dataLoader = FindObjectOfType<AsyncSimulationDataLoader>();
        if (dataLoaderSync == null) dataLoaderSync = FindObjectOfType<SimulationDataLoaderSync>();

        bool hooked = false;

        if (dataLoader != null)
        {
            dataLoader.simFolderPath = simFolderPath;
            dataLoader.OnDataLoaded -= OnDataLoaded; // 防重复
            dataLoader.OnDataLoaded += OnDataLoaded;
            Debug.Log("[SIMBOOT:S1] Subscribed to Async DataLoader.OnDataLoaded");
            hooked = true;
        }

        if (!hooked && dataLoaderSync != null)
        {
            dataLoaderSync.simFolderPath = simFolderPath;
            dataLoaderSync.OnDataLoaded -= OnDataLoaded_FromSync; // 防重复
            dataLoaderSync.OnDataLoaded += OnDataLoaded_FromSync;
            Debug.Log("[SIMBOOT:S1] Hooked to Sync DataLoader.OnDataLoaded");
            hooked = true;
        }

        if (!hooked)
        {
            Debug.LogError("[SIMBOOT:S1][ERR] No DataLoader found (neither Async nor Sync).");
        }
    }

    /*──────────────────────── 加载回调（异步） ─────────────────────*/
    private void OnDataLoaded()
    {
        Debug.Log("[SIMBOOT:S5] OnDataLoaded() - start");
        loadingPanel?.Hide();
        Debug.Log("[SIMBOOT:S5] LoadingPanel.Hide() called");

        simulationSteps = dataLoader.simulationSteps;
        timeStampList = dataLoader.stepTimestamps;
        stepDateList = dataLoader.stepDateTimes;
        stepNoteworthy = dataLoader.stepNoteworthy;

        PostDataReadyCommon();
    }

    /*──────────────────────── 加载回调（同步） ─────────────────────*/
    private void OnDataLoaded_FromSync()
    {
        Debug.Log("[SIMBOOT:S5] OnDataLoaded_FromSync() - start");
        loadingPanel?.Hide();

        simulationSteps = dataLoaderSync.simulationSteps;
        timeStampList = dataLoaderSync.stepTimestamps;
        stepDateList = dataLoaderSync.stepDateTimes;
        stepNoteworthy = dataLoaderSync.stepNoteworthy;

        PostDataReadyCommon();
    }

    /*──────────────────────── 数据就绪后的通用流程 ─────────────────────*/
    private void PostDataReadyCommon()
    {
        if (simulationSteps == null || simulationSteps.Count == 0)
        {
            Debug.LogError("[SIMBOOT:S5][ERR] No simulation steps!");
            return;
        }
        Debug.Log("[SIMBOOT:S5] Steps loaded: " + simulationSteps.Count);

        if (mapImage == null && SimulationMapManager.Instance != null)
            mapImage = SimulationMapManager.Instance.mapImage;

        // 计算网格尺寸（安全兜底为 >=1）
        if (SimulationMapManager.Instance != null && SimulationMapManager.Instance.mapImage != null)
        {
            Vector2 mapSize = SimulationMapManager.Instance.mapImage.rectTransform.sizeDelta;
            float factor = Mathf.Max(1e-5f, SimulationMapManager.Instance.backgroundScaleFactor);
            float grid = Mathf.Max(1f, agentRenderer != null ? agentRenderer.gridSize : 32f);
            gridWidth = Mathf.Max(1, Mathf.FloorToInt(mapSize.x / factor / grid));
            gridHeight = Mathf.Max(1, Mathf.FloorToInt(mapSize.y / factor / grid));
            Debug.Log($"[SIMBOOT:S5] Map size ready: {mapSize}, factor={factor}, grid={gridWidth}x{gridHeight}");
        }
        else
        {
            gridWidth = Mathf.Max(1, gridWidth);
            gridHeight = Mathf.Max(1, gridHeight);
            Debug.LogWarning("[SIMBOOT:S5] Map image not ready, using fallback grid (>=1)");
        }

        currentStepIndex = 0;
        simulatedTime = (stepDateList != null && stepDateList.Count > 0) ? stepDateList[0] : DateTime.Now;
        agentRenderer?.RenderAgents(simulationSteps[0]);
        Debug.Log("[SIMBOOT:S5] First frame rendered at step 0");

        WriteHistory(0, true);

        if (jumpStepInput) jumpStepInput.text = "0";
        UpdateStepInfoText();
        UpdateStepTimeText();

        // UI 事件绑定（防重复）
        playButton?.onClick.RemoveListener(OnPlay);
        pauseButton?.onClick.RemoveListener(OnPause);
        jumpButton?.onClick.RemoveListener(OnJump);
        speedDropdown?.onValueChanged.RemoveListener(OnSpeedChanged);
        toggleBubbleButton?.onClick.RemoveAllListeners();                // ★ 既有
        toggleJumpButtonsButton?.onClick.RemoveAllListeners();          // ★ 新增

        playButton?.onClick.AddListener(OnPlay);
        pauseButton?.onClick.AddListener(OnPause);
        jumpButton?.onClick.AddListener(OnJump);
        speedDropdown?.onValueChanged.AddListener(OnSpeedChanged);
        toggleBubbleButton?.onClick.AddListener(OnToggleBubbles);        // ★ 既有
        toggleJumpButtonsButton?.onClick.AddListener(OnToggleJumpButtons); // ★ 新增

        UpdateBubbleButtonLabel();               // ★ 既有
        UpdateJumpButtonsToggleLabel();          // ★ 新增

        SetPlayPauseInteractable(true, false);
        SetJumpUIInteractable(false);
        Debug.Log("[SIMBOOT:S5] UI wired; ready to play");
    }

    /*──────────────────── Update ────────────────────*/
    private void Update()
    {
        UpdateStepTimeText();

        if (!isPaused)
        {
            if (jumpStepInput) jumpStepInput.text = currentStepIndex.ToString();
            SetJumpUIInteractable(false);
        }
    }

    /*────────────── UI 回调 ──────────────*/
    private void OnPlay()
    {
        if (playbackCoroutine != null) return;

        isPaused = false;
        SetPlayPauseInteractable(false, true);
        SetJumpUIInteractable(false);
        playbackCoroutine = StartCoroutine(PlaybackCoroutine());
    }

    private void OnPause()
    {
        isPaused = true;
        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        playbackCoroutine = null;

        if (simulationSteps != null && simulationSteps.Count > 0 && agentRenderer != null)
        {
            agentRenderer.UpdateAgentsPositions(simulationSteps[currentStepIndex]);
        }
        WriteHistory(currentStepIndex, false);
        ForceAgentsIdle();

        SetPlayPauseInteractable(true, false);
        SetJumpUIInteractable(true);
    }

    private void OnJump()
    {
        if (simulationSteps == null || simulationSteps.Count == 0) return;
        if (jumpStepInput == null) return;

        if (!int.TryParse(jumpStepInput.text, out int target)) return;

        target = Mathf.Clamp(target, 0, simulationSteps.Count - 1);
        currentStepIndex = target;
        if (stepDateList != null && target < stepDateList.Count)
            simulatedTime = stepDateList[target];
        agentRenderer?.UpdateAgentsPositions(simulationSteps[target]);
        if (target > 0 && agentRenderer != null)
        {
            var prev = simulationSteps[target - 1];
            var curr = simulationSteps[target];

            foreach (var kv in prev)
            {
                string name = kv.Key;
                if (!curr.ContainsKey(name)) continue;

                var a = kv.Value?.bag ?? System.Array.Empty<string>();
                var b = curr[name]?.bag ?? System.Array.Empty<string>();

                var added = DiffAdded(a, b);
                var removed = DiffRemoved(a, b);

                if (added.Count > 0)
                    agentRenderer.PlayBagChangeBubble(name, added[0], true);
                else if (removed.Count > 0)
                    agentRenderer.PlayBagChangeBubble(name, removed[0], false);
            }
        }
        WriteHistory(target, true);
        UpdateStepInfoText();
        UpdateStepTimeText();
    }

    private void OnSpeedChanged(int idx)
    {
        speedMultiplier = idx switch
        {
            1 => 2f,
            2 => 4f,
            3 => 8f,
            4 => 16f,
            5 => 32f,
            6 => 64f,
            _ => 1f,
        };
    }

    private IEnumerator PlaybackCoroutine()
    {
        Debug.Log("[SIMBOOT:S5] PlaybackCoroutine started");

        if (simulationSteps == null || simulationSteps.Count <= 1)
        {
            Debug.LogWarning("[SIMBOOT:S5] Not enough steps to play.");
            playbackCoroutine = null;
            yield break;
        }

        while (currentStepIndex < simulationSteps.Count - 1)
        {
            var fromStep = simulationSteps[currentStepIndex];
            var toStep = simulationSteps[currentStepIndex + 1];
            float dur = Mathf.Max(1e-4f, baseStepDuration / Mathf.Max(1f, speedMultiplier));

            // —— 原有：做位移动画 —— //
            yield return StartCoroutine(AnimateStep(fromStep, toStep, dur));

            // ====== 新增：对比背包，触发“物品变更气泡” ======
            if (agentRenderer != null && fromStep != null && toStep != null)
            {
                foreach (var kv in fromStep)
                {
                    string name = kv.Key;
                    if (!toStep.ContainsKey(name)) continue;

                    var a = kv.Value.bag ?? System.Array.Empty<string>();
                    var b = toStep[name].bag ?? System.Array.Empty<string>();

                    // 计算增/减（按集合比较；若同名多件，建议你把 bag 切换为带数量，或这里做多重计数）
                    var added = DiffAdded(a, b);
                    var removed = DiffRemoved(a, b);

                    // 规则：若同时有增与减，优先显示“增”的第一件；否则显示“有的那种”的第一件
                    if (added.Count > 0)
                        agentRenderer.PlayBagChangeBubble(name, added[0], true);
                    else if (removed.Count > 0)
                        agentRenderer.PlayBagChangeBubble(name, removed[0], false);
                }
            }

            // —— 原有：进入下一步并刷新 UI —— //
            currentStepIndex++;
            agentRenderer?.UpdateAgentsPositions(simulationSteps[currentStepIndex]);
            WriteHistory(currentStepIndex, false);
            UpdateStepInfoText();
            UpdateStepTimeText();

            //Debug.Log("[SIMBOOT:S5] Advanced to step " + currentStepIndex);

            while (isPaused) yield return null;
        }
        playbackCoroutine = null;
        Debug.Log("[SIMBOOT:S5] PlaybackCoroutine finished");
    }

    // —— 小工具：集合差分（名字/ID 维度；如需“多件计数”，可改为 Dictionary<string,int>） —— //
    private List<string> DiffAdded(IEnumerable<string> oldBag, IEnumerable<string> newBag)
    {
        var A = new HashSet<string>(oldBag ?? System.Array.Empty<string>());
        var B = new HashSet<string>(newBag ?? System.Array.Empty<string>());
        var list = new List<string>();
        foreach (var x in B) if (!A.Contains(x)) list.Add(x);
        return list;
    }
    private List<string> DiffRemoved(IEnumerable<string> oldBag, IEnumerable<string> newBag)
    {
        var A = new HashSet<string>(oldBag ?? System.Array.Empty<string>());
        var B = new HashSet<string>(newBag ?? System.Array.Empty<string>());
        var list = new List<string>();
        foreach (var x in A) if (!B.Contains(x)) list.Add(x);
        return list;
    }


    /*──────────────── AnimateStep ────────────────*/
    private IEnumerator AnimateStep(
        Dictionary<string, SimulationAgent> fromStep,
        Dictionary<string, SimulationAgent> toStep,
        float duration)
    {
        float factor = (SimulationMapManager.Instance != null)
                     ? Mathf.Max(1e-5f, SimulationMapManager.Instance.backgroundScaleFactor)
                     : 1f;

        Dictionary<string, List<Vector2>> paths = new();
        const int SUBDIV = 10;

        // 预计算所有 agent 路径（带 A*）
        foreach (var kv in fromStep)
        {
            string name = kv.Key;

            if (!toStep.ContainsKey(name))
                continue;

            Vector2Int startGrid = new Vector2Int(kv.Value.curr_tile[0], kv.Value.curr_tile[1]);
            Vector2Int targetGrid = new Vector2Int(toStep[name].curr_tile[0], toStep[name].curr_tile[1]);

            List<Vector2Int> gridPath =
                AStarPathfinder.GetPath(startGrid, targetGrid, gridWidth, gridHeight);

            List<Vector2> world = new();
            if (agentRenderer != null)
            {
                float cell = Mathf.Max(1f, agentRenderer.gridSize);
                foreach (var g in gridPath)
                {
                    world.Add(new Vector2(g.x * cell * factor,
                                          -g.y * cell * factor));
                }
            }

            List<Vector2> sub = new();
            if (world.Count > 0)
            {
                sub.Add(world[0]);
                for (int i = 0; i < world.Count - 1; i++)
                {
                    Vector2 p0 = world[i];
                    Vector2 p1 = world[i + 1];
                    for (int s = 1; s <= SUBDIV; s++)
                        sub.Add(Vector2.Lerp(p0, p1, s / (float)SUBDIV));
                }
            }
            paths[name] = sub;
        }

        int maxLen = 0;
        foreach (var p in paths.Values) if (p.Count > maxLen) maxLen = p.Count;
        if (maxLen <= 1) { yield return new WaitForSeconds(duration); yield break; }

        float segDur = duration / (maxLen - 1);

        DateTime dtStart = (stepDateList != null && currentStepIndex < stepDateList.Count)
            ? stepDateList[currentStepIndex]
            : DateTime.Now;
        DateTime dtEnd = (stepDateList != null && currentStepIndex + 1 < stepDateList.Count)
            ? stepDateList[currentStepIndex + 1]
            : dtStart;

        simulatedTime = dtStart;
        float played = 0f;

        for (int seg = 0; seg < maxLen - 1; seg++)
        {
            float tElapsed = 0f;
            while (tElapsed < segDur)
            {
                float t = segDur > 1e-6f ? tElapsed / segDur : 1f;

                foreach (var kv in paths)
                {
                    string name = kv.Key;
                    List<Vector2> p = kv.Value;

                    if (p == null || p.Count == 0) continue;

                    Vector2 fromPos = seg < p.Count ? p[seg] : p[^1];
                    Vector2 toPos = (seg + 1) < p.Count ? p[seg + 1] : p[^1];
                    Vector2 pos = Vector2.Lerp(fromPos, toPos, t);

                    agentRenderer?.UpdateAgentPosition(name, pos);

                    bool moving = (toPos - fromPos).sqrMagnitude > 1e-4f;
                    var dir = AgentAnimationController.Direction.Down;
                    if (moving)
                    {
                        Vector2 d = toPos - fromPos;
                        dir = Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                            ? (d.x > 0 ? AgentAnimationController.Direction.Right
                                       : AgentAnimationController.Direction.Left)
                            : (d.y > 0 ? AgentAnimationController.Direction.Up
                                       : AgentAnimationController.Direction.Down);
                    }
                    agentRenderer?.GetAnimationController(name)?.UpdateAnimation(dir, moving, Time.deltaTime);
                }

                played += Time.deltaTime;
                float ratio = Mathf.Clamp01(duration > 1e-6f ? played / duration : 1f);
                simulatedTime = dtStart + TimeSpan.FromSeconds(
                    (dtEnd - dtStart).TotalSeconds * ratio);

                tElapsed += Time.deltaTime;
                yield return null;
            }
        }

        // 落地为 idle
        foreach (var kv in paths)
        {
            string name = kv.Key;
            var p = kv.Value;
            if (p == null || p.Count == 0) continue;

            Vector2 delta = p.Count >= 2 ? p[^1] - p[^2] : Vector2.zero;
            var idleDir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x > 0 ? AgentAnimationController.Direction.Right
                               : AgentAnimationController.Direction.Left)
                : (delta.y > 0 ? AgentAnimationController.Direction.Up
                               : AgentAnimationController.Direction.Down);

            agentRenderer?.GetAnimationController(name)?.UpdateAnimation(idleDir, false, 0f);
        }
    }

    /*──────────── 小工具 ───────────*/
    private void SetPlayPauseInteractable(bool playOn, bool pauseOn)
    {
        if (playButton) playButton.interactable = playOn;
        if (pauseButton) pauseButton.interactable = pauseOn;
    }
    private void SetJumpUIInteractable(bool on)
    {
        if (jumpButton) jumpButton.interactable = on;
        if (jumpStepInput) jumpStepInput.interactable = on;
    }

    /// <summary>
    /// 更新 stepInfoText：如果有 noteworthy，按多行显示：
    /// [person1, person2] EventText
    /// </summary>
    private void UpdateStepInfoText()
    {
        if (stepInfoText == null) return;

        if (stepNoteworthy != null &&
            currentStepIndex >= 0 &&
            currentStepIndex < stepNoteworthy.Count &&
            stepNoteworthy[currentStepIndex] != null &&
            stepNoteworthy[currentStepIndex].Count > 0)
        {
            var list = stepNoteworthy[currentStepIndex];
            StringBuilder sb = new StringBuilder();
            foreach (var entry in list)
            {
                string peoplePart = (entry.people != null && entry.people.Length > 0)
                    ? "[" + string.Join(", ", entry.people) + "] "
                    : "[ ] ";
                sb.AppendLine(peoplePart + entry.eventText);
            }
            stepInfoText.text = sb.ToString().TrimEnd();
        }
        else
        {
            stepInfoText.text = $"当前在第 {currentStepIndex} 步";
        }
    }

    private void UpdateStepTimeText()
    {
        if (stepTimeText == null || timeStampList == null ||
            currentStepIndex < 0 || currentStepIndex >= timeStampList.Count) return;

        string raw = timeStampList[currentStepIndex];
        stepTimeText.text = raw.Replace('_', ':').Replace('T', ' ');
    }

    private float GetDayFactor(DateTime dt)
    {
        float hourFrac = dt.Hour + dt.Minute / 60f + dt.Second / 3600f;
        float delta = Mathf.Abs(hourFrac - 12f) / 12f;
        return 1f - delta;
    }

    private void UpdateMapColor(DateTime dt)
    {
        if (mapImage == null) return;
        mapImage.color = Color.Lerp(nightColor, dayColor, GetDayFactor(dt));
    }

    private void ForceAgentsIdle()
    {
        if (simulationSteps == null || simulationSteps.Count == 0 || agentRenderer == null) return;

        foreach (var kv in simulationSteps[currentStepIndex])
            agentRenderer.GetAnimationController(kv.Key)?
                         .UpdateAnimation(AgentAnimationController.Direction.Down, false, 0f);
    }

    /*──────────────── 写 History ─────────────────*/
    private void WriteHistory(int stepIdx, bool forceWrite)
    {
        if (AgentHistoryManager.Instance == null) return;
        if (timeStampList == null || simulationSteps == null) return;
        if (stepIdx < 0 || stepIdx >= simulationSteps.Count || stepIdx >= timeStampList.Count) return;

        string raw = timeStampList[stepIdx];
        string timeStr = raw.Replace('_', ':').Replace('T', ' ');

        foreach (var kv in simulationSteps[stepIdx])
        {
            string agName = kv.Value.name;
            string act = kv.Value.activity;
            if (string.IsNullOrEmpty(act)) act = kv.Value.short_activity;
            if (string.IsNullOrEmpty(act)) act = "(idle)";

            AgentHistoryManager.Instance.AddRecord(
                agName, timeStr, act, forceWrite);
        }
    }

    // ★ 既有：气泡开关回调 + 按钮文案
    private void OnToggleBubbles()
    {
        if (agentRenderer == null) return;
        bool next = !agentRenderer.AreActivityBubblesEnabled;
        agentRenderer.SetActivityBubblesEnabled(next);
        UpdateBubbleButtonLabel();
    }

    private void UpdateBubbleButtonLabel()
    {
        if (toggleBubbleButton == null || agentRenderer == null) return;
        var label = toggleBubbleButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (label) label.text = agentRenderer.AreActivityBubblesEnabled ? "Hide" : "Show";
    }

    // ★ 新增：Agent 跳转按钮显隐开关
    private void OnToggleJumpButtons()
    {
        if (agentRenderer == null) return;
        bool next = !agentRenderer.AreAgentJumpButtonsEnabled;
        agentRenderer.SetAgentJumpButtonsEnabled(next);
        UpdateJumpButtonsToggleLabel();
    }

    private void UpdateJumpButtonsToggleLabel()
    {
        if (toggleJumpButtonsButton == null || agentRenderer == null) return;
        var label = toggleJumpButtonsButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (label) label.text = agentRenderer.AreAgentJumpButtonsEnabled ? "Hide" : "Show";
    }
}
