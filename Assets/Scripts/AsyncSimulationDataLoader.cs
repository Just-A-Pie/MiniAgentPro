using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Linq;

public class AsyncSimulationDataLoader : MonoBehaviour
{
    /*���������������������������������������� Inspector ��������������������������������������*/
    public string simFolderPath;                      // sim �ļ���·��

    /*���������������� ������ɺ��ṩ�����Ľ�� ����������������*/
    public List<Dictionary<string, SimulationAgent>> simulationSteps;
    public List<string> stepTimestamps;              // ԭʱ������ַ�����
    public List<DateTime> stepDateTimes;              // �� ͬ������Ϊ DateTime

    public Action OnDataLoaded;                       // ������ɻص�

    /*���������������������������������������� Start ��������������������������������������*/
    private void Start()
    {
        if (string.IsNullOrEmpty(simFolderPath))
        {
            simFolderPath = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
                            ? GameManager.Instance.simPath
                            : Path.Combine(Application.dataPath, "Sim");
        }

        string fullPath = Path.Combine(simFolderPath, "records_for_sim.json");
        Debug.Log("Async Loader ʹ�õ� sim �ļ���·��: " + simFolderPath);

        LoadSimulationData(fullPath);
    }

    /*������������������������ �������첽��ȡ ������������������������*/
    private async void LoadSimulationData(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("�޷��ҵ����������ļ���" + filePath);
            return;
        }

        string jsonData = await Task.Run(() => File.ReadAllText(filePath));

        var allSteps = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, SimulationAgent>>>(jsonData);

        /*������ �� ����ͬʱ������� List ������*/
        var orderedKeys = allSteps.Keys.OrderBy(k => ParseTimeFromFileName(k)).ToList();

        simulationSteps = new List<Dictionary<string, SimulationAgent>>();
        stepTimestamps = new List<string>();
        stepDateTimes = new List<DateTime>();                   // ��

        foreach (string ts in orderedKeys)
        {
            simulationSteps.Add(allSteps[ts]);
            stepTimestamps.Add(ts);
            stepDateTimes.Add(ParseTimeFromFileName(ts));         // ��
        }
        /*������������������������������������������������������������������������*/

        OnDataLoaded?.Invoke();
    }

    /*�������������������������������� �ļ���ʱ����� ��������������������������������*/
    private DateTime ParseTimeFromFileName(string fileNameWithoutExtension)
    {
        // �ļ�����ʽ "HH_MM_SS am" �� "HH_MM_SS pm"
        string[] parts = fileNameWithoutExtension.Split(' ');
        if (parts.Length < 2) return DateTime.Today;

        string[] t = parts[0].Split('_');             // HH MM SS
        if (t.Length < 3) return DateTime.Today;

        int h = int.Parse(t[0]);
        int m = int.Parse(t[1]);
        int s = int.Parse(t[2]);
        string period = parts[1].ToLower();

        if (period == "pm" && h < 12) h += 12;
        else if (period == "am" && h == 12) h = 0;

        return new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, h, m, s);
    }
}
