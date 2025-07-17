using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartupManager : MonoBehaviour
{
    [Header("输入框")]
    public TMP_InputField mapFolderInput;   // 地图文件夹地址输入框
    public TMP_InputField simFolderInput;     // Simulation 文件夹地址输入框

    [Header("启动按钮")]
    public Button mapEditorButton;            // 启动地图编辑器按钮
    public Button simulationButton;           // 启动 Simulation 模块按钮

    private void Start()
    {
        // 为按钮绑定点击事件
        if (mapEditorButton != null)
            mapEditorButton.onClick.AddListener(OnMapEditorButtonClicked);
        if (simulationButton != null)
            simulationButton.onClick.AddListener(OnSimulationButtonClicked);
    }

    // 地图编辑器启动按钮点击事件处理
    private void OnMapEditorButtonClicked()
    {
        string mapFolder = mapFolderInput.text.Trim();
        if (string.IsNullOrEmpty(mapFolder))
        {
            Debug.LogWarning("地图文件夹地址为空！");
            return;
        }
        GameManager.Instance.resourcePath = mapFolder;
        Debug.Log("地图文件夹地址设置为：" + mapFolder);
        SceneManager.LoadScene("EditingPage");
    }

    // Simulation 启动按钮点击事件处理（使用异步加载方式）
    private void OnSimulationButtonClicked()
    {
        string mapFolder = mapFolderInput.text.Trim();
        string simFolder = simFolderInput.text.Trim();

        if (string.IsNullOrEmpty(mapFolder))
        {
            Debug.LogWarning("地图文件夹地址为空！");
            return;
        }
        if (string.IsNullOrEmpty(simFolder))
        {
            Debug.LogWarning("Simulation 文件夹地址为空！");
            return;
        }
        GameManager.Instance.resourcePath = mapFolder;
        GameManager.Instance.simPath = simFolder;
        Debug.Log("地图文件夹地址设置为：" + mapFolder + "；Simulation 文件夹地址设置为：" + simFolder);

        // 异步加载 SimulationPage 场景
        StartCoroutine(LoadSimulationSceneAsync("SimulationPage"));
    }

    private IEnumerator LoadSimulationSceneAsync(string sceneName)
    {
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        // 延迟激活新场景
        asyncOp.allowSceneActivation = false;
        Debug.Log("开始异步加载场景 " + sceneName);

        // 等待加载进度达到 0.9（Unity 加载新场景时最大值为0.9）
        while (asyncOp.progress < 0.9f)
        {
            Debug.Log("场景加载进度: " + asyncOp.progress);
            yield return null;
        }
        Debug.Log("场景加载进度达 0.9，等待 SimulationPage 内部数据加载完成...");

        // 此处可根据 SimulationPage 内部数据加载情况来控制延迟时间
        // 示例中简单等待 1 秒
        yield return new WaitForSeconds(1f);

        // 允许激活新场景
        asyncOp.allowSceneActivation = true;
        yield return null;
    }
}
