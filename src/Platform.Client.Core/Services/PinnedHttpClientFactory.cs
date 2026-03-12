using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Platform.Client.Core.Configuration;

namespace Platform.Client.Core.Services;

public sealed class PinnedHttpClientFactory
{
    public HttpClient Create(ClientSettings settings, string? bearerToken = null)
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, sslErrors) =>
        {
            if (certificate is null)
            {
                return false;
            }

            if (sslErrors != System.Net.Security.SslPolicyErrors.None)
            {
                return false;
            }

            if (!settings.RequireCertificatePinning)
            {
                return true;
            }

            if (settings.PinnedSpkiSha256.Count == 0)
            {
                return false;
            }

            var cert = new X509Certificate2(certificate);
            var spkiHash = Convert.ToBase64String(SHA256.HashData(cert.GetPublicKey()));
            return settings.PinnedSpkiSha256.Contains(spkiHash, StringComparer.Ordinal);
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.ServerBaseUrl.TrimEnd('/') + "/")
        };

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }
}
