using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.Library;

namespace Awake;

/// <summary>
/// NPC 全身像的本地缓存。
///
/// **幂等键就是文件名的全部依据。** 同一个 NPC ＋ 同一段提示词 ＋ 同一组尺寸与种子 ⇒ 同一个文件名
/// ⇒ 天然命中：不重复出图、不必再维护一张"谁生成过什么"的表、离线也能看。
/// 玩家改了提示词 ⇒ 键变 ⇒ 自然是一张新图，旧图仍留在盘上。
///
/// 位置走游戏官方的 Application 数据桶（&lt;ProgramData&gt;/&lt;应用名&gt;/Awake/AwakePortraits）。
/// 为什么不自拼绝对路径去喂引擎：写文件和读纹理必须共用同一套路径解析，
/// 两边算法一旦分叉，症状是"文件明明写了、纹理却是空的"这种最难查的形态。
/// 真实绝对目录在首次使用时记进 Awake.log（`portrait_cache_dir=…`），照着它放文件就能被读到。
/// </summary>
internal static class AwakePortraitCache
{
    private const string FolderName = "Awake/AwakePortraits";
    private const string FilePrefix = "portrait_";
    private const string FileExtension = ".png";
    private const int KeyHexLength = 32;

    private static readonly object Gate = new object();
    private static string loggedRoot;

    /// <summary>目录的官方坐标。文件助手与引擎读纹理都用它，别在别处另拼路径。</summary>
    internal static PlatformDirectoryPath Root
    {
        get { return new PlatformDirectoryPath(PlatformFileType.Application, FolderName); }
    }

    internal static string FileName(string key)
    {
        return FilePrefix + (key ?? string.Empty) + FileExtension;
    }

    /// <summary>交给平台文件助手／引擎的路径对象。</summary>
    internal static PlatformFilePath PathFor(string key)
    {
        return new PlatformFilePath(Root, FileName(key));
    }

    /// <summary>绝对路径。只用于日志、探测与目录浏览；喂引擎一律用 <see cref="PathFor"/>。</summary>
    internal static string AbsolutePathFor(string key)
    {
        return PathFor(key).FileFullPath;
    }

    /// <summary>绝对目录。</summary>
    internal static string AbsoluteRootDirectory
    {
        get
        {
            string probe = PathFor("probe").FileFullPath;
            return Path.GetDirectoryName(probe) ?? probe;
        }
    }

    /// <summary>首次使用时把真实目录记一次日志——这是"手放一张 png 也能被读到"的前提。</summary>
    internal static void LogRootOnce()
    {
        lock (Gate)
        {
            if (loggedRoot != null) return;
            try
            {
                loggedRoot = AbsoluteRootDirectory;
            }
            catch (Exception ex)
            {
                loggedRoot = null;
                AwakeLog.Write("portrait_cache_root_resolve_failed error=" + ex.Message);
                return;
            }
        }
        AwakeLog.Write("portrait_cache_dir=" + loggedRoot);
    }

    /// <summary>
    /// 幂等键：npcKey ＋ 提示词 ＋ 尺寸 ＋ 种子 的 SHA-256 前 128 bit。
    /// 用 0x1F 当分隔符（prompt 里不会出现），避免"a|b"与"a"+"|b" 这类边界拼接撞键。
    /// </summary>
    internal static string BuildKey(string npcKey, string prompt, int width, int height, long seed)
    {
        StringBuilder material = new StringBuilder();
        material.Append(Normalize(npcKey)).Append('\u001f')
                .Append(Normalize(prompt)).Append('\u001f')
                .Append(width.ToString(CultureInfo.InvariantCulture)).Append('x')
                .Append(height.ToString(CultureInfo.InvariantCulture)).Append('\u001f')
                .Append(seed.ToString(CultureInfo.InvariantCulture));

        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(material.ToString()));
            StringBuilder hex = new StringBuilder(KeyHexLength);
            for (int i = 0; i < digest.Length && hex.Length < KeyHexLength; i++)
            {
                hex.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return hex.ToString();
        }
    }

    /// <summary>命中判定：文件在、且长度大于 0。半张坏图不算命中。</summary>
    internal static bool TryResolveExisting(string key, out string absolutePath, out long byteLength)
    {
        absolutePath = null;
        byteLength = 0L;
        if (string.IsNullOrWhiteSpace(key)) return false;
        try
        {
            string path = AbsolutePathFor(key);
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0) return false;
            absolutePath = path;
            byteLength = info.Length;
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("portrait_cache_probe_failed key=" + key + " error=" + ex.Message);
            return false;
        }
    }

    /// <summary>落盘。成功返回绝对路径；失败返回 null 且**不留半张坏图**（重试天然可用）。</summary>
    internal static string Save(string key, byte[] pngBytes)
    {
        if (string.IsNullOrWhiteSpace(key) || pngBytes == null || pngBytes.Length == 0) return null;
        try
        {
            PlatformFilePath path = PathFor(key);
            SaveResult result = FileHelper.SaveFile(path, pngBytes);
            if (result != SaveResult.Success)
            {
                AwakeLog.Write("portrait_cache_save_failed key=" + key + " result=" + result
                    + " error=" + FileHelper.GetError());
                TryDelete(path);
                return null;
            }
            LogRootOnce();
            return path.FileFullPath;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("portrait_cache_save_error key=" + key + " error=" + ex.Message);
            return null;
        }
    }

    /// <summary>列出缓存里已有的键（探测面板用）。</summary>
    internal static List<string> ListKeys()
    {
        List<string> keys = new List<string>();
        try
        {
            string root = AbsoluteRootDirectory;
            if (!Directory.Exists(root)) return keys;
            string[] files = Directory.GetFiles(root, FilePrefix + "*" + FileExtension, SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                if (name == null || name.Length <= FilePrefix.Length) continue;
                keys.Add(name.Substring(FilePrefix.Length));
            }
            keys.Sort(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            AwakeLog.Write("portrait_cache_list_failed error=" + ex.Message);
        }
        return keys;
    }

    /// <summary>给人看的一行状态（探测面板用）。</summary>
    internal static string DescribeKey(string key)
    {
        string absolutePath;
        long byteLength;
        if (!TryResolveExisting(key, out absolutePath, out byteLength)) return "缺失";
        return byteLength.ToString(CultureInfo.InvariantCulture) + " B";
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static void TryDelete(PlatformFilePath path)
    {
        try
        {
            FileHelper.DeleteFile(path);
        }
        catch
        {
            // 删不掉也不该把失败原因盖掉，交给下一次重试覆盖写。
        }
    }
}
