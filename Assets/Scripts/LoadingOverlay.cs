using UnityEngine;
using TMPro;

public class LoadingOverlay : MonoBehaviour
{
    public static LoadingOverlay Instance;

    [Header("引用")]
    public GameObject rootPanel;      // 整个加载 Panel（Image 要勾选 Raycast Target）
    public RectTransform spinner;     // 子 Image：旋转
    public TMP_Text message;          // 子 Text：提示文案

    [Header("行为")]
    public float spinSpeed = 180f;    // 旋转速度（度/秒）
    public int bringToFrontOrder = 20000; // 单独 Canvas 时的排序号（很大以确保顶层）

    void Awake()
    {
        Instance = this;
        HideImmediate();
    }

    void Update()
    {
        if (rootPanel != null && rootPanel.activeSelf && spinner != null)
        {
            spinner.Rotate(0f, 0f, -spinSpeed * Time.unscaledDeltaTime);
        }
    }

    public void Show(string msg = null)
    {
        if (message != null && !string.IsNullOrEmpty(msg))
            message.text = msg;

        if (rootPanel == null)
        {
            Debug.LogWarning("[LoadingOverlay] rootPanel 未绑定，无法显示加载面板");
            return;
        }

        // 激活
        rootPanel.SetActive(true);

        // 置顶：同 Canvas 内把自己放到最后
        rootPanel.transform.SetAsLastSibling();

        // 如该面板挂了独立 Canvas，则强行提高排序
        var panelCanvas = rootPanel.GetComponent<Canvas>();
        if (panelCanvas != null)
        {
            panelCanvas.overrideSorting = true;
            // 让它在最上面（比你的主 Canvas 大很多）
            panelCanvas.sortingOrder = bringToFrontOrder;
        }

        // 立刻刷新一次布局/绘制队列
        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public void HideImmediate()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }
}
