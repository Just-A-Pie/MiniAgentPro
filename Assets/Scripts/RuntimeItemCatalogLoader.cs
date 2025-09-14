using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class RuntimeItemCatalogLoader : MonoBehaviour
{
    // ====== 扫描与创建参数 ======
    [Header("扫描设置")]
    [Tooltip("资源根路径下要扫描的一级目录")]
    public string[] rootFolders = new[] { "objects", "buildings" };

    [Tooltip("在物品目录下要尝试的文件名（按顺序优先级）")]
    public string[] candidateFileNames = new[] { "texture.png", "thumbnail.png", "thumb.png" };

    [Tooltip("创建 Sprite 使用的像素/单位")]
    public float pixelsPerUnit = 100f;

    // ====== Hook 设置 ======
    [Header("Hook 设置")]
    [Tooltip("是否强制覆盖 SimulationAgentRenderer 的 bagItemToSprite 解析回调（推荐勾上）")]
    public bool overrideRendererResolver = true;

    // ====== 日志 ======
    [Header("日志")]
    [Tooltip("是否输出 BAGDBG 调试日志")]
    public bool verboseLogs = true;

    private const string BAGDBG = "[BAGDBG]";

    // ====== 运行期目录（静态，可给外部直接用） ======
    public static readonly Dictionary<string, Sprite> Catalog = new Dictionary<string, Sprite>();

    // ====== 统一归一化规则（小写、去多空格、下划线与空格互通） ======
    public static string Norm(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        s = s.Replace('_', ' ');
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s;
    }

    // 常见单复数的简单变体
    private static IEnumerable<string> Variants(string k)
    {
        yield return k;
        if (k.EndsWith("ies")) yield return k[..^3] + "y"; // diaries -> diary
        if (k.EndsWith("es")) yield return k[..^2];       // boxes   -> box
        if (k.EndsWith("s")) yield return k[..^1];       // notebooks -> notebook
    }

    // 对外解析：给定物品名尝试返回 Sprite（严格→变体→模糊）
    public static Sprite Resolve(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;

        string k = Norm(rawName);

        // 1) 严格
        if (Catalog.TryGetValue(k, out var sp)) return sp;

        // 2) 常见变体
        foreach (var v in Variants(k))
            if (Catalog.TryGetValue(v, out sp)) return sp;

        // 3) 模糊（包含 / 前缀）
        foreach (var kv in Catalog)
            if (kv.Key.Contains(k) || k.Contains(kv.Key) || kv.Key.StartsWith(k))
                return kv.Value;

        return null;
    }

    private void Start()
    {
        if (verboseLogs) Debug.Log($"{BAGDBG} RuntimeItemCatalogLoader.Start begin");

        // 1) 扫描磁盘构建目录
        BuildCatalogFromDisk();

        // 2) 接管 SimulationAgentRenderer 的解析回调
        TryHookRenderer(force: overrideRendererResolver);

        if (verboseLogs) Debug.Log($"{BAGDBG} RuntimeItemCatalogLoader.Start end (Catalog.Count={Catalog.Count})");
    }

    private void TryHookRenderer(bool force)
    {
        var sar = SimulationAgentRenderer.Instance ?? FindObjectOfType<SimulationAgentRenderer>();
        if (sar == null)
        {
            if (verboseLogs) Debug.LogWarning($"{BAGDBG} SimulationAgentRenderer not found in scene.");
            return;
        }

        if (!force && sar.bagItemToSprite != null)
        {
            if (verboseLogs) Debug.Log($"{BAGDBG} Skip hook: bagItemToSprite already set and overrideRendererResolver=false");
            return;
        }

        sar.bagItemToSprite = (name) =>
        {
            var sp = Resolve(name);
            if (verboseLogs) Debug.Log($"{BAGDBG} Catalog resolve '{name}' -> {(sp != null ? "OK" : "NULL")}");
            return sp;
        };

        if (verboseLogs)
            Debug.Log($"{BAGDBG} Hooked bagItemToSprite (force={force})");
    }

    private void BuildCatalogFromDisk()
    {
        Catalog.Clear();

        var gm = GameManager.Instance;
        if (gm == null || string.IsNullOrEmpty(gm.resourcePath))
        {
            if (verboseLogs) Debug.LogWarning($"{BAGDBG} GameManager.resourcePath is NULL; skip disk scan.");
            return;
        }

        string root = gm.resourcePath;
        int added = 0, skipped = 0;

        foreach (var folder in rootFolders ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            string dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir))
            {
                if (verboseLogs) Debug.LogWarning($"{BAGDBG} Dir not found: '{folder}' under '{root}'");
                continue;
            }

            // 子目录作为物品名
            foreach (var itemDir in Directory.GetDirectories(dir))
            {
                string itemName = Path.GetFileName(itemDir) ?? "";
                string normKey = Norm(itemName);

                // 找一个存在的文件
                string hitPath = null;
                foreach (var fn in candidateFileNames ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(fn)) continue;
                    string test = Path.Combine(itemDir, fn);
                    if (File.Exists(test)) { hitPath = test; break; }
                }

                if (hitPath == null)
                {
                    if (verboseLogs) Debug.Log($"{BAGDBG} DISK MISS '{folder}/{itemName}/(texture|thumbnail|thumb).png'");
                    skipped++;
                    continue;
                }

                try
                {
                    byte[] bytes = File.ReadAllBytes(hitPath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(bytes))
                    {
                        if (verboseLogs) Debug.LogWarning($"{BAGDBG} LoadImage failed: '{hitPath}'");
                        continue;
                    }

                    var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                            new Vector2(0.5f, 0.5f), pixelsPerUnit);

                    if (!Catalog.ContainsKey(normKey))
                    {
                        Catalog[normKey] = sp;
                        added++;
                        if (verboseLogs)
                            Debug.Log($"{BAGDBG} DISK HIT '{folder}/{itemName}/{Path.GetFileName(hitPath)}' -> key='{normKey}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{BAGDBG} Read fail '{hitPath}': {ex.Message}");
                }
            }
        }

        if (verboseLogs) Debug.Log($"{BAGDBG} Catalog built. added={added} skipped(no png)={skipped} total={Catalog.Count}");
    }
}
