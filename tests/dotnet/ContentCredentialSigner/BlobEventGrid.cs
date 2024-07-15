using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ContentCredentialSigner
{
    public class BlobEventGrid
    {
        const string InputFile = "grid-input-files/{name}";
        const string OutputFile = "grid-output-files/{name}";
        const string ConnectionString = "BlobStorage";

        private readonly ILogger<BlobEventGrid> _logger;
        private readonly TokenCredential _credential;

        public BlobEventGrid(ILogger<BlobEventGrid> logger, TokenCredential credential)
        {
            _logger = logger;
            _credential = credential;
        }

        [Function(nameof(BlobEventGrid))]
        [BlobOutput(OutputFile, Connection = ConnectionString)]
        public async Task<Stream> Run([BlobTrigger(InputFile, Source = BlobTriggerSource.EventGrid, Connection = ConnectionString)] byte[] stream, string name)
        {
            _logger.LogInformation($"C# Blob Trigger (using Event Grid) processed blob\n Name: {name}");
            try
            {
                var tempDir = Path.GetTempPath();
                var inputFile = Path.Combine(tempDir, Path.GetTempFileName());
                var outputFile = Path.Combine(tempDir, Path.GetTempFileName());
                using (var input = File.OpenWrite(inputFile))
                    await input.WriteAsync(stream);
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
