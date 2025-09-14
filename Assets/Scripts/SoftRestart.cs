// �ļ���SoftRestart.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SoftRestart
{
    /// <summary>
    /// ������������չؼ����� �� �л�����ҳ �� ж��������Դ
    /// </summary>
    public static IEnumerator Go(string homeSceneName)
    {
        // 1) �������Ŀ��ĵ������ã����貹�䣩
        TryClearSingletons();

        // 2) �����������첽��˳����
        var op = SceneManager.LoadSceneAsync(homeSceneName, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        // 3) �ͷ��ڴ棨��ѡ�����Ƽ���
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }

    /// <summary>
    /// ���аѾ�̬ Instance �ÿգ������´ν��볡��ʱ�¶����� Awake ���Ի�
    /// </summary>
    private static void TryClearSingletons()
    {
        // ���� �����õ��ĵ������ÿգ������ʵ�ʽű�����������
        MapManager.Instance = null;
        SimulationMapManager.Instance = null;
        GridOverlayManager.Instance = null;
        EditorManager.Instance = null;
        ContainerLogoConfigManager.Instance = null;

        // �绹��������̬״̬/����/�¼���˳������������
        // Example:
        // SomeGlobalCache.Clear();
        // SomeStaticEvent = null;
        // Time.timeScale = 1f;  // ������ĳ�����Ĺ�ʱ������
    }
}
