// 文件：SimulationPlaybackController.cs  （完整覆盖版）
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Globalization;

public class SimulationPlaybackController : MonoBehaviour
{
    /*──────────────────────── Inspector 字段 ─────────────────────*/
    [Header("播放控制 UI")]
    public Button playButton;
    public Button pauseButton;
    public Button jumpButton;
    public TMP_Dropdown speedDropdown;
    public TMP_InputField jumpStepInput;

    [Header("Agent 渲染")]
    public SimulationAgentRenderer agentRenderer;

    [Header("播放参数")]
    public float baseStepDuration = 1f;

    [Header("仿真数据路径")]
    public string simFolderPath;

    [Header("异步加载组件")]
    public LoadingPanelController loadingPanel;
    public AsyncSimulationDataLoader dataLoader;

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
    private int currentStepIndex = 0;
    private bool isPaused = false;
    private float speedMultiplier = 1f;
    private Coroutine playbackCoroutine;
    private DateTime simulatedTime;                      // ★ 当前模拟时间

    // A* 用的网格宽高
    private int gridWidth, gridHeight;

    // ★ 记录本 step 已写入过的 agent，避免重复
    private readonly HashSet<string> writtenThisStep = new();

    /*──────────────────────── Start ─────────────────────*/
    private void Start()
    {
        // 若场景里还没有 HistoryManager，则自动新建
        if (AgentHistoryManager.Instance == null)
            new GameObject("HistoryManager").AddComponent<AgentHistoryManager>();

        Time.timeScale = 1f;
        loadingPanel?.Show();

        if (string.IsNullOrEmpty(simFolderPath))
        {
            simFolderPath = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
                          ? GameManager.Instance.simPath
                          : System.IO.Path.Combine(Application.dataPath, "Sim");
        }

        dataLoader.simFolderPath = simFolderPath;
        dataLoader.OnDataLoaded += OnDataLoaded;
    }

    /*────────────── 数据加载完成 ─────────────*/
    private void OnDataLoaded()
    {
        loadingPanel?.Hide();

        simulationSteps = dataLoader.simulationSteps;
        timeStampList = dataLoader.stepTimestamps;
        stepDateList = dataLoader.stepDateTimes;

        if (simulationSteps == null || simulationSteps.Count == 0)
        {
            Debug.LogError("[SimPlayback] 无仿真数据！");
            return;
        }

        // mapImage 缺省自动获取
        if (mapImage == null && SimulationMapManager.Instance != null)
            mapImage = SimulationMapManager.Instance.mapImage;

        // 计算网格宽高（用于 A*）
        if (SimulationMapManager.Instance != null && SimulationMapManager.Instance.mapImage != null)
        {
            Vector2 mapSize = SimulationMapManager.Instance.mapImage.rectTransform.sizeDelta;
            float factor = SimulationMapManager.Instance.backgroundScaleFactor;
            gridWidth = Mathf.FloorToInt(mapSize.x / factor / agentRenderer.gridSize);
            gridHeight = Mathf.FloorToInt(mapSize.y / factor / agentRenderer.gridSize);
        }

        /*────────── 首帧渲染 ──────────*/
        currentStepIndex = 0;
        simulatedTime = stepDateList[0];          // ★ 初始化模拟时间
        agentRenderer.RenderAgents(simulationSteps[0]);
        WriteHistory(0,true);                             // ★ 写历史(首帧)

        jumpStepInput.text = "0";
        UpdateStepInfoText();
        UpdateStepTimeText();

        /*────────── UI 事件绑定 ──────────*/
        playButton?.onClick.AddListener(OnPlay);
        pauseButton?.onClick.AddListener(OnPause);
        jumpButton?.onClick.AddListener(OnJump);
        speedDropdown?.onValueChanged.AddListener(OnSpeedChanged);

        SetPlayPauseInteractable(true, false);
        SetJumpUIInteractable(false);
    }

