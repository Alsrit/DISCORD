using System.Security.Cryptography;
using System.Text;
using Platform.Application.Models;
using Platform.Client.Core.Configuration;

namespace Platform.Client.Core.Services;

public sealed class UpdateVerificationService(ClientSettingsStore settingsStore)
{
    public bool VerifyPackage(UpdatePackageDto package, string filePath)
    {
        var settings = settingsStore.Load();
        if (string.IsNullOrWhiteSpace(settings.UpdatePublicKeyPath) || !File.Exists(settings.UpdatePublicKeyPath))
        {
            return false;
        }

        using var stream = File.OpenRead(filePath);
        var computedHash = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(computedHash, package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedPayload = $"{package.Version}|{package.Channel}|{package.Sha256}|{package.Mandatory}|{package.PublishedUtc:O}";
        if (!string.Equals(expectedPayload, package.SignaturePayload, StringComparison.Ordinal))
        {
            return false;
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(settings.UpdatePublicKeyPath));
        var signature = Convert.FromBase64String(package.SignatureBase64);
        return rsa.VerifyData(Encoding.UTF8.GetBytes(package.SignaturePayload), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
