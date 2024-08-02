using C2pa;
using Azure.Identity;


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
            string inputPath = "test_samples/signing_sample.jpg";
            string outputPath = $"test_samples/output_{signerType}_signed.jpg";

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            ISignerCallback signer;

            // Can perform some logic to test with different signers based off of signerType
            signer = new TestUtils.KeyVaultSigner(new DefaultAzureCredential(true));

            // Act
            TestUtils.CreateSignedFile(inputPath, outputPath, signer);

            if (!File.Exists(outputPath)) throw new IOException("Output path was not created.");

            ManifestStoreReader reader = new();

            ManifestStore? store = reader.ReadFromFile(outputPath);
            
            Assert.NotNull(store);

            Manifest? manifest = store.Manifests[store.ActiveManifest];

            // Assert
            Assert.NotNull(manifest);

            Assert.Equal("C# Test Image", manifest.Title);
            Assert.Equal("C# Test", manifest.ClaimGeneratorInfo[0].Name);
            Assert.Equal("Isaiah Carrington", (manifest.Assertions[0].Data as CreativeWorkAssertionData)?.Authors[0].Name);
            Assert.Equal(Relationship.parentOf, manifest.Ingredients[0].Relationship);
            Assert.Equal("jpg", manifest.Format);
        }

        [Fact]
        public void TestMultipleManifestsAddedToFileAndDeserializedCorrectly()
        {
            // Arrange
            string inputPath = "test_samples/multi_sample.jpg";
            string outputPath1 = "test_samples/multi_signed.jpg";
            string outputPath2 = "test_samples/multi_signed2.jpg";

            if (File.Exists(outputPath1))
            {
                File.Delete(outputPath1);
            }

            if (File.Exists(outputPath2))
            {
                File.Delete(outputPath2);
            }

            ISignerCallback signer = new TestUtils.KeyVaultSigner(new DefaultAzureCredential(true));

            ManifestDefinition manifest1 = new() { ClaimGeneratorInfo = [new("Dotnet Multi Test", "1.0.0-alpha.1")] ,Title = "Manifest 1", Format = "jpg" } ;
            ManifestBuilder builder1 = new(new() { ClaimGenerator = "Dotnet Multi Test" }, signer, manifest1);
            CustomAssertion assertion1 = new("Custom Operation", new { name = "ByteDefender", source = "MicroHard" });
            builder1.AddAssertion(assertion1);

            ManifestBuilder builder2 = new(ManifestBuilder.CreateBuilderSettings("Dotnet Multi Test"), signer);
            builder2.SetTitle("Manifest 2");
            builder2.SetFormat("jpg");
            builder2.AddClaimGeneratorInfo("Dotnet Multi Test", "1.0.0-alpha.1");
            builder2.AddAssertion(new ActionAssertion(new() { Action = "A Second Signing", When = "After the first one", SoftwareAgent = "Dotnet SDK", Actors = [ new { name = "Jerry", occupation = "Coder" }] }));

            // Act
            builder1.Sign(inputPath, outputPath1);
            builder2.Sign(outputPath1, outputPath2);

            ManifestStoreReader reader = new();
            ManifestStore? store = reader.ReadFromFile(outputPath2);


            // Assert
            Assert.NotNull(store);

            string active = store.ActiveManifest;
            Dictionary<string, Manifest> manifests = store.Manifests;

            Assert.Equal(2, manifests.Count);
        }
    }
}