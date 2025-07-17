using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;  // ��ȷ���ѵ��� Newtonsoft.Json
using System;
using System.Linq;

public class SimulationDataReader
{
    /// <summary>
    /// ��ȡָ��·�������� JSON �ļ��������ļ�����ʱ������������ÿ�� step �� agent ���ݣ�ͬʱ��ȱʧ��Ϣ���в�ȫ��
    /// </summary>
    /// <param name="simFolderPath">sim �ļ���·��</param>
    /// <returns>List��ÿ��Ԫ��Ϊһ��ʱ�䲽�����ݣ�����Ϊ Dictionary&lt;string, SimulationAgent&gt;</returns>
    // ��ȡ�ϲ���ķ��������ļ���������ÿ��ʱ���������
    public List<Dictionary<string, SimulationAgent>> ReadAllSteps(string simFilePath)
    {
        List<Dictionary<string, SimulationAgent>> steps = new List<Dictionary<string, SimulationAgent>>();

        try
        {
            // ��ȡ������ JSON �ļ�
            Debug.Log($"Reading simulation data from: {simFilePath}");
            string json = File.ReadAllText(simFilePath);
            Debug.Log("JSON data loaded successfully.");

            // �����л�Ϊһ���ֵ䣬��Ϊʱ�����ֵΪ��ʱ����µĴ�����Ϣ
            Dictionary<string, Dictionary<string, SimulationAgent>> allSteps =
                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, SimulationAgent>>>(json);

            if (allSteps == null)
            {
                Debug.LogError("Failed to deserialize the simulation data. The data is null.");
                return steps;
            }

            // ����ʱ���
            var timestamps = allSteps.Keys.ToList();
            timestamps.Sort(); // ����ʱ�����������
            Debug.Log($"Loaded {timestamps.Count} timestamps. Sorting complete.");

            // ��ÿ��ʱ����µ�����ת��Ϊ��������
            foreach (var timestamp in timestamps)
            {
                Debug.Log($"Processing timestamp: {timestamp}");
                Dictionary<string, SimulationAgent> stepData = allSteps[timestamp];
                steps.Add(stepData);
            }

            Debug.Log($"Total steps loaded: {steps.Count}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Error reading simulation file: " + ex.Message);
        }

        return steps;
    }



    /// <summary>
    /// ���ļ����н���ʱ����Ϣ���ļ�����ʽʾ����"06_00_00 am" �� "01_00_00 pm"
    /// </summary>
    /// <param name="fileNameWithoutExtension">�ļ���������չ����</param>
    /// <returns>ת��Ϊ DateTime ���ͣ����ڲ���Ĭ��Ϊ���죩</returns>
    private DateTime ParseTimeFromFileName(string fileNameWithoutExtension)
    {
        // �����ļ�����ʽΪ "HH_MM_SS am" �� "HH_MM_SS pm"
        // �Ȱ��ո���
        string[] parts = fileNameWithoutExtension.Split(' ');
        if (parts.Length < 2)
        {
            Debug.LogWarning("�ļ�����ʽ����: " + fileNameWithoutExtension);
            return DateTime.Today;
        }
        string timePart = parts[0]; // "HH_MM_SS"
        string period = parts[1].ToLower(); // "am" �� "pm"

        string[] timeParts = timePart.Split('_');
        if (timeParts.Length < 3)
        {
            Debug.LogWarning("ʱ���ʽ����: " + timePart);
            return DateTime.Today;
        }
        int hour = int.Parse(timeParts[0]);
        int minute = int.Parse(timeParts[1]);
        int second = int.Parse(timeParts[2]);

        // ���� period ת��Ϊ24Сʱ��
        if (period == "pm" && hour < 12)
        {
            hour += 12;
        }
        else if (period == "am" && hour == 12)
        {
            hour = 0;
        }

        DateTime dt = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, hour, minute, second);
        return dt;
    }
}
