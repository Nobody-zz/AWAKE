using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MarcusAwakeProvider;

internal sealed class DpapiCredentialProtector : ICredentialProtector
{
    private const int CryptprotectUiForbidden = 0x1;

    public byte AlgorithmId => 0xD1;
    public CredentialProtectionStatus Status => CredentialProtectionStatus.WindowsDpapi;
    public string Warning => string.Empty;

    public byte[] Protect(byte[] plaintext)
    {
        return Invoke(plaintext, protect: true);
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        return Invoke(ciphertext, protect: false);
    }

    public void Dispose()
    {
    }

    private static byte[] Invoke(byte[] input, bool protect)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var inputHandle = Marshal.AllocHGlobal(input.Length);
        try
        {
            Marshal.Copy(input, 0, inputHandle, input.Length);
            var inputBlob = new DataBlob { Size = input.Length, Data = inputHandle };
            DataBlob outputBlob;
            var succeeded = protect
                ? CryptProtectData(ref inputBlob, "MarcusAwakeProviderCredential", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, out outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, out outputBlob);
            if (!succeeded) throw new CryptographicException(Marshal.GetLastWin32Error());

            try
            {
                var output = new byte[outputBlob.Size];
                Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                return output;
            }
            finally
            {
                if (outputBlob.Data != IntPtr.Zero) LocalFree(outputBlob.Data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inputHandle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        internal int Size;
        internal IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
