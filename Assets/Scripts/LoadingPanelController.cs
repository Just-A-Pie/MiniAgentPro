using UnityEngine;
using UnityEngine.UI;

public class LoadingPanelController : MonoBehaviour
{
    // ָ����תָʾ���� Image ��������һ��Բ�� Spinner ͼ��
    public Image spinnerImage;
    // ��ת�ٶȣ���λ��/��
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
    /// ��ʼ��ת����
    /// </summary>
    public void StartSpinning()
    {
        isSpinning = true;
    }

    /// <summary>
    /// ֹͣ��ת����
    /// </summary>
    public void StopSpinning()
    {
        isSpinning = false;
    }

    private void Update()
    {
        if (isSpinning && spinnerImage != null)
        {
            // ÿ֡���� rotationSpeed ��ת spinnerImage �� RectTransform
            spinnerImage.rectTransform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// ��ʾ Loading Panel
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        StartSpinning();
    }

    /// <summary>
    /// ���� Loading Panel
    /// </summary>
    public void Hide()
    {
        StopSpinning();
        gameObject.SetActive(false);
    }
}
