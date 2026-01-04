using System;
using System.Security.Cryptography;
using System.Text;
using TextCopy;

#region Props
string privateKeyXml = File.ReadAllText("private-key.xml");

string licenseId = "TRIAL-30D-2026-003";
string customer = "Mostafa Kanaan";

// Validity
DateTime validUntilUtc = DateTime.UtcNow.AddMonths(1);

#endregion

// canonical payload (must match app)
string payload =
    "LicenseId=" + licenseId + "\n" +
    "Customer=" + customer + "\n" +
    "ValidUntilUtc=" + validUntilUtc.ToString("O");

byte[] data = Encoding.UTF8.GetBytes(payload);

byte[] signature;
using (RSA rsa = RSA.Create())
{
    rsa.FromXmlString(privateKeyXml);
    signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}

string signatureBase64 = Convert.ToBase64String(signature);

// KEY format
string key = "KF1|" + licenseId + "|" + customer + "|" + validUntilUtc.ToString("O") + "|" + signatureBase64;

Console.WriteLine(key);
ClipboardService.SetText(key);

Console.WriteLine();
Console.WriteLine("License key copied to clipboard!");