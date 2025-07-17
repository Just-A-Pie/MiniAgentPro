using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public string resourcePath; // 地图资源路径
    public string simPath;      // 仿真数据路径

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // 如果 simPath 未设置，可以设置默认值
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
