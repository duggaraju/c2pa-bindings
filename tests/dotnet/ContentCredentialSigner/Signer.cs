using Azure.Core;
using C2pa;

namespace ContentCredentialSigner
{
    public static class Signer
    {
        static readonly ManifestBuilderSettings Settings = new()
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

        static Manifest GetManifest(string format) => new()
        {
            ClaimGeneratorInfo = [
                new ClaimGeneratorInfoData
                {
                    Name = "Azure Function Test",
                    Version = "1.0.0"
                }
            ],
            Format = format,
            Title = "C# Test Image",
            Assertions = [
                new("stds.schema-org.CreativeWork",
                    new AssertionData(
                        "http://schema.org/",
                        "CreativeWork",
                        [
                            new AuthorInfo("person", "Prakash Duggaraju")
                        ]
                    )
                )]
        };

        public static void SignFile(string format, string inputFile, string outputFile, TokenCredential credential)
        {
            TrustedSigner signer = new(credential);
            SignerConfig config = signer.Config;
            var manifest = GetManifest(format);
            ManifestBuilder builder = new(Settings, config, signer, manifest.GetManifestJson());
            builder.Sign(inputFile, outputFile);
        }

        public static ManifestStore? ReadManifestStore(string inputFile)
        {
            ManifestStoreReader reader = new();
            return reader.ReadFromFile(inputFile);
        }
    }
}
