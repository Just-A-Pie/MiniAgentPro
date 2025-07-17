using UnityEngine;
using System.Collections.Generic;

public class SimulationAgentTest : MonoBehaviour
{
    // 请将挂载了 SimulationAgentRenderer 脚本的对象（通常挂在 mapContent 上）拖入此字段
    public SimulationAgentRenderer agentRenderer;

    void Start()
    {
        // 自动获取 sim 文件夹路径：优先使用 GameManager.Instance.simPath，否则使用默认路径
        string simFolderPath = "";
        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
        {
            simFolderPath = GameManager.Instance.simPath;
        }
        else
        {
            simFolderPath = System.IO.Path.Combine(Application.dataPath, "Sim");
        }
        Debug.Log("使用的 sim 文件夹路径: " + simFolderPath);

        // 使用 SimulationDataReader 读取所有 step 数据
        SimulationDataReader reader = new SimulationDataReader();
        List<Dictionary<string, SimulationAgent>> steps = reader.ReadAllSteps(simFolderPath);
        if (steps.Count > 0)
        {
            // 使用第一步数据进行渲染
            agentRenderer.RenderAgents(steps[0]);
        }
        else
        {
            Debug.LogWarning("未读取到任何 step 数据！");
        }
    }
}
