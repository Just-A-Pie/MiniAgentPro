// 文件: MapUIController.cs
using UnityEngine;
using UnityEngine.UI;

public class MapUIController : MonoBehaviour
{
    public Button saveButton;
    public Button resetButton;
    public Button toggleLogoButton;
    public Button toggleOverlayButton;

    void Start()
    {
        saveButton.onClick.AddListener(OnSaveClicked);
        resetButton.onClick.AddListener(OnResetClicked);
        if (toggleLogoButton != null)
            toggleLogoButton.onClick.AddListener(OnToggleLogoClicked);
        if (toggleOverlayButton != null)
            toggleOverlayButton.onClick.AddListener(OnToggleOverlayClicked);
    }

    void Update()
    {
        if (MapManager.Instance != null)
            resetButton.interactable = MapManager.Instance.isDirty;
    }

    void OnSaveClicked()
    {
        if (MapManager.Instance != null)
        {
            MapManager.Instance.SaveAllCsv();
            MapManager.Instance.SaveMapData();
        }
    }

    void OnResetClicked()
    {
        if (MapManager.Instance != null && MapManager.Instance.isDirty)
        {
            MapManager.Instance.ResetAllCsv();
            MapManager.Instance.ReloadMapData();
        }
    }

    void OnToggleLogoClicked()
    {
        if (MapManager.Instance == null || MapManager.Instance.mapContent == null)
        {
            Debug.LogWarning("MapManager 或 mapContent 未设置！");
            return;
        }
        var logoContainer = MapManager.Instance.mapContent.Find("LogoContainer");
        if (logoContainer != null)
        {
            bool active = logoContainer.gameObject.activeSelf;
            logoContainer.gameObject.SetActive(!active);
            var txt = toggleLogoButton.GetComponentInChildren<Text>();
            if (txt != null)
                txt.text = active ? "隐藏 Logo" : "显示 Logo";
        }
        else
        {
            Debug.LogWarning("未找到 LogoContainer！");
        }
    }

    void OnToggleOverlayClicked()
    {
        if (GridOverlayManager.Instance == null)
        {
            Debug.LogWarning("GridOverlayManager 未初始化！");
            return;
        }
        GridOverlayManager.Instance.ToggleOverlayMode();
    }
}
