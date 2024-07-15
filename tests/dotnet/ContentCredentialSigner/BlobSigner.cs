using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ContentCredentialSigner
{
    public class BlobSigner
    {
        const string InputFile = "input-files/{name}";
        const string OutputFile = "output-files/{name}";
        const string ConnectionString = "BlobStorage";

        private readonly ILogger<BlobSigner> _logger;
        private readonly TokenCredential _credential;

        public BlobSigner(ILogger<BlobSigner> logger, TokenCredential credential)
        {
            _logger = logger;
            _credential = credential;
        }

        [Function(nameof(BlobSigner))]
        [BlobOutput(OutputFile, Connection = ConnectionString)]
        public async Task<Stream> Run([BlobTrigger(InputFile, Connection = ConnectionString)] byte[] input, string name)
        {
            _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n");
            try
            {
                var tempDir = Path.GetTempPath();
                var inputFile = Path.Combine(tempDir, Path.GetTempFileName());
                var outputFile = Path.Combine(tempDir, Path.GetTempFileName());
                using (var stream = File.OpenWrite(inputFile))
                    await stream.WriteAsync(input);
                Signer.SignFile(Path.GetExtension(name).Substring(1), inputFile, outputFile, _credential);
                return File.OpenRead(outputFile);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error processing blob");
                throw;
            }
        }
    }
}
