namespace KanaanFlow.App.Licensing;

using KanaanFlow.Core.Licensing;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class LicenseService : ILicenseService
{
    const string PublicKeyXml = @"<RSAKeyValue><Modulus>r2w3drmTrdUmRKZv7GjgGF9sZMetiGIPt+HYSkzwO5Nk2NnIfU0/PETzuSAkVpCXQc9k0eVjx/Ej1ziLtSs3RqpJgL8R/OE0zJHjgoXZUPTdUc6/KPU41VT5E8TlhZFpsUdcqu21D0Gyp/fKN7oD2twm0rF/dIhM5GIOwCw6ETzqeKR/i+x05ALnekClbMv8haoDRacrGHbJyCKKdElTyXQ7+jCJbMsGtpEd4GC4CHYPerHMr5c0lTJN6P6rmNrupdMCXIJTELzpdJyCMb1zSMbaxmjMJFNSxY7qUQYNfJ9zxhi/elurC5pobFbnvCEzg+3gOi1TlsOGy48oP/EL7Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";


    private static string LicenseKeyPath
        => Path.Combine(FileSystem.AppDataDirectory, "license.key");

    public async Task SaveLicenseKeyAsync(string licenseKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            throw new InvalidOperationException("License key is empty.");

        await File.WriteAllTextAsync(LicenseKeyPath, licenseKey.Trim(), cancellationToken);
    }

    public async Task<string?> LoadLicenseKeyAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(LicenseKeyPath))
            return null;

        string key = await File.ReadAllTextAsync(LicenseKeyPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return key.Trim();
    }

    public async Task<LicenseStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        string? key = await LoadLicenseKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            return new LicenseStatus
            {
                IsValid = false,
                Message = "No license key found."
            };
        }

        ParsedLicense? parsed = TryParseKey(key);
        if (parsed == null)
        {
            return new LicenseStatus { IsValid = false, Message = "License key format is invalid." };
        }

        if (parsed.Claims.ValidUntilUtc <= DateTime.UtcNow)
        {
            return new LicenseStatus
            {
                IsValid = false,
                Message = "License expired.",
                Claims = parsed.Claims
            };
        }

        bool signatureOk = VerifySignature(parsed.Claims, parsed.SignatureBase64);
        if (!signatureOk)
        {
            return new LicenseStatus
            {
                IsValid = false,
                Message = "License signature is invalid.",
                Claims = parsed.Claims
            };
        }

        return new LicenseStatus
        {
            IsValid = true,
            Message = "License valid.",
            Claims = parsed.Claims
        };
    }

    private static ParsedLicense? TryParseKey(string key)
    {
        // KF1|licenseId|customer|validUntilUtcO|signatureBase64
        string[] parts = key.Split('|');
        if (parts.Length != 5)
            return null;

        if (!string.Equals(parts[0], "KF1", StringComparison.Ordinal))
            return null;

        string licenseId = parts[1].Trim();
        string customer = parts[2].Trim();
        string validUntilRaw = parts[3].Trim();
        string signatureBase64 = parts[4].Trim();

        if (string.IsNullOrWhiteSpace(licenseId) ||
            string.IsNullOrWhiteSpace(customer) ||
            string.IsNullOrWhiteSpace(validUntilRaw) ||
            string.IsNullOrWhiteSpace(signatureBase64))
        {
            return null;
        }

        DateTime validUntilUtc;
        bool ok = DateTime.TryParseExact(
            validUntilRaw,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out validUntilUtc);

        if (!ok)
            return null;

        LicenseClaims claims = new LicenseClaims
        {
            LicenseId = licenseId,
            Customer = customer,
            ValidUntilUtc = validUntilUtc
        };

        ParsedLicense parsed = new ParsedLicense
        {
            Claims = claims,
            SignatureBase64 = signatureBase64
        };

        return parsed;
    }

    private bool VerifySignature(LicenseClaims claims, string signatureBase64)
    {
        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureBase64);
        }
        catch
        {
            return false;
        }

        string payload =
            "LicenseId=" + (claims.LicenseId ?? string.Empty) + "\n" +
            "Customer=" + (claims.Customer ?? string.Empty) + "\n" +
            "ValidUntilUtc=" + claims.ValidUntilUtc.ToUniversalTime().ToString("O");

        byte[] data = Encoding.UTF8.GetBytes(payload);

        using (RSA rsa = RSA.Create())
        {
            try
            {
                rsa.FromXmlString(PublicKeyXml);
            }
            catch
            {
                return false;
            }

            return rsa.VerifyData(
                data,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
    }

    private sealed class ParsedLicense
    {
        public LicenseClaims Claims { get; set; } = new LicenseClaims();
        public string SignatureBase64 { get; set; } = string.Empty;
    }
}
