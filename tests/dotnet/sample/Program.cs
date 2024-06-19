// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using C2pa;

internal class Program
{
    public static void Main(string inputFile, string? outputFile = null)
    {
        Console.WriteLine("Version: {0}", Sdk.Version);
        Console.WriteLine("Supprted Extensions: {0}", string.Join(",", Sdk.SupportedExtensions));

        if (string.IsNullOrEmpty(inputFile))
            throw new ArgumentNullException(nameof(inputFile), "No filename was provided.");
        if (!File.Exists(inputFile))
            throw new IOException($"No file exists with the filename of {inputFile}.");

        if (outputFile == null)
            ValidateFile(inputFile);
        else
            SignFile(inputFile, outputFile);
    }

    private static void SignFile(string inputFile, string outputFile)
    {
        string extension = Path.GetExtension(inputFile).Substring(1);
        //var signer = new OpenSslSigner();
        var signer = new KeyVaultSigners(new DefaultAzureCredential(true));

        var settings = new ManifestBuilderSettings
        {
            ClaimGenerator = "C# Binding test"
        };

        var manifestDefinition = $$"""
{
    "claim_generator_info": [
        {
            "name": "C# test",
            "version": "0.0.1"
        }
    ],
    "format": "{{extension}}",
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
        var builder = new ManifestBuilder(settings, signer.Config, signer, manifestDefinition);
        builder.Sign(inputFile, outputFile);
        Console.WriteLine("Signing successful");
    }

    private static void ValidateFile(string filename)
    {
        Console.WriteLine($"Reading manifest from file: {filename}");

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

class KeyVaultSigners : SignerCallback
{
    const string KeyVaultUri = "https://kv-8c538cfad6204d9cb88a.vault.azure.net/";
    const string SecretName = "media-provenance-pem";

    const string KeyName = "media-provenance-sign";

    private readonly TokenCredential _credential;

    public KeyVaultSigners(TokenCredential credential)
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
        var result = crypto.SignData(SignatureAlgorithm.RS384, data.ToArray());
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


class OpenSslSigner : SignerCallback
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
                UseShellExecute = false,
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