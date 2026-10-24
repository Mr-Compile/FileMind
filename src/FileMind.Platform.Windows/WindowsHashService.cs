using System.Security.Cryptography;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Platform.Windows;

public sealed class WindowsHashService : IHashService
{
    private const int PartialBytes = 4 * 1024 * 1024;

    public async Task<Result<string>> HashAsync(string path, bool full, CancellationToken ct = default)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = SHA256.Create();
            if (!full && stream.Length > PartialBytes)
            {
                var buffer = new byte[PartialBytes];
                var read = await stream.ReadAsync(buffer, ct);
                var hash = sha.ComputeHash(buffer, 0, read);
                return Result<string>.Ok($"sha256-partial:{Convert.ToHexStringLower(hash)}");
            }
            var fullHash = await sha.ComputeHashAsync(stream, ct);
            return Result<string>.Ok($"sha256:{Convert.ToHexStringLower(fullHash)}");
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex is FileNotFoundException or DirectoryNotFoundException
                ? FileMindError.NotFound(ex.Message)
                : FileMindError.Io(ex.Message));
        }
    }
}
