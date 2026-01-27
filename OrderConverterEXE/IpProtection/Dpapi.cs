using System.ComponentModel;
using System.Runtime.InteropServices;

namespace OrderConverterEXE.IpProtection;

/// <summary>
/// Windows DPAPI 封装（不引入额外 NuGet 依赖）。
/// </summary>
internal static class Dpapi
{
    // https://learn.microsoft.com/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata
    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static byte[] Protect(byte[] plaintext, byte[] entropy)
    {
        if (plaintext.Length == 0) return Array.Empty<byte>();

        var inBlob = ToBlob(plaintext);
        var entBlob = ToBlob(entropy);
        var outBlob = new DATA_BLOB();

        try
        {
            if (!CryptProtectData(ref inBlob, null, ref entBlob, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptProtectData failed");
            }

            return FromBlobAndFree(outBlob);
        }
        finally
        {
            FreeBlob(inBlob);
            FreeBlob(entBlob);
        }
    }

    public static byte[] Unprotect(byte[] ciphertext, byte[] entropy)
    {
        if (ciphertext.Length == 0) return Array.Empty<byte>();

        var inBlob = ToBlob(ciphertext);
        var entBlob = ToBlob(entropy);
        var outBlob = new DATA_BLOB();

        try
        {
            if (!CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entBlob, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptUnprotectData failed");
            }

            return FromBlobAndFree(outBlob);
        }
        finally
        {
            FreeBlob(inBlob);
            FreeBlob(entBlob);
        }
    }

    private static DATA_BLOB ToBlob(byte[] data)
    {
        var blob = new DATA_BLOB
        {
            cbData = data.Length,
            pbData = Marshal.AllocHGlobal(data.Length)
        };
        Marshal.Copy(data, 0, blob.pbData, data.Length);
        return blob;
    }

    private static byte[] FromBlobAndFree(DATA_BLOB blob)
    {
        if (blob.cbData <= 0 || blob.pbData == IntPtr.Zero) return Array.Empty<byte>();
        var data = new byte[blob.cbData];
        Marshal.Copy(blob.pbData, data, 0, blob.cbData);
        LocalFree(blob.pbData);
        blob.pbData = IntPtr.Zero;
        blob.cbData = 0;
        return data;
    }

    private static void FreeBlob(DATA_BLOB blob)
    {
        if (blob.pbData != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(blob.pbData);
        }
    }
}

