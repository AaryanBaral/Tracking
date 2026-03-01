using System.Security.Cryptography;
using System.Text;

namespace Agent.Shared.Services;

public static class ScreenshotFileProtector
{
    public const string KeyEnvironmentVariable = "AGENT_SCREENSHOT_ENCRYPTION_KEY";

    public static bool TryEncryptFileAtRest(string sourcePath, out string encryptedPath, out string? error)
    {
        encryptedPath = sourcePath;
        error = null;

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            error = "Screenshot file is missing.";
            return false;
        }

        var keyText = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(keyText))
        {
            error = $"{KeyEnvironmentVariable} is not configured.";
            return false;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyText);
        }
        catch (FormatException)
        {
            error = $"{KeyEnvironmentVariable} must be base64 encoded.";
            return false;
        }

        if (key.Length != 32)
        {
            error = $"{KeyEnvironmentVariable} must decode to exactly 32 bytes.";
            return false;
        }

        try
        {
            var plain = File.ReadAllBytes(sourcePath);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[plain.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes("employee-tracker-screenshot"));

            encryptedPath = $"{sourcePath}.enc";
            using (var output = File.Create(encryptedPath))
            {
                output.Write(nonce, 0, nonce.Length);
                output.Write(tag, 0, tag.Length);
                output.Write(cipher, 0, cipher.Length);
            }

            File.Delete(sourcePath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static int PurgeExpiredScreenshots(string rootDir, int retentionDays)
    {
        if (retentionDays <= 0 || !Directory.Exists(rootDir))
        {
            return 0;
        }

        var deleted = 0;
        var threshold = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        foreach (var path in Directory.EnumerateFiles(rootDir, "*.enc", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.LastWriteTimeUtc <= threshold.UtcDateTime)
                {
                    info.Delete();
                    deleted++;
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        return deleted;
    }
}
