using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class SecureTokenStore(ClientPathService pathService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public StoredSessionBundle? Load()
    {
        pathService.EnsureFolders();
        if (!File.Exists(pathService.TokensPath))
        {
            return null;
        }

        var encryptedBytes = File.ReadAllBytes(pathService.TokensPath);
        var decrypted = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(decrypted);
        return JsonSerializer.Deserialize<StoredSessionBundle>(json, JsonOptions);
    }

    public void Save(StoredSessionBundle bundle)
    {
        pathService.EnsureFolders();
        var json = JsonSerializer.Serialize(bundle, JsonOptions);
        var plaintext = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(pathService.TokensPath, encrypted);
    }

    public void Clear()
    {
        if (File.Exists(pathService.TokensPath))
        {
            File.Delete(pathService.TokensPath);
        }
    }
}
