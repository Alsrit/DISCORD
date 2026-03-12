using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Infrastructure.Configuration;

namespace Platform.Infrastructure.Services;

public sealed class UpdateSignatureService(IOptions<UpdateSigningOptions> options) : IUpdateSignatureService
{
    private readonly UpdateSigningOptions _options = options.Value;

    public Task<(string Payload, string SignatureBase64, string Algorithm)> SignReleaseAsync(
        string version,
        string channel,
        string sha256,
        bool mandatory,
        DateTimeOffset publishedUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PrivateKeyPath) || !File.Exists(_options.PrivateKeyPath))
        {
            throw new InvalidOperationException("Не найден приватный ключ подписи обновлений.");
        }

        var payload = $"{version}|{channel}|{sha256}|{mandatory}|{publishedUtc:O}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(_options.PrivateKeyPath));
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Task.FromResult((payload, Convert.ToBase64String(signature), "RSA-SHA256"));
    }
}
