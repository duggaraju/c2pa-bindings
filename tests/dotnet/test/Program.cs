// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using C2pa;
using Azure.CodeSigning;
using Azure.CodeSigning.Models;

internal class Program
{
    public static void Main(string inputFile, string? outputFile = null)
    {
        Console.WriteLine("Version: {0}", Sdk.Version);
        Console.WriteLine("Supprted Extensions: {0}", string.Join(",", Sdk.SupportedExtensions));

        if (outputFile == null)
            ValidateFile(inputFile);
        else 
            SignFile(inputFile, outputFile);
    }

    private static void SignFile(string inputFile, string outputFile)
    {
        //var signer = new LocalSigner();
        var signer = new TrustedSigner(
            new DefaultAzureCredential(true));

        var settings = new ManifestBuilderSettings
        {
            ClaimGenerator = "C# Binding test",
            TrustSettings = """
            {
              "trust": {
                "trust_config": "1.3.6.1.5.5.7.3.36\n1.3.6.1.4.1.311.76.59.1.9"
              },
              "verify": {
                "verify_after_sign": false
              }
            }
            """
        };

        var manifestDefinition = """
{
    "claim_generator": "C# test",
    "claim_generator_info": [
        {
            "name": "C# test",
            "version": "0.0.1"
        }
    ],
    "format": "image/png",
    "title": "C# Test Image",
    "ingredients": [],
    "assertions": [
        {   "label": "stds.schema-org.CreativeWork",
            "data": {
                "@context": "http://schema.org/",
                "@type": "CreativeWork",
                "author": [
                    {   "@type": "Person",
                        "name": "Prakash Duggaraju"
                    }
                ]
            },
            "kind": "Json"
        }
    ]
}
""";

        Console.WriteLine("Signing manifest");
        var builder = new ManifestBuilder(settings, signer, manifestDefinition);
        builder.Sign(inputFile, outputFile);
        Console.WriteLine("Signing successful");
    }

    private static void ValidateFile(string filename)
    {
        Console.WriteLine("Reading manifest from file");
        var reader = new ManifestStoreReader();
        var store = reader.ReadFromFile(filename);
        if (store != null)
        {
            var activeManifest = store.Manifests[store.ActiveManifest];
            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine("Manifest: {0}", JsonSerializer.Serialize(store, options));
        }
        else
        {
            Console.WriteLine("No Manifest found: {0}", filename);
        }
    }
}

class TrustedSigner : ISignerCallback
{
    const string EndpointUri = "https://eus.codesigning.azure.net/";
    static readonly Azure.CodeSigning.Models.SignatureAlgorithm Algorithm = Azure.CodeSigning.Models.SignatureAlgorithm.PS384;
    const string CertificateProfile = "media-provenance-sign";
    const string AccountName = "ts-80221a56b4b24529a43e";

    private readonly CertificateProfileClient _client;

    public TrustedSigner(TokenCredential credential)
    {
        _client = new (credential, new Uri(EndpointUri));
    }

    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        var digest = GetDigest(data);
        var request = new SignRequest(Algorithm, digest);
        var operation = _client.StartSign(AccountName, CertificateProfile, request);
        SignStatus status = operation.WaitForCompletion();
        status.Signature.CopyTo(hash);
        return status.Signature.Length;
    }

    private byte[] GetDigest(ReadOnlySpan<byte>data )
    {
        using var sha = SHA384.Create();
        return sha.ComputeHash(data.ToArray());
    }

    public string GetCertificates()
    {
        // For now dummy signing. Eventually use the separate call to get certificates.
        var random = new Random();
        var hash = new byte[32];
        random.NextBytes(hash);
        var digest = GetDigest(hash);
        var request = new SignRequest(Algorithm, digest);
        var operation = _client.StartSign(AccountName, CertificateProfile, request);
        SignStatus status = operation.WaitForCompletion();
        var cmsData = new SignedCms();
        var cdata = Encoding.ASCII.GetChars(status.SigningCertificate);
        //File.WriteAllLines(@"D:\temp\test.p7b", new[] { "-----BEGIN PKCS7-----", new String(cdata), "-----END PKCS7-----" });
        var certificate = Convert.FromBase64CharArray(cdata, 0, cdata.Length);
        cmsData.Decode(certificate);
        var builder = new StringBuilder();
        // TODO: sort them properly from leaf to root.
        foreach (var cert in cmsData.Certificates)
        {
            Console.WriteLine("Subject = {0} Issuer = {1} Expiry = {2}", cert.Subject, cert.Issuer, cert.GetExpirationDateString());
            builder.AppendLine($"subject={cert.Subject}");
            builder.AppendLine($"issuer={cert.Issuer}");
            var data = PemEncoding.Write("CERTIFICATE", cert.RawData);
            builder.AppendLine(new String(data));
        }
        var pem = builder.ToString();
        //File.WriteAllText(@"D:\temp\test.pem", pem);
        return pem;
    }

    public SignerConfig Config => new SignerConfig
    {
        Alg = "ps384",
        Certs = GetCertificates(),
        TimeAuthorityUrl = "http://timestamp.digicert.com",
        UseOcsp = false
    };

}

