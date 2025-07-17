using UnityEngine;
using System.Collections.Generic;

public class SimulationAgentTest : MonoBehaviour
{
    // �뽫������ SimulationAgentRenderer �ű��Ķ���ͨ������ mapContent �ϣ�������ֶ�
    public SimulationAgentRenderer agentRenderer;

    void Start()
    {
        // �Զ���ȡ sim �ļ���·��������ʹ�� GameManager.Instance.simPath������ʹ��Ĭ��·��
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

        // ʹ�� SimulationDataReader ��ȡ���� step ����
        SimulationDataReader reader = new SimulationDataReader();
        List<Dictionary<string, SimulationAgent>> steps = reader.ReadAllSteps(simFolderPath);
        if (steps.Count > 0)
        {
            // ʹ�õ�һ�����ݽ�����Ⱦ
            agentRenderer.RenderAgents(steps[0]);
        }
        else
        {
            Debug.LogWarning("δ��ȡ���κ� step ���ݣ�");
        }
    }
}
