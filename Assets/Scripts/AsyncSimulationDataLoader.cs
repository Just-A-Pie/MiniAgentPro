using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Linq;

public class AsyncSimulationDataLoader : MonoBehaviour
{
    /*──────────────────── Inspector ───────────────────*/
    public string simFolderPath;                      // sim 文件夹路径

    /*──────── 加载完成后提供给外界的结果 ────────*/
    public List<Dictionary<string, SimulationAgent>> simulationSteps;
    public List<string> stepTimestamps;              // 原时间戳（字符串）
    public List<DateTime> stepDateTimes;              // ★ 同步保存为 DateTime

    public Action OnDataLoaded;                       // 加载完成回调

    /*──────────────────── Start ───────────────────*/
    private void Start()
    {
        if (string.IsNullOrEmpty(simFolderPath))
        {
            simFolderPath = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
                            ? GameManager.Instance.simPath
                            : Path.Combine(Application.dataPath, "Sim");
        }

        string fullPath = Path.Combine(simFolderPath, "records_for_sim.json");
        Debug.Log("Async Loader 使用的 sim 文件夹路径: " + simFolderPath);

        LoadSimulationData(fullPath);
    }

    /*──────────── 真正的异步读取 ────────────*/
    private async void LoadSimulationData(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("无法找到仿真数据文件：" + filePath);
            return;
        }

        string jsonData = await Task.Run(() => File.ReadAllText(filePath));

        var allSteps = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, SimulationAgent>>>(jsonData);

        /*─── ★ 排序并同时填充三个 List ───*/
        var orderedKeys = allSteps.Keys.OrderBy(k => ParseTimeFromFileName(k)).ToList();

        simulationSteps = new List<Dictionary<string, SimulationAgent>>();
        stepTimestamps = new List<string>();
        stepDateTimes = new List<DateTime>();                   // ★

        foreach (string ts in orderedKeys)
        {
            simulationSteps.Add(allSteps[ts]);
            stepTimestamps.Add(ts);
            stepDateTimes.Add(ParseTimeFromFileName(ts));         // ★
        }
        /*────────────────────────────────────*/

        OnDataLoaded?.Invoke();
    }

    /*──────────────── 文件名时间解析 ────────────────*/
    private DateTime ParseTimeFromFileName(string fileNameWithoutExtension)
    {
        // 文件名格式 "HH_MM_SS am" 或 "HH_MM_SS pm"
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