class KeyVaultSigner : ISignerCallback
{
    const string KeyVaultUri = "https://kv-8c538cfad6204d9cb88a.vault.azure.net/";
    const string SecretName = "media-provenance-pem";
    static readonly Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm Algorithm = Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm.PS384;

    const string KeyName = "media-provenance-sign";

    private readonly TokenCredential _credential;

    public KeyVaultSigner(TokenCredential credential)
    {
        _credential = credential;
    }

    public string GetCertificates()
    {
        var client = new SecretClient(new Uri(KeyVaultUri), _credential);
        KeyVaultSecret secret = client.GetSecretAsync(SecretName).Result;
        return secret.Value!;
    } 

    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        var client = new KeyClient(new Uri(KeyVaultUri), _credential);
        KeyVaultKey key = client.GetKey(KeyName);
        var crypto = new CryptographyClient(new Uri(key.Key.Id), _credential);
        var result = crypto.SignData(Algorithm, data.ToArray());
        result.Signature.CopyTo(hash);
        return result.Signature.Length;
    }

    public SignerConfig Config => new SignerConfig
    {
        Alg = "ps384",
        Certs = GetCertificates(),
        TimeAuthorityUrl = "http://timestamp.digicert.com",
        UseOcsp = false
    };
}


class LocalSigner: ISignerCallback
{
    const string KeyFile = "/home/krishndu/rust/c2pa-rs/sdk/tests/fixtures/certs/ps256.pem";
    const string CertFile = "/home/krishndu/rust/c2pa-rs/sdk/tests/fixtures/certs/ps256.pub";


    public SignerConfig Config => new SignerConfig
    {
        Alg = "ps256",
        Certs = File.ReadAllText(CertFile),
        TimeAuthorityUrl = "http://timestamp.digicert.com",
        UseOcsp = false
    };

    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        var pem = File.ReadAllText(KeyFile);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var signed = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        signed.CopyTo(hash);
        return signed.Length;
    }
    
    public int Sign2(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        Console.WriteLine("Signing data of length: {0}", data.Length);
        using var rsa = RSA.Create();
        var pem = File.ReadAllText(KeyFile);
        var der = pem.Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "");
        var key = Convert.FromBase64String(der);
        Console.WriteLine("Key is {0} {1}", der, key);
        rsa.ImportRSAPrivateKey(key, out var read);
        var result = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        result.CopyTo(hash);
        return result.Length;
    }

 }

    class OpenSslSigner : ISignerCallback
{
    const string KeyFile = "/home/krishndu/rust/c2pa-rs/sdk/tests/fixtures/certs/ps256.pem";
    const string CertFile = "/home/krishndu/rust/c2pa-rs/sdk/tests/fixtures/certs/ps256.pub";


    public SignerConfig Config => new SignerConfig
    {
        Alg = "ps256",
        Certs = File.ReadAllText(CertFile),
        TimeAuthorityUrl = "http://timestamp.digicert.com",
        UseOcsp = false
    };

    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        Console.WriteLine("Signing data of length: {0}", data.Length);
        var path = Path.GetTempPath();
        var dataFile = Path.Combine(path, Path.GetTempFileName());
        var outFile = Path.Combine(path, Path.GetTempFileName());
        File.WriteAllBytes(dataFile, data.ToArray());
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "openssl",
                Arguments = $"dgst -sign {KeyFile} -sha256 -out {outFile} {dataFile}",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();
        Console.WriteLine("Process exit code: {0}", process.ExitCode);
        var bytes = File.ReadAllBytes(outFile);
        bytes.CopyTo(hash);
        return bytes.Length;
    }
}