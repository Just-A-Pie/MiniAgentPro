using UnityEngine;
using System.Collections.Generic;

public class SimulationDataReaderTest : MonoBehaviour
{
    void Start()
    {
        // 自动使用 GameManager.Instance.simPath 或默认路径
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

        SimulationDataReader reader = new SimulationDataReader();
        List<Dictionary<string, SimulationAgent>> steps = reader.ReadAllSteps(simFolderPath);
    }
}
