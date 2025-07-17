using UnityEngine;
using System.Collections.Generic;

public class SimulationDataReaderTest : MonoBehaviour
{
    void Start()
    {
        // �Զ�ʹ�� GameManager.Instance.simPath ��Ĭ��·��
        string simFolderPath = "";
        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
        {
            simFolderPath = GameManager.Instance.simPath;
        }
        else
        {
            simFolderPath = System.IO.Path.Combine(Application.dataPath, "Sim");
        }
        Debug.Log("ʹ�õ� sim �ļ���·��: " + simFolderPath);

        SimulationDataReader reader = new SimulationDataReader();
        List<Dictionary<string, SimulationAgent>> steps = reader.ReadAllSteps(simFolderPath);
    }
}