    /*──────────────────── Update ────────────────────*/
    private void Update()
    {
        UpdateStepTimeText();

        if (!isPaused)
        {
            jumpStepInput.text = currentStepIndex.ToString();
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

        agentRenderer.UpdateAgentsPositions(simulationSteps[currentStepIndex]);
        WriteHistory(currentStepIndex,false);              // ★ 写历史(暂停刷新)
        ForceAgentsIdle();

        SetPlayPauseInteractable(true, false);
        SetJumpUIInteractable(true);
    }

    private void OnJump()
    {
        if (!int.TryParse(jumpStepInput.text, out int target)) return;

        target = Mathf.Clamp(target, 0, simulationSteps.Count - 1);
        currentStepIndex = target;
        simulatedTime = stepDateList[target];     // 同步时间
        agentRenderer.UpdateAgentsPositions(simulationSteps[target]);
        WriteHistory(target,true);                        // ★ 写历史(跳帧)
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

    /*──────────────── 播放协程 ────────────────*/
    private IEnumerator PlaybackCoroutine()
    {
        while (currentStepIndex < simulationSteps.Count - 1)
        {
            var fromStep = simulationSteps[currentStepIndex];
            var toStep = simulationSteps[currentStepIndex + 1];
            float dur = baseStepDuration / speedMultiplier;

            yield return StartCoroutine(AnimateStep(fromStep, toStep, dur));

            currentStepIndex++;
            agentRenderer.UpdateAgentsPositions(simulationSteps[currentStepIndex]);
            WriteHistory(currentStepIndex,false);          // ★ 写历史(每步)
            UpdateStepInfoText();
            UpdateStepTimeText();

            while (isPaused) yield return null;
        }
        playbackCoroutine = null;
    }

    /*──────────────── AnimateStep ────────────────*/
    private IEnumerator AnimateStep(
        Dictionary<string, SimulationAgent> fromStep,
        Dictionary<string, SimulationAgent> toStep,
        float duration)
    {
        /*── 路径预处理 ─────────────────*/
        float factor = SimulationMapManager.Instance != null
                     ? SimulationMapManager.Instance.backgroundScaleFactor
                     : 1f;

        Dictionary<string, List<Vector2>> paths = new();
        const int subdivisions = 10;

        foreach (var kv in fromStep)
        {
            string name = kv.Key;

            Vector2Int startGrid = new(kv.Value.curr_tile[0], kv.Value.curr_tile[1]);
            Vector2Int targetGrid = new(toStep[name].curr_tile[0], toStep[name].curr_tile[1]);

            List<Vector2Int> gridPath =
                AStarPathfinder.GetPath(startGrid, targetGrid, gridWidth, gridHeight);

            List<Vector2> world = new();
            foreach (var g in gridPath)
                world.Add(new Vector2(g.x * agentRenderer.gridSize * factor,
                                      -g.y * agentRenderer.gridSize * factor));

            List<Vector2> sub = new();
            if (world.Count > 0)
            {
                sub.Add(world[0]);
                for (int i = 0; i < world.Count - 1; i++)
                {
                    Vector2 p0 = world[i];
                    Vector2 p1 = world[i + 1];
                    for (int s = 1; s <= subdivisions; s++)
                        sub.Add(Vector2.Lerp(p0, p1, s / (float)subdivisions));
                }
            }
            paths[name] = sub;
        }

        int maxLen = 0;
        foreach (var p in paths.Values) if (p.Count > maxLen) maxLen = p.Count;
        if (maxLen <= 1) { yield return new WaitForSeconds(duration); yield break; }

        float segDur = duration / (maxLen - 1);

        /*── 初始化本段时间 ───────────────*/
        DateTime dtStart = stepDateList[currentStepIndex];
        DateTime dtEnd = stepDateList[currentStepIndex + 1];
        simulatedTime = dtStart;                               // 起点时间
        float played = 0f;

        /*── 分段插值 ───────────────────*/
        for (int seg = 0; seg < maxLen - 1; seg++)
        {
            float tElapsed = 0f;
            while (tElapsed < segDur)
            {
                float t = tElapsed / segDur;

                // 1. 位置 & 动画
                foreach (var kv in paths)
                {
                    string name = kv.Key;
                    List<Vector2> p = kv.Value;

                    Vector2 fromPos = seg < p.Count ? p[seg] : p[^1];
                    Vector2 toPos = seg + 1 < p.Count ? p[seg + 1] : p[^1];
                    Vector2 pos = Vector2.Lerp(fromPos, toPos, t);

                    agentRenderer.UpdateAgentPosition(name, pos);

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
                    agentRenderer.GetAnimationController(name)?.UpdateAnimation(dir, moving, Time.deltaTime);
                }

                // 2. 推进时间
                played += Time.deltaTime;
                float ratio = Mathf.Clamp01(played / duration);
                simulatedTime = dtStart + TimeSpan.FromSeconds(
                                    (dtEnd - dtStart).TotalSeconds * ratio);

                tElapsed += Time.deltaTime;
                yield return null;
            }
        }

        /*── 结束时 Idle ─────────────────*/
        foreach (var kv in paths)
        {
            string name = kv.Key;
            var p = kv.Value;

            Vector2 delta = p.Count >= 2 ? p[^1] - p[^2] : Vector2.zero;
            var idleDir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x > 0 ? AgentAnimationController.Direction.Right
                               : AgentAnimationController.Direction.Left)
                : (delta.y > 0 ? AgentAnimationController.Direction.Up
                               : AgentAnimationController.Direction.Down);

            agentRenderer.GetAnimationController(name)
                         ?.UpdateAnimation(idleDir, false, 0f);
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

    private void UpdateStepInfoText()
    {
        if (stepInfoText)
            stepInfoText.text = $"当前在第 {currentStepIndex} 步";
    }

    /*──────────── 时间文字 + 昼夜调色 ───────────*/
    private void UpdateStepTimeText()
    {
        if (stepTimeText == null || timeStampList == null ||
            currentStepIndex >= timeStampList.Count) return;

        // 与历史页保持一致的轻量格式化
        string raw = timeStampList[currentStepIndex];
        stepTimeText.text = raw.Replace('_', ':').Replace('T', ' ');
    }


    /*──────────── 昼夜颜色工具 ───────────*/
    private float GetDayFactor(DateTime dt)
    {
        float hourFrac = dt.Hour + dt.Minute / 60f + dt.Second / 3600f;
        float delta = Mathf.Abs(hourFrac - 12f) / 12f;   // 0 正午,1 午夜
        return 1f - delta;                               // 0=夜,1=昼
    }

    private void UpdateMapColor(DateTime dt)
    {
        if (mapImage == null) return;
        mapImage.color = Color.Lerp(nightColor, dayColor, GetDayFactor(dt));
    }

    private void ForceAgentsIdle()
    {
        foreach (var kv in simulationSteps[currentStepIndex])
            agentRenderer.GetAnimationController(kv.Key)?
                         .UpdateAnimation(AgentAnimationController.Direction.Down, false, 0f);
    }

    /*──────────────────────────────────────────────
     *         ★★★★★  写 History 核心 ★★★★★
     *────────────────────────────────────────────*/
    // ★ 新实现：forceWrite 控制是否强制写入
    private void WriteHistory(int stepIdx, bool forceWrite)
    {
        if (AgentHistoryManager.Instance == null) return;

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

}
