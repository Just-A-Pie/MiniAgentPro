// 文件：AgentHistoryManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>全局保存每个 Agent 的最近 N 条活动历史（最新在最前）。</summary>
public class AgentHistoryManager : MonoBehaviour
{
    public static AgentHistoryManager Instance;

    private const int MaxEntriesPerAgent = 100;      // ★ 每人最多 100 条

    /// <summary>agentName → 倒序活动行列表</summary>
    private readonly Dictionary<string, List<string>> historyMap = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 追加一条记录  
    ///   • forceWrite = true   ⇒ 不比较文本，总是写入（Jump / 首帧）  
    ///   • forceWrite = false  ⇒ 若文本与最新一条相同则跳过（正常播放去重）
    /// </summary>
    public void AddRecord(string agentName,
                          string timeText,
                          string activity,
                          bool forceWrite = false)
    {
        if (string.IsNullOrEmpty(agentName)) return;

        // 初始化列表
        if (!historyMap.ContainsKey(agentName))
            historyMap[agentName] = new List<string>();

        List<string> list = historyMap[agentName];

        // ── ① “动作是否变化”判断 ─────────────────────────────
        if (!forceWrite && list.Count > 0)
        {
            // 记录格式: "08:00 ― 吃早餐\n"
            string lastLine = list[0];
            int idx = lastLine.IndexOf('―');
            string lastAct = (idx >= 0) ? lastLine[(idx + 1)..].Trim() : lastLine;
            if (lastAct == activity) return;   // 文本相同 ⇒ 不写
        }

        // ── ② 滑动窗口：已满则删除最旧一条 ──────────────────
        if (list.Count == MaxEntriesPerAgent)
            list.RemoveAt(list.Count - 1);

        // ── ③ 插入新行（最新在最前）────────────────────────
        string line = $"{timeText}  ―  {activity}\n";
        list.Insert(0, line);
    }

    /// <summary>取某 Agent 的完整倒序历史文本</summary>
    public string GetHistoryText(string agentName)
    {
        return historyMap.ContainsKey(agentName)
            ? string.Join("\n", historyMap[agentName]) : "(no record)";
    }

    /// <summary>清空全部历史（可选）</summary>
    public void ClearAll() => historyMap.Clear();
}
