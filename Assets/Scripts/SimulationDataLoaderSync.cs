// �ļ���SimulationDataLoaderSync.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class SimulationDataLoaderSync : MonoBehaviour
{
    [Header("��������·��")]
    public string simFolderPath;
    public string fileName = "records_for_sim.json";

    // ������ݣ��� Playback ʹ�ã�
    [NonSerialized] public List<Dictionary<string, SimulationAgent>> simulationSteps;
    [NonSerialized] public List<string> stepTimestamps;
    [NonSerialized] public List<DateTime> stepDateTimes;
    [NonSerialized] public List<List<NoteworthyEntry>> stepNoteworthy;

    // �¼���Playback ���ģ�
    public event Action OnDataLoaded;

    private bool finished;

    void Start()
    {
        Debug.Log("[SIMBOOT:S4][SYNC] DataLoaderSync Start()");
        if (string.IsNullOrEmpty(simFolderPath))
        {
            simFolderPath = (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
                ? GameManager.Instance.simPath
                : Path.Combine(Application.dataPath, "Sim");
        }
        Debug.Log("[SIMBOOT:S4][SYNC] Resolved simFolder=" + simFolderPath);

        string full = Path.Combine(simFolderPath, fileName);
        Debug.Log("[SIMBOOT:S4][SYNC] Will load (SYNC): " + full);

        StartCoroutine(LoadRoutine(full));
    }

    void Update()
    {
    }

    private IEnumerator LoadRoutine(string fullPath)
    {
        Debug.Log("[SIMBOOT:S4][SYNC] LoadRoutine begin (mainThread=1)");

        if (!File.Exists(fullPath))
        {
            Debug.LogError("[SIMBOOT:S4][SYNC][ERR] File not found: " + fullPath);
            yield break;
        }

        var fi = new FileInfo(fullPath);
        Debug.Log($"[SIMBOOT:S4][SYNC] File exists. Size={fi.Length} bytes, LastWrite={fi.LastWriteTime}");

        byte[] bytes = File.ReadAllBytes(fullPath);
        if (bytes.Length >= 64)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64 * 3);
            for (int i = 0; i < 64; i++) sb.Append(bytes[i].ToString("X2")).Append(' ');
            Debug.Log("[SIMBOOT:S4][SYNC] First 64 bytes (hex): " + sb.ToString());
        }
        yield return null; // ��һ֡�����⿨���߳�

        string jsonText = System.Text.Encoding.UTF8.GetString(bytes);

        JObject root;
        try
        {
            root = JObject.Parse(jsonText);
        }
        catch (Exception ex)
        {
            Debug.LogError("[SIMBOOT:S4][SYNC][ERR] JSON parse failed: " + ex);
            yield break;
        }

        // ��ʼ������
        simulationSteps = new List<Dictionary<string, SimulationAgent>>(4096);
        stepTimestamps = new List<string>(4096);
        stepDateTimes = new List<DateTime>(4096);
        stepNoteworthy = new List<List<NoteworthyEntry>>(4096);

        // ���� key��ʱ���������
        var keys = new List<string>();
        foreach (var prop in root.Properties()) keys.Add(prop.Name);
        keys.Sort(StringComparer.Ordinal); // ��� key �� ISO-8601 �ַ������ֵ��򼴿�

        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        int yielded = 0;
        int built = 0;

        foreach (var key in keys)
        {
            var token = root[key];
            if (token == null || token.Type != JTokenType.Object)
            {
                Debug.LogWarning($"[SIMBOOT:S4][SYNC] Value of key={key} is not an object, skip.");
                continue;
            }

            var stepObj = (JObject)token;

            // 1) noteworthy����� JSON ���ֶ��� "event"��
            List<NoteworthyEntry> noteworthyList = null;
            var noteworthyToken = stepObj["noteworthy"];
            if (noteworthyToken is JArray na)
            {
                noteworthyList = new List<NoteworthyEntry>(na.Count);
                foreach (var n in na)
                {
                    try
                    {
                        var e = new NoteworthyEntry();
                        // ������ݼ��� "event"
                        e.eventText = n.Value<string>("event") ?? "";
                        var peopleTok = n["people"];
                        if (peopleTok is JArray ja)
                        {
                            var arr = new List<string>(ja.Count);
                            foreach (var p in ja) arr.Add(p.ToString());
                            e.people = arr.ToArray();
                        }
                        else e.people = Array.Empty<string>();
                        noteworthyList.Add(e);
                    }
                    catch { /* �����ݴ� */ }
                }
            }

            // 2) ���조agent �ֵ䡱��һ��
            JObject agentsDict = null;

            // 2.1 ������� agents ������������Է���������Դ��
            var agentsTok = stepObj["agents"];
            if (agentsTok is JObject ao)
            {
                agentsDict = ao;
            }
            else
            {
                // 2.2 ���򣺱��������һ���������������� noteworthy��meta������������ͼ�����Ϊһ�� agent
                agentsDict = new JObject();
                foreach (var p in stepObj.Properties())
                {
                    var name = p.Name;
                    if (name == "noteworthy" || name == "meta") continue;

                    if (p.Value is JObject)
                    {
                        agentsDict[name] = p.Value;
                    }
                    else
                    {
                        // ���ﲻ�ٰ� "name/location/curr_tile" �� agent
                        Debug.LogWarning($"[SIMBOOT:S4][SYNC] Property '{name}' under step '{key}' is not an agent object, ignore.");
                    }
                }
            }

            // 3) ������������ agent
            var stepDict = new Dictionary<string, SimulationAgent>(Math.Max(agentsDict.Count, 1));
            foreach (var ap in agentsDict.Properties())
            {
                var agentName = ap.Name;
                var aTok = ap.Value as JObject;
                if (aTok == null) continue;

                try
                {
                    var ag = new SimulationAgent();

                    ag.name = aTok.Value<string>("name") ?? agentName;

                    // �
                    ag.short_activity = aTok.Value<string>("short_activity") ?? aTok.Value<string>("shortActivity");
                    ag.activity = aTok.Value<string>("activity") ?? ag.short_activity ?? "";

                    // ��ǰλ�ã�����
                    int cx = 0, cy = 0;
                    var ct = aTok["curr_tile"] ?? aTok["currTile"];
                    if (ct is JArray cta && cta.Count >= 2)
                    {
                        cx = cta[0].Value<int>();
                        cy = cta[1].Value<int>();
                    }
                    ag.curr_tile = new int[] { cx, cy };

                    // λ��������**�ַ���**����ʾ�� JSON һ�£�
                    ag.location = aTok.Value<string>("location");

                    // ���� ������bag ������֧�� ["item"] �� [["item","uuid"], ...] ���ָ�ʽ�� ����
                    var bagTok = aTok["bag"];
                    if (bagTok is JArray bagArr)
                    {
                        var list = new List<string>(bagArr.Count);
                        foreach (var it in bagArr)
                        {
                            if (it is JArray inner && inner.Count > 0)
                                list.Add(inner[0]?.ToString() ?? "");
                            else
                                list.Add(it?.ToString() ?? "");
                        }
                        // ����հ���
                        list.RemoveAll(s => string.IsNullOrWhiteSpace(s));
                        ag.bag = list.Count > 0 ? list.ToArray() : Array.Empty<string>();
                    }
                    else
                    {
                        ag.bag = Array.Empty<string>();
                    }

                    stepDict[agentName] = ag;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SIMBOOT:S4][SYNC] Parse agent '{agentName}' under '{key}' failed: {ex.Message}");
                }
            }

            if (stepDict.Count == 0)
            {
                Debug.LogWarning($"[SIMBOOT:S4][SYNC] Step '{key}' has no valid agents, skip.");
                continue;
            }

            // 4) ʱ����б�
            stepTimestamps.Add(key.Replace(':', '_')); // ���첽�汣��һ��
            if (DateTime.TryParse(key, out var dt))
                stepDateTimes.Add(dt);
            else
                stepDateTimes.Add(DateTime.MinValue);

            simulationSteps.Add(stepDict);
            stepNoteworthy.Add(noteworthyList ?? new List<NoteworthyEntry>());

            built++;

            // ���ļ��ó����߳�
            if (built % 500 == 0)
            {
                yielded++;
                Debug.Log($"[SIMBOOT:S4][SYNC] Build progress: {built}/{keys.Count} (lastKey='{key}')");
                yield return null;
            }
        }

        sw.Stop();
        Debug.Log($"[SIMBOOT:S4][SYNC] Steps built (sync) = {simulationSteps.Count}, yielded={yielded} slices, total={sw.ElapsedMilliseconds} ms");

        Debug.Log("[SIMBOOT:S4][SYNC] About to invoke OnDataLoaded (main thread)");
        OnDataLoaded?.Invoke();
        Debug.Log("[SIMBOOT:S4][SYNC] OnDataLoaded invoked OK");

        finished = true;
        Debug.Log("[SIMBOOT:S4][SYNC] LoadRoutine end");
    }
}
