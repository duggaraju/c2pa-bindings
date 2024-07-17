using C2pa;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using System.Text.Json;
using System.Security.Cryptography;
using System.Runtime.ConstrainedExecution;


namespace sdktests
{
    public class TestSigning
    {
        [Theory]
        [InlineData("Invalid\\File\\Path")]
        [InlineData("")]
        public void TestExceptionRaisedWhenInvalidFilePathProvidedToManifestReader(string filepath)
        {
            // Arrange
            ManifestStoreReader msr = new();

            // Act
            Action act = () => { msr.ReadFromFile(filepath); };
            // Assert

            ArgumentException exc = Assert.Throws<ArgumentException>(act);

            Assert.Equal("Invalid file path provided. (Parameter 'path')", exc.Message);
        }

        [Theory]
        [InlineData("KeyVault")]
        public void TestManifestAddedToFileCorrectly(string signerType)
        {
            // Arrange
            string inputPath = "..\\..\\..\\test_samples\\signing_sample.jpg";
            string outputPath = $"..\\..\\..\\test_samples\\output_{signerType}.jpg";

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            ISignerCallback signer;

            // Can perform some logic to test with different signers based off of signerType
            signer = new KeyVaultSigner(new DefaultAzureCredential(true));

            // Act
            TestUtils.CreateSignedFile(inputPath, outputPath, signer);

            if (!File.Exists(outputPath)) throw new IOException("Output path was not created.");

            ManifestStoreReader reader = new();

            ManifestStore? store = reader.ReadFromFile(outputPath);
            
            Assert.NotNull(store);

            string manifest = JsonSerializer.Serialize(store.Manifests[store.ActiveManifest]);

            var manifestJson = JsonSerializer.Deserialize<Manifest>(manifest);

            // Assert
            Assert.NotNull(manifestJson);

            Assert.Equal("C# Test Image", manifestJson.Title);
            Assert.Equal("C# Test", manifestJson.ClaimGeneratorInfo[0].Name);
            Assert.Equal("Isaiah Carrington", (manifestJson.Assertions[0].Data as AssertionData).Author[0].Name);
            Assert.Equal("jpg", manifestJson.Format);
        }
    }


    class KeyVaultSigner : ISignerCallback
    {
        const string KeyVaultUri = "https://kv-8c538cfad6204d9cb88a.vault.azure.net/";
        const string SecretName = "media-provenance-pem";

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
            var result = crypto.SignData(SignatureAlgorithm.RS384, data.ToArray());
            result.Signature.CopyTo(hash);
            return result.Signature.Length;
        }

        public SignerConfig Config => new ()
        {
            Alg = "ps384",
            Certs = GetCertificates(),
            TimeAuthorityUrl = "http://timestamp.digicert.com",
            UseOcsp = false
        };
    }

}