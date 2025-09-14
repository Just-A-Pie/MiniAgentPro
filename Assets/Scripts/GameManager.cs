// GameManager.cs （仅展示关键改动）
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public string resourcePath; // 地图资源路径：支持 "root:" 或绝对路径
    public string simPath;      // 仿真数据路径：支持 "root:" 或绝对路径

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 统一规范化（含兜底）
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
        // 优先用已设置的，没设置就用兜底
        string candidate = string.IsNullOrWhiteSpace(current) ? fallback : current;
        string resolved = RootPath.Resolve(candidate);

        if (string.IsNullOrEmpty(resolved))
        {
            Debug.LogWarning($"GameManager: {label} 无效（{candidate}）。将使用 EXE 目录兜底。");
            return RootPath.GetExeDir(); // 最后兜底（几乎不会走到）
        }
        return resolved; // 永远返回绝对路径
    }
}
