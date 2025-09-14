// 文件：AppNavigator.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AppNavigator : MonoBehaviour
{
    [Header("主页场景名")]
    [Tooltip("返回的目标场景（主页/主菜单）")]
    public string homeSceneName = "Home";

    [Header("可选：淡出遮罩（全屏 Image，初始透明，RaycastTarget=true）")]
    public Image fadeOverlay;
    [Tooltip("淡出时长（秒）")]
    public float fadeDuration = 0.2f;

    [Header("可选：未保存提示面板")]
    public GameObject confirmPanel;   // 面板里做三个按钮：保存并退出 / 直接退出 / 取消

    private bool _isQuitting;

    /// <summary>
    /// 返回主页（近似重启）：若地图有改动且有确认面板，则先弹确认，否则直接执行近似重启。
    /// </summary>
    public void ReturnToHome()
    {
        if (_isQuitting) return;

        // 如需“有改动才提示”
        bool dirty = MapManager.Instance != null && MapManager.Instance.isDirty;
        if (dirty && confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            return;
        }

        StartCoroutine(GoHomeRoutine());
    }

    // ====== 供确认面板上的三个按钮绑定 ======

    /// <summary>确认：保存并退出（按你项目的实际保存逻辑实现 SaveCurrentMap 再退出）</summary>
    public void OnConfirmSaveAndExit()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        // TODO: 在此调用你的保存逻辑（如果有）
        // SaveCurrentMap();
        StartCoroutine(GoHomeRoutine());
    }

    /// <summary>确认：不保存直接退出</summary>
    public void OnConfirmExitWithoutSave()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        StartCoroutine(GoHomeRoutine());
    }

    /// <summary>取消返回</summary>
    public void OnCancelExit()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ============== 内部流程 ==============

    private IEnumerator GoHomeRoutine()
    {
        _isQuitting = true;

        // 1) 可选淡出
        if (fadeOverlay != null && fadeDuration > 0f)
        {
            fadeOverlay.gameObject.SetActive(true);
            // 确保起始透明
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

        // 2) 近似重启：清空单例 → 切主场景 → 卸载无用资源
        yield return SoftRestart.Go(homeSceneName);

        // 3)（可选）回到主页后把淡出遮罩隐藏，避免叠在主页上
        if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);

        _isQuitting = false;
    }
}
