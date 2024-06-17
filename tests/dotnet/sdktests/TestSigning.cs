using C2pa;
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
    }
}