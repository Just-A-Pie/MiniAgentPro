// GameManager.cs ����չʾ�ؼ��Ķ���
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public string resourcePath; // ��ͼ��Դ·����֧�� "root:" �����·��
    public string simPath;      // ��������·����֧�� "root:" �����·��

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // ͳһ�淶���������ף�
            resourcePath = NormalizeOrDefault(resourcePath, "root:/sampleMap", "resourcePath");
            simPath = NormalizeOrDefault(simPath, "root:/sampleSim", "simPath");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private string NormalizeOrDefault(string current, string fallback, string label)
    {
        // �����������õģ�û���þ��ö���
        string candidate = string.IsNullOrWhiteSpace(current) ? fallback : current;
        string resolved = RootPath.Resolve(candidate);

        if (string.IsNullOrEmpty(resolved))
        {
            Debug.LogWarning($"GameManager: {label} ��Ч��{candidate}������ʹ�� EXE Ŀ¼���ס�");
            return RootPath.GetExeDir(); // ��󶵵ף����������ߵ���
        }
        return resolved; // ��Զ���ؾ���·��
    }
}
