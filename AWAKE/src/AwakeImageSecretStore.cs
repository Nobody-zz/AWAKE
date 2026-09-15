using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.Library;

namespace Awake;

/// <summary>
/// 出图 API Key 的本机保护存储（模组侧）。
///
/// **为什么模组要自己存一份**：AI 链路那把 Key 存在 Runtime 进程（net8）的保护存储里，
/// 模组进程（net472）既读不到、也没有回读接口。生图既然要在模组侧自己出网，
/// 就需要自己的一处落点。两处各存各的，槽位名不同（<c>awake.image.default</c>）。
///
/// 保护方式与框架侧同源：Windows DPAPI（<c>CurrentUser</c> 作用域）—— 换个用户账号解不开。
/// 明文只在本进程内存里存在一瞬；**不写 MCM、不进存档、不进日志**。
///
/// 失败一律 fail-closed：DPAPI 不可用时**拒绝保存**，绝不退化成明文落盘。
/// </summary>
internal static class AwakeImageSecretStore
{
    private const string FolderName = "Awake/AwakeSecrets";
    private const string FileName = "image.endpoint.key";

    // 附加熵：与文件一起被保护，防止别的程序用同一把 DPAPI 把密文搬走直接用。
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("AWAKE.image.endpoint.key.v1");

    private static readonly object Gate = new object();
    private static bool? cachedHasKey;

    private static PlatformFilePath PathFor()
    {
        return new PlatformFilePath(
            new PlatformDirectoryPath(PlatformFileType.Application, FolderName),
            FileName);
    }

    /// <summary>绝对路径。只用于日志与目录浏览。</summary>
    internal static string AbsolutePath
    {
        get { return PathFor().FileFullPath; }
    }

    /// <summary>有没有存过钥匙。**不做解密**，只判文件在不在、非空。</summary>
    internal static bool HasKey
    {
        get
        {
            lock (Gate)
            {
                if (cachedHasKey.HasValue) return cachedHasKey.Value;
            }

            bool exists = false;
            try
            {
                FileInfo info = new FileInfo(AbsolutePath);
                exists = info.Exists && info.Length > 0;
            }
            catch (Exception ex)
            {
                AwakeLog.Write("image_key_probe_failed error=" + ex.Message);
            }

            lock (Gate) cachedHasKey = exists;
            return exists;
        }
    }

    /// <summary>写钥匙。成功返回 true；失败**不留半份密文**，且原因落日志。</summary>
    internal static bool Save(string secret)
    {
        string value = (secret ?? string.Empty).Trim();
        if (value.Length == 0) return false;

        byte[] protectedBytes;
        try
        {
            byte[] plaintext = Encoding.UTF8.GetBytes(value);
            protectedBytes = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            Array.Clear(plaintext, 0, plaintext.Length);
        }
        catch (Exception ex)
        {
            // fail-closed：宁可存不上，也不落明文。
            AwakeLog.Write("image_key_protect_failed error=" + ex.Message);
            return false;
        }

        try
        {
            string path = AbsolutePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, protectedBytes);
            lock (Gate) cachedHasKey = true;
            AwakeLog.Write("image_key_saved path=" + path + " bytes=" + protectedBytes.Length);
            return true;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("image_key_save_failed error=" + ex.Message);
            return false;
        }
    }

    /// <summary>取钥匙。取不到返回 null —— 调用方必须把它当成"没配"而不是"配了个空"。</summary>
    internal static string TryRead(out string error)
    {
        error = string.Empty;
        string path;
        try
        {
            path = AbsolutePath;
        }
        catch (Exception ex)
        {
            error = "无法解析出图密钥路径：" + ex.Message;
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                error = "尚未配置出图 API Key。";
                return null;
            }

            byte[] protectedBytes = File.ReadAllBytes(path);
            byte[] plaintext = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                string secret = Encoding.UTF8.GetString(plaintext).Trim();
                if (secret.Length == 0)
                {
                    error = "出图 API Key 是空的，请重新填写。";
                    return null;
                }

                return secret;
            }
            finally
            {
                Array.Clear(plaintext, 0, plaintext.Length);
            }
        }
        catch (CryptographicException ex)
        {
            error = "出图 API Key 无法解密（多半是换了 Windows 账号或用户配置损坏）：" + ex.Message;
            AwakeLog.Write("image_key_unprotect_failed error=" + ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            error = "读取出图 API Key 失败：" + ex.Message;
            AwakeLog.Write("image_key_read_failed error=" + ex.Message);
            return null;
        }
    }

    /// <summary>MCM 里只读显示用的短状态。**不含任何密钥内容。**</summary>
    internal static string DescribePresence()
    {
        return HasKey ? "已配置（本机保护存储）" : "未配置";
    }
}
