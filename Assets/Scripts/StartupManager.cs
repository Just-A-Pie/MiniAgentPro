// 文件: StartupManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartupManager : MonoBehaviour
{
    // ───────────────── 窗口启动设置（新增，可在 Inspector 调整） ─────────────────
    [Header("窗口启动设置（新增）")]
    [Tooltip("启动时强制用窗口模式（不是无边框全屏）")]
    public bool forceWindowedMode = true;

    [Tooltip("按主显示器分辨率的比例设置窗口大小，例如 0.8 表示 80%")]
    [Range(0.3f, 1.0f)]
    public float screenFraction = 0.8f;

    [Tooltip("窗口最小宽度（像素）")]
    public int minWidth = 960;

    [Tooltip("窗口最小高度（像素）")]
    public int minHeight = 540;

    [Tooltip("启动时是否强制设置窗口大小（覆盖 Player Settings 默认分辨率）")]
    public bool forceWindowSizeOnStart = true;

    // ───────────────── 你的原有字段 ─────────────────
    [Header("输入框")]
    public TMP_InputField mapFolderInput;   // 地图文件夹地址输入框
    public TMP_InputField simFolderInput;   // Simulation 文件夹地址输入框

    [Header("启动按钮")]
    public Button mapEditorButton;          // 启动地图编辑器按钮
    public Button simulationButton;         // 启动 Simulation 模块按钮

    // ───────────────── 新增：Awake 中设置窗口模式/尺寸 ─────────────────
    private void Awake()
    {
        // 1) 强制窗口模式（避免无边框“全屏窗口”）
        if (forceWindowedMode)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }

        // 2) 按显示器分辨率比例设置一个较小窗口，避免一启动就铺满屏幕
        if (forceWindowSizeOnStart)
        {
            // 主显示器分辨率；若不可用则回退到当前分辨率
            int sw = (Display.main != null) ? Display.main.systemWidth : Screen.currentResolution.width;
            int sh = (Display.main != null) ? Display.main.systemHeight : Screen.currentResolution.height;

            int targetW = Mathf.Max(minWidth, Mathf.RoundToInt(sw * screenFraction));
            int targetH = Mathf.Max(minHeight, Mathf.RoundToInt(sh * screenFraction));

#if UNITY_2019_1_OR_NEWER
            // 新版 API：直接指定窗口模式
            Screen.SetResolution(targetW, targetH, FullScreenMode.Windowed);
#else
            // 兼容老 API：false = 非全屏（窗口）
            Screen.SetResolution(targetW, targetH, false);
#endif
        }
    }

    // ───────────────── 下面是你原有的 Start/逻辑（未改动） ─────────────────
    private void Start()
    {
        // 自动填充（仅当当前为空时）
        if (mapFolderInput != null && string.IsNullOrWhiteSpace(mapFolderInput.text))
            mapFolderInput.text = "root:/sampleMap";
        if (simFolderInput != null && string.IsNullOrWhiteSpace(simFolderInput.text))
            simFolderInput.text = "root:/sampleSim";

        // 为按钮绑定点击事件
        if (mapEditorButton != null)
            mapEditorButton.onClick.AddListener(OnMapEditorButtonClicked);
        if (simulationButton != null)
            simulationButton.onClick.AddListener(OnSimulationButtonClicked);
    }

    // 地图编辑器启动按钮点击事件处理
    private void OnMapEditorButtonClicked()
    {
        if (mapFolderInput == null)
        {
            Debug.LogWarning("地图文件夹输入框未绑定！");
            return;
        }

        string mapFolderRaw = mapFolderInput.text.Trim();
        if (string.IsNullOrEmpty(mapFolderRaw))
        {
            Debug.LogWarning("地图文件夹地址为空！");
            return;
        }

        // 解析 root:/ 前缀或绝对路径
        string mapFolderResolved = RootPath.Resolve(mapFolderRaw);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.resourcePath = mapFolderResolved;
            Debug.Log($"地图文件夹地址设置为（原始='{mapFolderRaw}' → 解析='{mapFolderResolved}'）");
        }
        else
        {
            Debug.LogError("未找到 GameManager 实例！");
            return;
        }

        SceneManager.LoadScene("EditingPage");
    }

    // Simulation 启动按钮点击事件处理（使用异步加载方式）
    private void OnSimulationButtonClicked()
    {
        if (mapFolderInput == null || simFolderInput == null)
        {
            Debug.LogWarning("地图/仿真 输入框未绑定！");
            return;
        }

        string mapFolderRaw = mapFolderInput.text.Trim();
        string simFolderRaw = simFolderInput.text.Trim();

        if (string.IsNullOrEmpty(mapFolderRaw))
        {
            Debug.LogWarning("地图文件夹地址为空！");
            return;
        }
        if (string.IsNullOrEmpty(simFolderRaw))
        {
            Debug.LogWarning("Simulation 文件夹地址为空！");
            return;
        }

        // 解析 root:/ 前缀或绝对路径
        string mapFolderResolved = RootPath.Resolve(mapFolderRaw);
        string simFolderResolved = RootPath.Resolve(simFolderRaw);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.resourcePath = mapFolderResolved;
            GameManager.Instance.simPath = simFolderResolved;
            Debug.Log($"地图文件夹设置（原='{mapFolderRaw}' → 解析='{mapFolderResolved}'）；" +
                      $"Simulation 文件夹设置（原='{simFolderRaw}' → 解析='{simFolderResolved}'）");
        }
        else
        {
            Debug.LogError("未找到 GameManager 实例！");
            return;
        }

        // 异步加载 SimulationPage 场景
        StartCoroutine(LoadSimulationSceneAsync("SimulationPage"));
    }

    private IEnumerator LoadSimulationSceneAsync(string sceneName)
    {
        Debug.Log("[SIMBOOT:S0] Begin LoadSimulationSceneAsync -> " + sceneName);
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
        {
            yield return null;
        }
        Debug.Log("[SIMBOOT:S0] Reached 0.9, waiting 1s before activation");
        yield return new WaitForSeconds(1f);

        asyncOp.allowSceneActivation = true;
        Debug.Log("[SIMBOOT:S0] allowSceneActivation = true");
    }

}
