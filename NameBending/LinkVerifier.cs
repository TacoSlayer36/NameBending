using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NameBending;
// Full discretion--this entire class is vibecoded qwq
public static class LinkVerifier
{
    private const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEyDokFop6UARvf5kc4AoF4hVcI75P3eKv/wE9IlFZUcIksso7FBXJgn+vHlHgax3ABkDaf9JFl++kbIVCstE8PA==-----END PUBLIC KEY-----";

    public static bool IsApprovedLink(string url, string token)
    {
        if (Config.Disclaimer.Value && Config.TrustAllImages.Value) return true;

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(PublicKeyPem);

            byte[] urlBytes = Encoding.UTF8.GetBytes(url);
            byte[] signature = Convert.FromBase64String(token);

            return ecdsa.VerifyData(urlBytes, signature, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    public static async Task<(bool approved, byte[]? imageBytes)> VerifyAndFetch(string proxyUrl, string token, HttpClient http)
    {
        try
        {
            // Verify signature
            if (!IsApprovedLink(proxyUrl, token))
                return (false, null);

            // Extract checksum and original URL from proxy URL
            var uri = new Uri(proxyUrl);
            var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
            string? expectedChecksum = qs["cs"];
            if (string.IsNullOrEmpty(expectedChecksum))
                return (false, null);

            string base64 = uri.Segments.Last().Replace('-', '+').Replace('_', '/');
            string originalUrl = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

            // Fetch once
            byte[] imageBytes = await http.GetByteArrayAsync(originalUrl);

            // Verify checksum
            string actualChecksum = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
            if (actualChecksum != expectedChecksum)
                return (false, null);

            return (true, imageBytes);
        }
        catch
        {
            return (false, null);
        }
    }
}