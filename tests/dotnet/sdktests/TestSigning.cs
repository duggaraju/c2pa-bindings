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

        public static ManifestBuilder GetTestBuilder(ISignerCallback signer)
        {
            ManifestBuilderSettings builderSettings = new () { ClaimGenerator = "C# Binding Test" };

            Manifest manifest = new ()
            {
                Title = "C# Test Image",
                Format = "jpg",
                ClaimGeneratorInfo = [new("C# Test", "1.0.0")],
                Assertions = [ new("stds.schema-org.CreativeWork", new AssertionData("http://schema.org", "CreativeWork", [new AuthorInfo("Person", "Isaiah Carrington")]))]
            };

            ManifestBuilder builder = new(builderSettings, signer.Config, signer, manifest.GetManifestJson());
            return builder;
        }

        [Fact]
        public void TestManifestClassStoresManifestDataCorrectly()
        {
            // Arrange 
            string manifestTitle = "C# Test Image";
            string format = "jpg";
            ClaimGeneratorInfoData claimInfo = new () { Name = "C# Test", Version = "1.0.0"};
            AuthorInfo author = new ("Person", "Isaiah Carrington");
            AssertionData assertionData = new ("http://schema.org", "CreativeWork", [author]);
            Assertion assertion = new ("stds.schema-org.CreativeWork", assertionData);

            // Act

            Manifest manifest = new () {Title = manifestTitle, Format = format, ClaimGeneratorInfo = [claimInfo], Assertions = [assertion]};

            // Assert
            Assert.Equal("C# Test Image", manifest.Title);
            Assert.Equal("C# Test", manifest.ClaimGeneratorInfo[0].Name);
            Assert.Equal("Isaiah Carrington", manifest.Assertions[0].Data.Author[0].Name);
            Assert.Equal("jpg", manifest.Format);
        }

        [Theory]
        [InlineData("KeyVault")]
        public void TestManifestAddedToFileCorrectly(string signerType)
        {
            // Arrange
            string inputPath = "C:\\sample\\sample1.jpg";
            string outputPath = $"C:\\sample\\output_{signerType}.jpg";

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            ISignerCallback signer;

            signer = new KeyVaultSigner(new DefaultAzureCredential(true));

            ManifestBuilder builder = TestSigning.GetTestBuilder(signer);

            // Act
            builder.Sign(inputPath, outputPath);

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
            Assert.Equal("Isaiah Carrington", manifestJson.Assertions[0].Data.Author[0].Name);
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