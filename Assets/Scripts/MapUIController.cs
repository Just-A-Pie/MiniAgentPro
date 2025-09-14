// �ļ�: MapUIController.cs
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
        // ��ǰ�������пգ����� Start ���� NRE
        if (saveButton == null || resetButton == null)
        {
            Debug.LogWarning("[MapUIController] saveButton/resetButton δ�󶨣�������������Ա��� NRE��");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // �¼���ȫ��
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

        // �ؼ�������������飨���ھͼ�����������Ҳ�����쳣��ֻ�ǲ��ֹ��ܲ����ã�
        if (MapManager.Instance == null)
            Debug.LogWarning("[MapUIController] MapManager.Instance Ϊ�գ�����/���ý������á�");
        if (GridOverlayManager.Instance == null)
            Debug.LogWarning("[MapUIController] GridOverlayManager.Instance Ϊ�գ����񿪹ؽ������á�");

        _inited = true;
    }

    void Update()
    {
        if (!_inited) return;

        // ����ʽ��û�� resetButton ֱ�ӷ��أ�û�� MapManager Ҳ������
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
            Debug.LogWarning("[MapUIController] Save ������� MapManager.Instance Ϊ�ա�");
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
            Debug.LogWarning("[MapUIController] Reset ������� MapManager δ������δ�ࡣ");
        }
    }

    void OnToggleLogoClicked()
    {
        if (MapManager.Instance == null || MapManager.Instance.mapContent == null)
        {
            Debug.LogWarning("[MapUIController] MapManager �� mapContent δ���ã�");
            return;
        }
        var logoContainer = MapManager.Instance.mapContent.Find("LogoContainer");
        if (logoContainer != null)
        {
            bool active = logoContainer.gameObject.activeSelf;
            logoContainer.gameObject.SetActive(!active);

            // ���°�ť���֣�����У�
            if (toggleLogoButton != null)
            {
                var txt = toggleLogoButton.GetComponentInChildren<Text>();
                if (txt != null)
                    txt.text = active ? "��ʾ Logo" : "���� Logo";
            }
        }
        else
        {
            Debug.LogWarning("[MapUIController] δ�ҵ� LogoContainer��");
        }
    }

    void OnToggleOverlayClicked()
    {
        if (GridOverlayManager.Instance == null)
        {
            Debug.LogWarning("[MapUIController] GridOverlayManager δ��ʼ����");
            return;
        }
        GridOverlayManager.Instance.ToggleOverlayMode();
    }
}
