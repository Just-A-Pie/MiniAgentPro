// �ļ���AppNavigator.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AppNavigator : MonoBehaviour
{
    [Header("��ҳ������")]
    [Tooltip("���ص�Ŀ�곡������ҳ/���˵���")]
    public string homeSceneName = "Home";

    [Header("��ѡ���������֣�ȫ�� Image����ʼ͸����RaycastTarget=true��")]
    public Image fadeOverlay;
    [Tooltip("����ʱ�����룩")]
    public float fadeDuration = 0.2f;

    [Header("��ѡ��δ������ʾ���")]
    public GameObject confirmPanel;   // �������������ť�����沢�˳� / ֱ���˳� / ȡ��

    private bool _isQuitting;

    /// <summary>
    /// ������ҳ������������������ͼ�иĶ�����ȷ����壬���ȵ�ȷ�ϣ�����ֱ��ִ�н���������
    /// </summary>
    public void ReturnToHome()
    {
        if (_isQuitting) return;

        // ���衰�иĶ�����ʾ��
        bool dirty = MapManager.Instance != null && MapManager.Instance.isDirty;
        if (dirty && confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            return;
        }

        StartCoroutine(GoHomeRoutine());
    }

    // ====== ��ȷ������ϵ�������ť�� ======

    /// <summary>ȷ�ϣ����沢�˳���������Ŀ��ʵ�ʱ����߼�ʵ�� SaveCurrentMap ���˳���</summary>
    public void OnConfirmSaveAndExit()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        // TODO: �ڴ˵�����ı����߼�������У�
        // SaveCurrentMap();
        StartCoroutine(GoHomeRoutine());
    }

    /// <summary>ȷ�ϣ�������ֱ���˳�</summary>
    public void OnConfirmExitWithoutSave()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        StartCoroutine(GoHomeRoutine());
    }

    /// <summary>ȡ������</summary>
    public void OnCancelExit()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ============== �ڲ����� ==============

    private IEnumerator GoHomeRoutine()
    {
        _isQuitting = true;

        // 1) ��ѡ����
        if (fadeOverlay != null && fadeDuration > 0f)
        {
            fadeOverlay.gameObject.SetActive(true);
            // ȷ����ʼ͸��
            var c0 = fadeOverlay.color; c0.a = 0f; fadeOverlay.color = c0;

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / fadeDuration);
                var c = fadeOverlay.color; c.a = a; fadeOverlay.color = c;
                yield return null;
            }
            var c1 = fadeOverlay.color; c1.a = 1f; fadeOverlay.color = c1;
        }

        // 2) ������������յ��� �� �������� �� ж��������Դ
        yield return SoftRestart.Go(homeSceneName);

        // 3)����ѡ���ص���ҳ��ѵ����������أ����������ҳ��
        if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);

        _isQuitting = false;
    }
}
