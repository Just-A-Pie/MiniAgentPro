// 文件: AsyncSimulationDataLoader.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class AsyncSimulationDataLoader : MonoBehaviour
{
    /*──────────────────── Inspector ───────────────────*/
    [Header("仿真数据根目录（留空=GameManager.simPath 或 Assets/Sim）")]
    public string simFolderPath; // sim 文件夹路径

    /*──────── 加载完成后提供给外界的结果 ────────*/
    [NonSerialized] public List<Dictionary<string, SimulationAgent>> simulationSteps;
    [NonSerialized] public List<string> stepTimestamps;               // 原时间戳（字符串）
    [NonSerialized] public List<DateTime> stepDateTimes;               // DateTime
    [NonSerialized] public List<List<NoteworthyEntry>> stepNoteworthy; // 每步的 noteworthy 列表

    public Action OnDataLoaded; // 加载完成回调（在主线程触发）

    /*──────────────── 内部自派发 ───────────────*/
    private volatile bool _pendingApply = false;
    private ParseResult _pendingResult = null;

    // 看门狗 & 心跳
    private float _startTime;
    private bool _applied;
    private float _lastBeat;
    private const float WatchdogTimeout = 8f; // 秒

    private class ParseResult
    {
        public List<Dictionary<string, SimulationAgent>> Steps;
        public List<string> Timestamps;
        public List<DateTime> DateTimes;
        public List<List<NoteworthyEntry>> Noteworthy;
    }

    /*──────────────────── 生命周期：Start / Update ───────────────────*/
    private void Start()
    {
        _startTime = Time.realtimeSinceStartup;
        Debug.Log("[SIMBOOT:S4][Start] DataLoader Start()");
        if (string.IsNullOrEmpty(simFolderPath))
        {
            simFolderPath =
                (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.simPath))
                ? GameManager.Instance.simPath
                : Path.Combine(Application.dataPath, "Sim");
        }

        try { simFolderPath = RootPath.Resolve(simFolderPath) ?? simFolderPath; }
        catch { /* 项目没有 RootPath 时忽略 */ }

        Debug.Log("[SIMBOOT:S4][Start] Resolved simFolder=" + simFolderPath);

        string fullPath = Path.Combine(simFolderPath, "records_for_sim.json");
        Debug.Log("[SIMBOOT:S4][Start] Will load: " + fullPath);

        LoadSimulationData(fullPath);
    }

    private void Update()
    {
        // 轻量心跳：每 2s 打一次，帮助确认 Update 在跑
        if (Time.realtimeSinceStartup - _lastBeat > 2f && !_applied)
        {
            _lastBeat = Time.realtimeSinceStartup;
            Debug.Log($"[SIMBOOT:S4][Beat] Update alive; pending={_pendingApply}, applied={_applied}");
        }

        // 看门狗：超过阈值未应用 -> 提示排查方向
        if (!_applied && Time.realtimeSinceStartup - _startTime > WatchdogTimeout)
        {
            Debug.LogError("[SIMERR:S4][Watchdog] Data not applied > " + WatchdogTimeout + "s. " +
                           "检查：1) 此脚本所在 GameObject 是否 Active & Enabled？" +
                           " 2) 是否有多份同名 Loader 且这份的 Update 没跑？" +
                           " 3) 是否 TimeScale=0 但没跑 Update（本组件用 realtimeSinceStartup，理论仍应跑）？" +
                           " 4) Console 是否有异常导致主线程卡住？");
            // 只报一次
            _startTime = float.MaxValue;
        }

        // 主线程消费后台结果
        if (_pendingApply && _pendingResult != null)
        {
            _pendingApply = false; // 先清标志避免重入
            try
            {
                ApplyResultOnMainThread(_pendingResult);
                _applied = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SIMERR:S4][ApplyResultOnMainThread] Exception: " + ex);
            }
            finally
            {
                _pendingResult = null;
            }
        }
    }

    /*──────────────── 在主线程应用构建结果 & 触发回调 ─────────────────*/
    private void ApplyResultOnMainThread(ParseResult r)
    {
        Debug.Log("[SIMBOOT:S4][Apply] Applying results on main thread=" + System.Threading.Thread.CurrentThread.ManagedThreadId);

        this.simulationSteps = r.Steps;
        this.stepTimestamps = r.Timestamps;
        this.stepDateTimes = r.DateTimes;
        this.stepNoteworthy = r.Noteworthy;

        try
        {
            Debug.Log("[SIMBOOT:S4][Apply] About to invoke OnDataLoaded …");
            OnDataLoaded?.Invoke();
            Debug.Log("[SIMBOOT:S4][Apply] OnDataLoaded invoked OK");
        }
        catch (Exception exCb)
        {
            Debug.LogError("[SIMERR:S4][Apply] OnDataLoaded callback threw: " + exCb);
        }
    }

    /*──────────── 真正的异步读取 + 后台解析构建（稳态队列版） ────────────*/
    private async void LoadSimulationData(string filePath)
    {
        var callerTid = System.Threading.Thread.CurrentThread.ManagedThreadId;
        Debug.Log($"[SIMBOOT:S4][Load] LoadSimulationData -> {filePath} (callerThread={callerTid})");

        if (!File.Exists(filePath))
        {
            Debug.LogError("[SIMERR:S4][Load] Missing file: " + filePath);
            return;
        }

        try
        {
            var fi = new FileInfo(filePath);
            Debug.Log($"[SIMBOOT:S4][Load] File exists. Size={fi.Length} bytes, LastWrite={fi.LastWriteTime}");
        }
        catch (Exception exInfo)
        {
            Debug.LogWarning("[SIMBOOT:S4][Load] FileInfo failed: " + exInfo.Message);
        }

        /* ===== PHASE-A：原始字节分块读取 ===== */
        byte[] allBytes = null;
        var swRead = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            allBytes = await Task.Run(() =>
            {
                int tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Debug.Log("[SIMBOOT:S4][A] RAW-Read Task started on thread=" + tid);

                const int chunkSize = 1 * 1024 * 1024;
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                              bufferSize: 64 * 1024, options: FileOptions.SequentialScan);

                long len = fs.Length;
                if (len > int.MaxValue)
                {
                    Debug.LogError("[SIMERR:S4][A] File too large (>2GB).");
                    return null;
                }

                var buf = new byte[len];
                int off = 0;
                Debug.Log($"[SIMBOOT:S4][A] About to first read… len={len} bytes");
                while (off < len)
                {
                    int toRead = Math.Min(chunkSize, (int)(len - off));
                    int n = fs.Read(buf, off, toRead);
                    if (n <= 0) break;
                    off += n;
                }
                Debug.Log($"[SIMBOOT:S4][A] Read loop finished. TotalRead={off} bytes");
                return buf;
            }).ConfigureAwait(false);
        }
        catch (Exception exRead)
        {
            Debug.LogError("[SIMERR:S4][A] RAW read failed: " + exRead);
            return;
        }
        swRead.Stop();
        if (allBytes == null)
        {
            Debug.LogError("[SIMERR:S4][A] allBytes == null");
            return;
        }
        Debug.Log($"[SIMBOOT:S4][A] RAW read ok. bytes={allBytes.Length} in {swRead.ElapsedMilliseconds} ms");

        /* ===== PHASE-B：后台解析/构建 =====
         * 关键：在 BG 线程内部完成后，直接把结果“排队到主线程”（设置 _pendingResult/_pendingApply），
         * 不依赖 await 之后的续体。
         */
        var swPipeline = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await Task.Run(() =>
            {
                int bgTid = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Debug.Log("[SIMBOOT:S4][B] BG pipeline thread=" + bgTid);

                // B-1: HEX 预览
                int preview = Math.Min(64, allBytes.Length);
                var hex = new System.Text.StringBuilder(preview * 3);
                for (int i = 0; i < preview; i++) hex.Append(allBytes[i].ToString("X2")).Append(' ');
                Debug.Log("[SIMBOOT:S4][B] First 64 bytes (hex): " + hex.ToString());

                // B-2: 编码判定 + 解码
                System.Text.Encoding enc = System.Text.Encoding.UTF8;
                if (allBytes.Length >= 2 && allBytes[0] == 0xFF && allBytes[1] == 0xFE) enc = System.Text.Encoding.Unicode;
                else if (allBytes.Length >= 2 && allBytes[0] == 0xFE && allBytes[1] == 0xFF) enc = System.Text.Encoding.BigEndianUnicode;
                else if (allBytes.Length >= 3 && allBytes[0] == 0xEF && allBytes[1] == 0xBB && allBytes[2] == 0xBF) enc = new System.Text.UTF8Encoding(true);

                var swDecode = System.Diagnostics.Stopwatch.StartNew();
                string jsonText = enc.GetString(allBytes);
                swDecode.Stop();
                Debug.Log($"[SIMBOOT:S4][B] Decode ok. encoding={enc.WebName} chars={jsonText.Length} in {swDecode.ElapsedMilliseconds} ms");

                // B-3: Parse
                var swParse = System.Diagnostics.Stopwatch.StartNew();
                JObject root = JObject.Parse(jsonText);
                swParse.Stop();
                Debug.Log("[SIMBOOT:S4][B] JSON parsed ok in " + swParse.ElapsedMilliseconds + " ms");

                // B-4: 收集
                var tempList = new List<(string key, JObject value)>();
                int propCount = 0;
                foreach (var prop in root.Properties())
                {
                    propCount++;
                    if (prop.Value is JObject jobj) tempList.Add((prop.Name, jobj));
                }
                Debug.Log($"[SIMBOOT:S4][B] Top-level properties={propCount}, stepsDetected={tempList.Count}");

                // B-5: 排序
                var swSort = System.Diagnostics.Stopwatch.StartNew();
                var ordered = tempList.OrderBy(p => ParseTimeFlexible(p.key)).ToList();
                swSort.Stop();
                Debug.Log($"[SIMBOOT:S4][B] Steps sorted. Count={ordered.Count} in {swSort.ElapsedMilliseconds} ms");

                // B-6: 构建 + 进度
                var outSteps = new List<Dictionary<string, SimulationAgent>>(Math.Max(ordered.Count, 1));
                var outTs = new List<string>(Math.Max(ordered.Count, 1));
                var outDt = new List<DateTime>(Math.Max(ordered.Count, 1));
                var outNW = new List<List<NoteworthyEntry>>(Math.Max(ordered.Count, 1));

                var swBuild = System.Diagnostics.Stopwatch.StartNew();
                int stepIdx = 0;
                foreach (var (key, obj) in ordered)
                {
                    var agentsDict = new Dictionary<string, SimulationAgent>();
                    var noteworthyList = new List<NoteworthyEntry>();

                    foreach (var child in obj.Properties())
                    {
                        if (child.Name == "noteworthy")
                        {
                            if (child.Value is JArray arr)
                            {
                                foreach (var evToken in arr)
                                {
                                    if (evToken is JObject evObj)
                                    {
                                        string evText = evObj["event"]?.ToString();
                                        string[] people = evObj["people"] is JArray ppl
                                            ? ppl.Select(p => p.ToString()).ToArray()
                                            : Array.Empty<string>();
                                        if (!string.IsNullOrEmpty(evText))
                                            noteworthyList.Add(new NoteworthyEntry { eventText = evText, people = people });
                                    }
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                var ag = child.Value.ToObject<SimulationAgent>();
                                if (ag != null)
                                {
                                    if (string.IsNullOrEmpty(ag.name)) ag.name = child.Name;
                                    agentsDict[child.Name] = ag;
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning($"[SIMBOOT:S4][B] Parse agent '{child.Name}' failed: {e.Message}");
                            }
                        }
                    }

                    outSteps.Add(agentsDict);
                    outTs.Add(key);
                    outDt.Add(ParseTimeFlexible(key));
                    outNW.Add(noteworthyList);

                    stepIdx++;
                    if (stepIdx % 100 == 0)
                        Debug.Log($"[SIMBOOT:S4][B] Build progress: {stepIdx}/{ordered.Count} (lastKey='{key}', agents={agentsDict.Count}, noteworthy={noteworthyList.Count})");
                }
                swBuild.Stop();
                Debug.Log($"[SIMBOOT:S4][B] Steps built: {outSteps.Count} in {swBuild.ElapsedMilliseconds} ms");

                // B-7: 直接在 BG 线程里排队到主线程（不等 await 后续）
                _pendingResult = new ParseResult
                {
                    Steps = outSteps,
                    Timestamps = outTs,
                    DateTimes = outDt,
                    Noteworthy = outNW
                };
                _pendingApply = true;
                Debug.Log("[SIMPHASE:S4][C0] Result queued for main-thread apply (from BG thread)");
            }).ConfigureAwait(false);
        }
        catch (Exception exBg)
        {
            Debug.LogError("[SIMERR:S4][B] BG pipeline failed: " + exBg);
            return;
        }
        swPipeline.Stop();
        // 注意：就算这行以后不执行（续体丢失），C0 也已经把结果排队了
        Debug.Log("[SIMPHASE:S4][B] BG pipeline done in " + swPipeline.ElapsedMilliseconds + " ms (post-await)");
    }

    /*──────────────── 时间解析（兼容 ISO 与旧文件名） ────────────────*/
    private DateTime ParseTimeFlexible(string key)
    {
        if (DateTime.TryParse(key, null, System.Globalization.DateTimeStyles.AssumeLocal, out var dt1))
            return dt1;

        string[] parts = key.Split(' ');
        if (parts.Length >= 2)
        {
            string[] t = parts[0].Split('_');
            if (t.Length >= 3 &&
                int.TryParse(t[0], out int h) &&
                int.TryParse(t[1], out int m) &&
                int.TryParse(t[2], out int s))
            {
                string period = parts[1].ToLower();
                if (period == "pm" && h < 12) h += 12;
                else if (period == "am" && h == 12) h = 0;
                return new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, h, m, s);
            }
        }
        return DateTime.Today;
    }
}
