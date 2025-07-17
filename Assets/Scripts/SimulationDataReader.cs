using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;  // 请确保已导入 Newtonsoft.Json
using System;
using System.Linq;

public class SimulationDataReader
{
    /// <summary>
    /// 读取指定路径下所有 JSON 文件，并按文件名中时间排序后解析出每个 step 的 agent 数据，同时对缺失信息进行补全。
    /// </summary>
    /// <param name="simFolderPath">sim 文件夹路径</param>
    /// <returns>List，每个元素为一个时间步的数据，类型为 Dictionary&lt;string, SimulationAgent&gt;</returns>
    // 读取合并后的仿真数据文件，并返回每个时间戳的数据
    public List<Dictionary<string, SimulationAgent>> ReadAllSteps(string simFilePath)
    {
        List<Dictionary<string, SimulationAgent>> steps = new List<Dictionary<string, SimulationAgent>>();

        try
        {
            // 读取整个大 JSON 文件
            Debug.Log($"Reading simulation data from: {simFilePath}");
            string json = File.ReadAllText(simFilePath);
            Debug.Log("JSON data loaded successfully.");

            // 反序列化为一个字典，键为时间戳，值为该时间戳下的代理信息
            Dictionary<string, Dictionary<string, SimulationAgent>> allSteps =
                JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, SimulationAgent>>>(json);

            if (allSteps == null)
            {
                Debug.LogError("Failed to deserialize the simulation data. The data is null.");
                return steps;
            }

            // 排序时间戳
            var timestamps = allSteps.Keys.ToList();
            timestamps.Sort(); // 根据时间戳进行排序
            Debug.Log($"Loaded {timestamps.Count} timestamps. Sorting complete.");

            // 将每个时间戳下的数据转换为步骤数据
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
    /// 从文件名中解析时间信息。文件名格式示例："06_00_00 am" 或 "01_00_00 pm"
    /// </summary>
    /// <param name="fileNameWithoutExtension">文件名（无扩展名）</param>
    /// <returns>转换为 DateTime 类型（日期部分默认为今天）</returns>
    private DateTime ParseTimeFromFileName(string fileNameWithoutExtension)
    {
        // 假设文件名格式为 "HH_MM_SS am" 或 "HH_MM_SS pm"
        // 先按空格拆分
        string[] parts = fileNameWithoutExtension.Split(' ');
        if (parts.Length < 2)
        {
            Debug.LogWarning("文件名格式错误: " + fileNameWithoutExtension);
            return DateTime.Today;
        }
        string timePart = parts[0]; // "HH_MM_SS"
        string period = parts[1].ToLower(); // "am" 或 "pm"

        string[] timeParts = timePart.Split('_');
        if (timeParts.Length < 3)
        {
            Debug.LogWarning("时间格式错误: " + timePart);
            return DateTime.Today;
        }
        int hour = int.Parse(timeParts[0]);
        int minute = int.Parse(timeParts[1]);
        int second = int.Parse(timeParts[2]);

        // 根据 period 转换为24小时制
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
