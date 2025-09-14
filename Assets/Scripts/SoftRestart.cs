// 文件：SoftRestart.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SoftRestart
{
    /// <summary>
    /// 近似重启：清空关键单例 → 切换到主页 → 卸载无用资源
    /// </summary>
    public static IEnumerator Go(string homeSceneName)
    {
        // 1) 清空你项目里的单例引用（按需补充）
        TryClearSingletons();

        // 2) 切主场景（异步更顺滑）
        var op = SceneManager.LoadSceneAsync(homeSceneName, LoadSceneMode.Single);
        while (!op.isDone) yield return null;

        // 3) 释放内存（可选，但推荐）
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();
    }

    /// <summary>
    /// 集中把静态 Instance 置空，避免下次进入场景时新对象在 Awake 里自毁
    /// </summary>
    private static void TryClearSingletons()
    {
        // —— 把你用到的单例都置空（按你的实际脚本增减）——
        MapManager.Instance = null;
        SimulationMapManager.Instance = null;
        GridOverlayManager.Instance = null;
        EditorManager.Instance = null;
        ContainerLogoConfigManager.Instance = null;

        // 如还有其它静态状态/缓存/事件，顺便在这里清理
        // Example:
        // SomeGlobalCache.Clear();
        // SomeStaticEvent = null;
        // Time.timeScale = 1f;  // 若你在某处曾改过时间缩放
    }
}
