using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Azure.Core;
using Microsoft.Extensions.Logging;
using System.Net;
using C2pa;

namespace ContentCredentialSigner
{
    internal class Function
    {
        private readonly ILogger _logger;
        private readonly TokenCredential _credential;

        public Function(ILoggerFactory loggerFactory, TokenCredential credential)
        {
            _logger = loggerFactory.CreateLogger<Function>();
            _credential = credential;
        }

        [Function("Sign")]
        public async Task<HttpResponseData> SignBody([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger signing function processed a request.");
            try
            {
                var contentType = req.Headers.GetValues("Content-Type").SingleOrDefault("application/binary");
                var format = contentType.Substring(contentType.LastIndexOf('/') + 1);
                
                var tempDir = Path.GetTempPath();
                var inputFile = Path.Combine(tempDir, Path.GetTempFileName());
                var outputFile = Path.Combine(tempDir, Path.GetTempFileName());
                using (var stream = File.OpenWrite(inputFile))
                    await req.Body.CopyToAsync(stream);

                Signer.SignFile(format, inputFile, outputFile, _credential);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", contentType);
                using (var stream = File.OpenRead(outputFile))
                    await stream.CopyToAsync(response.Body);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing data...");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync(ex.ToString());
                return response;
            }
        }

        [Function("Verify")]
        public async Task<HttpResponseData> VerifyBody([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger verification function processed a request.");
            var contentType = req.Headers.GetValues("Content-Type").SingleOrDefault("application/binary");
            var inputFile = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            using (var stream = File.OpenWrite(inputFile))
                await req.Body.CopyToAsync(stream);
            var manifest = new ManifestStoreReader().ReadJsonFromFile(inputFile);
            var response = req.CreateResponse();
            if (manifest == null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Headers.Add("Content-Type", "text/plain");
                await response.WriteStringAsync("Content credentials not found");
            }
            else
            {
                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(manifest);
            }
            return response;
        }
    }
}
