// 文件: RootPath.cs
using System;
using System.IO;
using UnityEngine;

public static class RootPath
{
    /// <summary>
    /// 返回“EXE 同目录”的物理路径：
    /// - Editor 下：project 根目录（Assets 的上一级）
    /// - Player 下：Windows/Linux 为 exe 同级目录；macOS 为 MyApp.app（Contents 的上一级）
    /// </summary>
    public static string GetExeDir()
    {
#if UNITY_EDITOR
        // Editor：Assets 的上一级（项目根）
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#else
        // Player：
        // Windows/Linux: dataPath 以 *_Data 结尾，父目录就是 exe 目录
        // macOS: dataPath 为 *.app/Contents，父目录为 *.app
        var parent = Directory.GetParent(Application.dataPath);
        return parent != null ? parent.FullName : Application.dataPath;
#endif
    }

    /// <summary>
    /// 解析路径，满足：
    /// 1) 支持 "root:/..."（输入里用 '\' 也行）；
    /// 2) 支持 "./" 与 "../" —— 以 GetExeDir() 作为锚点解析；
    /// 3) 普通相对路径（如 "sampleMap"）也视为相对 root；
    /// 4) 自动兼容 '/' 与 '\'，并规范化到绝对物理路径。
    /// </summary>
    public static string Resolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        // 统一分隔符为 '/' 便于判定；最终会再 Canonicalize 成系统风格
        string s = raw.Trim().Replace('\\', '/');

        // 1) root:/ 前缀（允许用户写 root:\，上面已统一为 root:/）
        const string scheme = "root:/";
        if (s.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            string tail = s.Substring(scheme.Length); // 可能还包含 ./ 或 ../
            string combined = Path.Combine(GetExeDir(), tail);
            return Canonicalize(combined);
        }

        // 2) ./ 或 ../ => 始终以 root（EXE 同目录）为锚点
        if (s.StartsWith("./", StringComparison.Ordinal) ||
            s.StartsWith("../", StringComparison.Ordinal))
        {
            string combined = Path.Combine(GetExeDir(), s);
            return Canonicalize(combined);
        }

        // 3) 已是绝对路径？（C:/..., /usr/..., //server/share）
        // 注意：.NET 在 Windows 下也接受前斜杠 "C:/..." 判定为 rooted
        if (Path.IsPathRooted(s))
        {
            return Canonicalize(s);
        }

        // 4) 其它相对写法（如 "sampleMap"） => 视为相对 root 的路径
        return Canonicalize(Path.Combine(GetExeDir(), s));
    }

    /// <summary>
    /// 规范化：消解 '.'、'..'，并返回系统风格绝对路径
    /// </summary>
    private static string Canonicalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            // 若路径含非法字符等导致 GetFullPath 抛异常，返回合成值以避免进一步异常
            return path;
        }
    }
}
