// �ļ�: RootPath.cs
using System;
using System.IO;
using UnityEngine;

public static class RootPath
{
    /// <summary>
    /// ���ء�EXE ͬĿ¼��������·����
    /// - Editor �£�project ��Ŀ¼��Assets ����һ����
    /// - Player �£�Windows/Linux Ϊ exe ͬ��Ŀ¼��macOS Ϊ MyApp.app��Contents ����һ����
    /// </summary>
    public static string GetExeDir()
    {
#if UNITY_EDITOR
        // Editor��Assets ����һ������Ŀ����
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#else
        // Player��
        // Windows/Linux: dataPath �� *_Data ��β����Ŀ¼���� exe Ŀ¼
        // macOS: dataPath Ϊ *.app/Contents����Ŀ¼Ϊ *.app
        var parent = Directory.GetParent(Application.dataPath);
        return parent != null ? parent.FullName : Application.dataPath;
#endif
    }

    /// <summary>
    /// ����·�������㣺
    /// 1) ֧�� "root:/..."���������� '\' Ҳ�У���
    /// 2) ֧�� "./" �� "../" ���� �� GetExeDir() ��Ϊê�������
    /// 3) ��ͨ���·������ "sampleMap"��Ҳ��Ϊ��� root��
    /// 4) �Զ����� '/' �� '\'�����淶������������·����
    /// </summary>
    public static string Resolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        // ͳһ�ָ���Ϊ '/' �����ж������ջ��� Canonicalize ��ϵͳ���
        string s = raw.Trim().Replace('\\', '/');

        // 1) root:/ ǰ׺�������û�д root:\��������ͳһΪ root:/��
        const string scheme = "root:/";
        if (s.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            string tail = s.Substring(scheme.Length); // ���ܻ����� ./ �� ../
            string combined = Path.Combine(GetExeDir(), tail);
            return Canonicalize(combined);
        }

        // 2) ./ �� ../ => ʼ���� root��EXE ͬĿ¼��Ϊê��
        if (s.StartsWith("./", StringComparison.Ordinal) ||
            s.StartsWith("../", StringComparison.Ordinal))
        {
            string combined = Path.Combine(GetExeDir(), s);
            return Canonicalize(combined);
        }

        // 3) ���Ǿ���·������C:/..., /usr/..., //server/share��
        // ע�⣺.NET �� Windows ��Ҳ����ǰб�� "C:/..." �ж�Ϊ rooted
        if (Path.IsPathRooted(s))
        {
            return Canonicalize(s);
        }

        // 4) �������д������ "sampleMap"�� => ��Ϊ��� root ��·��
        return Canonicalize(Path.Combine(GetExeDir(), s));
    }

    /// <summary>
    /// �淶�������� '.'��'..'��������ϵͳ������·��
    /// </summary>
    private static string Canonicalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            // ��·�����Ƿ��ַ��ȵ��� GetFullPath ���쳣�����غϳ�ֵ�Ա����һ���쳣
            return path;
        }
    }
}
