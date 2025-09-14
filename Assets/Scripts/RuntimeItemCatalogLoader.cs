using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class RuntimeItemCatalogLoader : MonoBehaviour
{
    // ====== ɨ���봴������ ======
    [Header("ɨ������")]
    [Tooltip("��Դ��·����Ҫɨ���һ��Ŀ¼")]
    public string[] rootFolders = new[] { "objects", "buildings" };

    [Tooltip("����ƷĿ¼��Ҫ���Ե��ļ�������˳�����ȼ���")]
    public string[] candidateFileNames = new[] { "texture.png", "thumbnail.png", "thumb.png" };

    [Tooltip("���� Sprite ʹ�õ�����/��λ")]
    public float pixelsPerUnit = 100f;

    // ====== Hook ���� ======
    [Header("Hook ����")]
    [Tooltip("�Ƿ�ǿ�Ƹ��� SimulationAgentRenderer �� bagItemToSprite �����ص����Ƽ����ϣ�")]
    public bool overrideRendererResolver = true;

    // ====== ��־ ======
    [Header("��־")]
    [Tooltip("�Ƿ���� BAGDBG ������־")]
    public bool verboseLogs = true;

    private const string BAGDBG = "[BAGDBG]";

    // ====== ������Ŀ¼����̬���ɸ��ⲿֱ���ã� ======
    public static readonly Dictionary<string, Sprite> Catalog = new Dictionary<string, Sprite>();

    // ====== ͳһ��һ������Сд��ȥ��ո��»�����ո�ͨ�� ======
    public static string Norm(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        s = s.Replace('_', ' ');
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s;
    }

    // �����������ļ򵥱���
    private static IEnumerable<string> Variants(string k)
    {
        yield return k;
        if (k.EndsWith("ies")) yield return k[..^3] + "y"; // diaries -> diary
        if (k.EndsWith("es")) yield return k[..^2];       // boxes   -> box
        if (k.EndsWith("s")) yield return k[..^1];       // notebooks -> notebook
    }

    // ���������������Ʒ�����Է��� Sprite���ϸ�������ģ����
    public static Sprite Resolve(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return null;

        string k = Norm(rawName);

        // 1) �ϸ�
        if (Catalog.TryGetValue(k, out var sp)) return sp;

        // 2) ��������
        foreach (var v in Variants(k))
            if (Catalog.TryGetValue(v, out sp)) return sp;

        // 3) ģ�������� / ǰ׺��
        foreach (var kv in Catalog)
            if (kv.Key.Contains(k) || k.Contains(kv.Key) || kv.Key.StartsWith(k))
                return kv.Value;

        return null;
    }

    private void Start()
    {
        if (verboseLogs) Debug.Log($"{BAGDBG} RuntimeItemCatalogLoader.Start begin");

        // 1) ɨ����̹���Ŀ¼
        BuildCatalogFromDisk();

        // 2) �ӹ� SimulationAgentRenderer �Ľ����ص�
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

            // ��Ŀ¼��Ϊ��Ʒ��
            foreach (var itemDir in Directory.GetDirectories(dir))
            {
                string itemName = Path.GetFileName(itemDir) ?? "";
                string normKey = Norm(itemName);

                // ��һ�����ڵ��ļ�
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
