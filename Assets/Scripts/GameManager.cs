using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public string resourcePath; // ��ͼ��Դ·��
    public string simPath;      // ��������·��

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // ��� simPath δ���ã���������Ĭ��ֵ
            if (string.IsNullOrEmpty(simPath))
            {
                simPath = System.IO.Path.Combine(Application.dataPath, "Sim");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
