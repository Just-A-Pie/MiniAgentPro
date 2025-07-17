using UnityEngine;
using UnityEngine.UI;

public class LoadingPanelController : MonoBehaviour
{
    // 指向旋转指示器的 Image 对象（例如一个圆形 Spinner 图）
    public Image spinnerImage;
    // 旋转速度，单位度/秒
    public float rotationSpeed = 90f;

    private bool isSpinning = false;

    private void OnEnable()
    {
        StartSpinning();
    }

    private void OnDisable()
    {
        StopSpinning();
    }

    /// <summary>
    /// 开始旋转动画
    /// </summary>
    public void StartSpinning()
    {
        isSpinning = true;
    }

    /// <summary>
    /// 停止旋转动画
    /// </summary>
    public void StopSpinning()
    {
        isSpinning = false;
    }

    private void Update()
    {
        if (isSpinning && spinnerImage != null)
        {
            // 每帧根据 rotationSpeed 旋转 spinnerImage 的 RectTransform
            spinnerImage.rectTransform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 显示 Loading Panel
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        StartSpinning();
    }

    /// <summary>
    /// 隐藏 Loading Panel
    /// </summary>
    public void Hide()
    {
        StopSpinning();
        gameObject.SetActive(false);
    }
}
