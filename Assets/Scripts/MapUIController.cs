// 文件: MapUIController.cs
using UnityEngine;
using UnityEngine.UI;

public class MapUIController : MonoBehaviour
{
    public Button saveButton;
    public Button resetButton;
    public Button toggleLogoButton;
    public Button toggleOverlayButton;

    private bool _inited;

    void Awake()
    {
        // 提前做基础判空，避免 Start 初期 NRE
        if (saveButton == null || resetButton == null)
        {
            Debug.LogWarning("[MapUIController] saveButton/resetButton 未绑定，组件将被禁用以避免 NRE。");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // 事件安全绑定
        saveButton.onClick.RemoveAllListeners();
        saveButton.onClick.AddListener(OnSaveClicked);

        resetButton.onClick.RemoveAllListeners();
        resetButton.onClick.AddListener(OnResetClicked);

        if (toggleLogoButton != null)
        {
            toggleLogoButton.onClick.RemoveAllListeners();
            toggleLogoButton.onClick.AddListener(OnToggleLogoClicked);
        }
        if (toggleOverlayButton != null)
        {
            toggleOverlayButton.onClick.RemoveAllListeners();
            toggleOverlayButton.onClick.AddListener(OnToggleOverlayClicked);
        }

        // 关键依赖的轻量检查（存在就继续，不存在也不抛异常，只是部分功能不可用）
        if (MapManager.Instance == null)
            Debug.LogWarning("[MapUIController] MapManager.Instance 为空，保存/重置将不可用。");
        if (GridOverlayManager.Instance == null)
            Debug.LogWarning("[MapUIController] GridOverlayManager.Instance 为空，网格开关将不可用。");

        _inited = true;
    }

    void Update()
    {
        if (!_inited) return;

        // 防御式：没有 resetButton 直接返回；没有 MapManager 也不报错
        if (resetButton != null)
        {
            bool canReset = (MapManager.Instance != null && MapManager.Instance.isDirty);
            resetButton.interactable = canReset;
        }
    }

    void OnSaveClicked()
    {
        if (MapManager.Instance != null)
        {
            MapManager.Instance.SaveAllCsv();
            MapManager.Instance.SaveMapData();
        }
        else
        {
            Debug.LogWarning("[MapUIController] Save 点击，但 MapManager.Instance 为空。");
        }
    }

    void OnResetClicked()
    {
        if (MapManager.Instance != null && MapManager.Instance.isDirty)
        {
            MapManager.Instance.ResetAllCsv();
            MapManager.Instance.ReloadMapData();
        }
        else
        {
            Debug.LogWarning("[MapUIController] Reset 点击，但 MapManager 未就绪或未脏。");
        }
    }

    void OnToggleLogoClicked()
    {
        if (MapManager.Instance == null || MapManager.Instance.mapContent == null)
        {
            Debug.LogWarning("[MapUIController] MapManager 或 mapContent 未设置！");
            return;
        }
        var logoContainer = MapManager.Instance.mapContent.Find("LogoContainer");
        if (logoContainer != null)
        {
            bool active = logoContainer.gameObject.activeSelf;
            logoContainer.gameObject.SetActive(!active);

            // 更新按钮文字（如果有）
            if (toggleLogoButton != null)
            {
                var txt = toggleLogoButton.GetComponentInChildren<Text>();
                if (txt != null)
                    txt.text = active ? "显示 Logo" : "隐藏 Logo";
            }
        }
        else
        {
            Debug.LogWarning("[MapUIController] 未找到 LogoContainer！");
        }
    }

    void OnToggleOverlayClicked()
    {
        if (GridOverlayManager.Instance == null)
        {
            Debug.LogWarning("[MapUIController] GridOverlayManager 未初始化！");
            return;
        }
        GridOverlayManager.Instance.ToggleOverlayMode();
    }
}
