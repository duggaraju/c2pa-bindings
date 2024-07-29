using System;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using C2pa;

namespace sdktests
{
    public class AssertionTypeTesting
    {
        [Fact]
        public void TestingActionAssertTypeMaintainsDataDuringSerialization()
        {
            // Arrange 
            ActionAssertionData data = new()
            {
                Action = "Some Action",
                When = new DateTime(2024, 7, 18).ToString("yyyy-MM-ddTHH:mm:ss"),
                SoftwareAgent = "Some Software Agent",
                Changed = "Some Changed",
                InstanceID = "u11245151",
            };

            ActionAssertion assertion = new(data);

            // Act

            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            ActionAssertion? result = JsonSerializer.Deserialize<ActionAssertion>(json, BaseAssertion.JsonOptions);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(assertion.Label, result.Label);
            Assert.Equal(data.Action, result.Data.Action);
            Assert.Equal(data.When, result.Data.When);
        }

        [Fact]
        public void TestCustomAssertionMaintainsDataDuringSerialization()
        {
            // Arrange
            var data = new {
                key1= "value1",
                key2= "value2",
                key3 = 1234,
                key4 = new
                {
                    key5 = true
                }
            };

            CustomAssertion assertion = new("Some Unique Label", data);
            // Act

            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            CustomAssertion? result = JsonSerializer.Deserialize<CustomAssertion>(json, BaseAssertion.JsonOptions);
            dynamic? resultData = result?.GetDataAsExpandoObject();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(resultData);
            Assert.Equal(assertion.Label, result.Label);
            Assert.Equal(data.key1, resultData?.key1);
            Assert.Equal(data.key4.key5, resultData?.key4.key5);
        }

        [Fact]
        public void TestCustomAssertionCanUseOtherAssertionDataTypesForDataField()
        {
            // Arrange
            CreativeWorkAssertionData data = new() { Context = "Some important Context", Type = "Test", Authors = [new("Person", "Test Account 1")]};
            CustomAssertion assertion = new("Some Unique Label", data);

            // Act
            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            CustomAssertion? result = JsonSerializer.Deserialize<CustomAssertion>(json, BaseAssertion.JsonOptions);
            dynamic? resultData = result?.GetDataAsExpandoObject();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(resultData);
            Assert.Equal(assertion.Label, result.Label);
            Assert.Equal(data.Context, resultData?.context);
        }

        [Fact]
        public void TestRegexMatchForThumbnailAssertionsReturnsCorrectObject()
        {
            // Arrange
            ThumbnailAssertionData data = new() { Thumbnail = "some_thumbnail.png", InstanceID = "u181241" };
            ClaimThumbnailAssertion assertion = new(data);

            // Act
            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            object? result = JsonSerializer.Deserialize<BaseAssertion>(json, BaseAssertion.JsonOptions);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ClaimThumbnailAssertion>(result);
        }

        [Theory]
        [InlineData("NotIngredientLabelAtAll", "CustomAssertion")]
        [InlineData("c2pa.thumbnail.ingredient.v2", "IngredientThumbnailAssertion")]
        public void TestRegexReturnsCorrectThumbnailAssertionGivenLabelSuffix(string label, string assertType)
        {
            // Arrange
            ThumbnailAssertionData data = new() { Thumbnail = "some_thumbnail.png", InstanceID = "u181241" };
            CustomAssertion assertion = new(label, data);

            // Act

            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            object? result = JsonSerializer.Deserialize<BaseAssertion>(json, BaseAssertion.JsonOptions);

            // Assert

            Assert.NotNull(result);
            string typeName = result.GetType().Name;
            Assert.Equal(assertType, result.GetType().Name);
        }

        [Theory]
        [InlineData("c2pa.action", "ActionAssertion")]
        [InlineData("c2pa.thumbnail", "ThumbnailAssertion")]
        [InlineData("c2pa.thumbnail.claim", "ClaimThumbnailAssertion")]
        [InlineData("c2pa.thumbnail.ingredient", "IngredientThumbnailAssertion")]
        [InlineData("stds.schema-org.CreativeWork", "CreativeWorkAssertion")]
        public void TestAllAssertionTypesReturnCorrectlyFromJsonByLabel(string label, string assertType)
        {
            // Arrange

            CustomAssertion assertion = new(label, new BaseAssertionData());

            // Act
            string json = JsonSerializer.Serialize(assertion, BaseAssertion.JsonOptions);

            object? result = JsonSerializer.Deserialize<BaseAssertion>(json, BaseAssertion.JsonOptions);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(assertType, result.GetType().Name);
        }

        [Fact]
        public void TestManifestStoreDeserializesAssertionTypesCorrectly()
        {
            // Arrange
            TestUtils.KeyVaultSigner signer = new(new DefaultAzureCredential(true));
            string inputPath = "..\\..\\..\\test_samples\\assertion_sample.jpg";
            string outputPath = "..\\..\\..\\test_samples\\assertion_sample_signed.jpg";

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            TestUtils.CreateSignedFile(inputPath, outputPath, signer);

            // Act

            ManifestStoreReader reader = new();
            ManifestStore? store = reader.ReadFromFile(outputPath);

            Assert.NotNull(store);

            Manifest manifest = store.Manifests[store.ActiveManifest];

            dynamic assertion = manifest.Assertions[0];

            // Assert

            Assert.Equal("stds.schema-org.CreativeWork", assertion.Label);
            Assert.Equal("Person", assertion.Data.Authors[0].Type);
            Assert.IsType<CreativeWorkAssertion>(assertion);
        }
    }
}
