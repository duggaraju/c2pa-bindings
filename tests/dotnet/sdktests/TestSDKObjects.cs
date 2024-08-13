using System.Runtime.InteropServices;
using System.Text;
using C2pa;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;

namespace sdktests
{
    public class CredentialsFixture
    {
        public TokenCredential GetCredentials()
        {
#if DEBUG            
            return new DefaultAzureCredential(true);
#else
            return new DefaultAzureCredential();
#endif
        }
    }

    public class TestSDKObjects : IClassFixture<CredentialsFixture>
    {
        private readonly TokenCredential _credentials;

        public TestSDKObjects(CredentialsFixture fixture)
        {
            _credentials = fixture.GetCredentials();
        }

        [Theory]
        [InlineData("A valid string", "A valid string")]
        [InlineData("", "")]
        public void TestUtilsFromCStringMethodHandlesValidStringsWhenOwnsResource(string str, string expected)
        {
            // Arrange
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            IntPtr unmanagedArray = Marshal.AllocHGlobal(bytes.Length + 1);

            Marshal.Copy(bytes, 0, unmanagedArray, bytes.Length);

            Marshal.WriteByte(unmanagedArray + bytes.Length, 0);

            // Act
            string result;

            try
            {
                unsafe
                {
                    if (str == string.Empty)
                    {
                        result = Utils.FromCString(null);
                    }
                    else {
                        sbyte* ptr_sbyte = (sbyte*)unmanagedArray.ToPointer();
                        result = Utils.FromCString(ptr_sbyte, true);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedArray);
            }

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("test_samples/space_sample.jpg", true)]
        [InlineData("./some/invalid/path.pth", false)]
        public void TestUtilsFilePathValidMethod(string path, bool expected)
        {
            // Act
            bool result = Utils.FilePathValid(path);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestManifestClassStoresManifestDataCorrectly()
        {
            // Arrange 
            string manifestTitle = "C# Test Image";
            string format = "jpg";
            ClaimGeneratorInfo claimInfo = new() { Name = "C# Test", Version = "1.0.0" };
            AuthorInfo author = new("Person", "Isaiah Carrington");
            CreativeWorkAssertionData assertionData = new("http://schema.org", "CreativeWork", [author]);
            CreativeWorkAssertion assertion = new(assertionData);

            // Act

            ManifestDefinition manifest = new() { Title = manifestTitle, Format = format, ClaimGeneratorInfo = [claimInfo], Assertions = [assertion] };

            // Assert
            Assert.Equal("C# Test Image", manifest.Title);
            Assert.Equal("C# Test", manifest.ClaimGeneratorInfo[0].Name);
            Assert.Equal("Isaiah Carrington", (manifest.Assertions[0].Data as CreativeWorkAssertionData)?.Authors[0].Name);
            Assert.Equal("jpg", manifest.Format);
        }

        [Fact]
        public void TestManifestReaderReadsAndSerializesManifestCorrectly()
        {
            // Arrange
            ISignerCallback signer = new TestUtils.KeyVaultSigner(_credentials);

            string outputPath = TestUtils.CreateSignedFile("test_samples/space_sample.jpg", "test_samples/space_sample_signed.jpg", signer);

            // Act

            ManifestStoreReader reader = new();
            ManifestStore? store = reader.ReadFromFile(outputPath);
            Sdk.CheckError();
            if (store == null)
            {
                if (Utils.FilePathValid(outputPath))
                {
                    File.Delete(outputPath);
                }
                throw new IOException("Output path was either not created or not signed properly.");
            }

            Manifest manifest = store.Manifests[store.ActiveManifest];

            // Assert
            Assert.Equal("C# Test Image", manifest.Title);
        }

        [Fact]
        public void TestIngrdientSerializedFromManifestCorrectly()
        {
            // Arrange 
            string inputfile = "test_samples/ingredient_sample.jpg";

            ManifestStoreReader reader = new();

            // Act
            ManifestStore? store = reader.ReadFromFile(inputfile);
            Assert.NotNull(store);

            Manifest? manifest = store.Manifests[store.ActiveManifest];

            // Assert
            Assert.NotNull(manifest);
            Assert.Equal("sample.jpg", manifest.Ingredients[0].Title);
            Assert.Equal("image/jpeg", manifest.Ingredients[0].Thumbnail?.Format);
        }

        [Fact]
        public void TestingCreatingASignedFileWithAnIngredient()
        {
            // Arrange
            string inputFile = "test_samples/ingredient_signing_sample.jpg";
            string title = "test_samples/pres_edited.jpg";
            string format = "image/jpeg";
            string parentName = "test_samples/ingredient_sample.jpg";

            ISignerCallback signer = new TestUtils.KeyVaultSigner(_credentials);

            Thumbnail thumbnail = new(format, "manifest_thumbnail.jpg");

            ManifestDefinition definition = new()
            {
                Title = title,
                Format = format,
                ClaimGeneratorInfo = { new ClaimGeneratorInfo("C# Test", "1.0.0") },
                Thumbnail = thumbnail,
                Assertions = [new CreativeWorkAssertion(new CreativeWorkAssertionData("http://schema.org", "CreativeWork", [new AuthorInfo("Person", "Isaiah Carrington")]))],
            };


            // Act
            ManifestBuilder builder = new(new() { ClaimGenerator = "C# Test" }, signer, definition);
            builder.AddIngredient(new(parentName, format, Relationship.parentOf), parentName);

            string? thumbURI = (builder.GetManifestDefinition()?.Thumbnail?.Identifier) ?? throw new Exception("Thumbnail URI shouldn't be null.");
            builder.SetThumbnail(thumbnail, "test_samples/ingredient_sample.jpg");

            builder.Sign(inputFile, "signed_files/ingredient_signed.jpg");
            // Assert
        }
    }


    public class TestUtils
    {
        public static ManifestBuilder GetTestBuilder(ISignerCallback signer, string format)
        {
            ManifestBuilderSettings builderSettings = new() { ClaimGenerator = "C# Binding Test" };

            ManifestDefinition manifest = new()
            {
                Title = "C# Test Image",
                Format = format,
                ClaimGeneratorInfo = [new("C# Test", "1.0.0")],
                Assertions = [new CreativeWorkAssertion(new CreativeWorkAssertionData("http://schema.org", "CreativeWork", [new AuthorInfo("Person", "Isaiah Carrington")]))],
                Ingredients = [new Ingredient("sample.jpg", "image/jpeg", Relationship.parentOf)]
            };

            ManifestBuilder builder = new(builderSettings, signer, manifest);
            return builder;
        }

        public static string CreateSignedFile(string inputPath, string outputPath, ISignerCallback signer)
        {
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            string format = inputPath.Split(".").Last();

            ManifestBuilder builder = GetTestBuilder(signer, format);

            // Act
            builder.Sign(inputPath, outputPath);
            return outputPath;
        }

        public class KeyVaultSigner : ISignerCallback
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

            public SignerConfig Config => new()
            {
                Alg = "ps384",
                Certs = GetCertificates(),
                TimeAuthorityUrl = "http://timestamp.digicert.com",
                UseOcsp = false
            };
        }

    }
}
