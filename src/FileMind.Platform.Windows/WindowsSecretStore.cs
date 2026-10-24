using System.Runtime.InteropServices;
using System.Text;
using FileMind.Application.Ports;
using FileMind.Domain.Errors;

namespace FileMind.Platform.Windows;

/// <summary>
/// DPAPI-backed secret store: values are CryptProtectData-encrypted
/// (CurrentUser scope) under %LOCALAPPDATA%\FileMind\secrets. Keys never
/// appear in the repo or logs.
/// </summary>
public sealed class WindowsSecretStore : ISecretStore
{
    private readonly string _dir;

    public WindowsSecretStore(string? dir = null) =>
        _dir = dir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileMind", "secrets");

    private string FileFor(string key) =>
        Path.Combine(_dir, Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32] + ".sec");

    public Task<Result<string>> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var f = FileFor(key);
            if (!File.Exists(f))
                return Task.FromResult(Result<string>.Fail(FileMindError.NotFound($"No secret '{key}'.")));
            var blob = Convert.FromBase64String(File.ReadAllText(f).Trim());
            var plain = Unprotect(blob);
            return Task.FromResult(Result<string>.Ok(Encoding.UTF8.GetString(plain)));
        }
        catch (Exception ex) { return Task.FromResult(Result<string>.Fail(FileMindError.Io(ex.Message))); }
    }

    public Task<Result<bool>> SetAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FileFor(key), Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(value))));
            return Task.FromResult(Result<bool>.Ok(true));
        }
        catch (Exception ex) { return Task.FromResult(Result<bool>.Fail(FileMindError.Io(ex.Message))); }
    }

    public Task<Result<bool>> DeleteAsync(string key, CancellationToken ct = default)
    {
        var f = FileFor(key);
        if (File.Exists(f)) File.Delete(f);
        return Task.FromResult(Result<bool>.Ok(true));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int cbData; public IntPtr pbData; }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string? szDataDescr,
        IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob dataOut);

    private static byte[] Protect(byte[] input)
    {
        var inBlob = new DataBlob { cbData = input.Length, pbData = Marshal.AllocHGlobal(input.Length) };
        var outBlob = new DataBlob();
        try
        {
            Marshal.Copy(input, 0, inBlob.pbData, input.Length);
            if (!CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob))
                throw new InvalidOperationException($"CryptProtectData failed ({Marshal.GetLastWin32Error()})");
            var output = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, output, 0, outBlob.cbData);
            return output;
        }
        finally
        {
            Marshal.FreeHGlobal(inBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(outBlob.pbData);
        }
    }

    private static byte[] Unprotect(byte[] input)
    {
        var inBlob = new DataBlob { cbData = input.Length, pbData = Marshal.AllocHGlobal(input.Length) };
        var outBlob = new DataBlob();
        try
        {
            Marshal.Copy(input, 0, inBlob.pbData, input.Length);
            if (!CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob))
                throw new InvalidOperationException($"CryptUnprotectData failed ({Marshal.GetLastWin32Error()})");
            var output = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, output, 0, outBlob.cbData);
            return output;
        }
        finally
        {
            Marshal.FreeHGlobal(inBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(outBlob.pbData);
        }
    }
}
