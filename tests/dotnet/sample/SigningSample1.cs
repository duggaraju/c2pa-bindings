// This file aims to show the user how to use the C# C2PA SDK to sign media using Azure Key Vault.

using System;
using C2pa;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;

// Note: This is a sample code snippet. It is not meant to be executed. It is meant to be used for documentation purposes.

// You will need a signer. The signer should inherit from the SignerCallback Interface as demonstrated:

class KeyVaultSigner : ISignerCallback
{

    /// <summary>
    /// The Azure credentials used to access the Key Vault.
    /// </summary>
    private readonly TokenCredential _credential;

    /// <summary>
    /// The URI of the Key Vault.
    /// </summary>
    const string KeyVaultURI = "https://<your-key-vault-name>.vault.azure.net/";

    /// <summary>
    /// The name of the key in the Key Vault.
    /// </summary>
    const string KeyName = "<your-key-name>";

    /// <summary>
    ///  The name of the secret to access your certificate.
    /// </summary>
    const string SecretName = "<your-secret-name>";

    public KeyVaultSigner()
    {
        // These will be your Azure credentials, that will be used to access your key vault.
        _credential = new DefaultAzureCredential();
    }

    /// <summary>
    /// Signs the specified data using the configured key and returns the length of the signature.
    /// </summary>
    /// <param name="data">The data to be signed.</param>
    /// <param name="hash">The buffer to store the signature.</param>
    /// <returns>The length of the signature.</returns>
    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        // Your signing logic here
        KeyClient client = new(new Uri(KeyVaultURI), _credential);
        KeyVaultKey key = client.GetKey(KeyName);

        CryptographyClient crypto = new (new Uri(key.Key.Id), _credential);

        SignResult result = crypto.SignData(SignatureAlgorithm.RS384, data.ToArray());
        result.Signature.CopyTo(hash);

        return result.Signature.Length;
    }

    /// <summary>
    /// Retrieves the certificates from the Key Vault.
    /// </summary>
    public string GetCertificates()
    {
        // Your certificate retrieval logic here
        SecretClient client = new(new Uri(KeyVaultURI), _credential);
        KeyVaultSecret secret = client.GetSecret(SecretName);

        return secret.Value;
    }

    /// <summary>
    /// The configuration for the signer.
    /// </summary>
    public SignerConfig Config => new SignerConfig{
        Alg = "ps384",
        Certs = GetCertificates(),
        TimeAuthorityUrl = null,
        UseOcsp = false
    };
}

/// <summary>
/// A sample class that demonstrates how to sign a file using the C2PA SDK.
/// </summary>
public class Demo {

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args) 
    {
        string inputFileName = "somefile.jpg";
        string outputFileName = "somefile-signed.jpg";

        // SignFile(inputFileName, outputFileName);
    }

    /// <summary>
    /// Signs the file provided from the inputPath, and stores the result in the outputPath.
    /// </summary>
    /// <param name="inputPath"></param>
    /// <param name="outputPath"></param>
    // private static void SignFile(string inputPath, string outputPath) {
    //     string extension = Path.GetExtension(inputPath)[1..]; // Will need the extension for the manifest

    //     // Create a new signer
    //     KeyVaultSigner signer = new ();

    //     // Will need your settings to build your manifest
    //     ManifestBuilderSettings settings = new()
    //     {
    //         ClaimGenerator = "c2pa"
    //     };

    //     // Will then need your manifest defintion.

    //     string manifestDefinition = C2pa.Utils.BuildManifestDefinition("Claim name", "Title of Manifest", "Name of Author", extension);

    //     // With the manifest definition, you can now create a new manifest builder
    //     ManifestBuilder builder = new(settings, signer.Config, signer, manifestDefinition);

    //     // Sign the file
    //     builder.Sign(inputPath, outputPath);
    // }
}