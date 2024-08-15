using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using C2pa;

namespace ContentCredentialSigner
{
    public class FileVerifier
    {

        private static HttpClient sharedClient = new HttpClient();

        private readonly ILogger<FileVerifier> _logger;

        public FileVerifier(ILogger<FileVerifier> logger)
        {
            _logger = logger;
        }

        [Function("verify_file")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req, FunctionContext executionContext)
        {
            string? url = req.Query["url"];
            HttpResponseData response;

            if (string.IsNullOrEmpty(url))
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Expected a query parameter named 'url' with a valid URL. Ex: ?url=https://example.com/image.jpg");
                return response;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("The 'url' query parameter must be a valid URL. Ex: ?url=https://example.com/image.jpg");
                return response;
            }

            string filename = Path.GetTempFileName();
            try
            {
                var imageResponse = await sharedClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                // var contentType = imageResponse.Content.Headers.GetValues("Content-Type").Single();
                // var format = contentType.Substring(contentType.LastIndexOf('/') + 1);

                using (var fstream = File.OpenWrite(filename))
                {
                    await imageResponse.Content.CopyToAsync(fstream);
                }

                var manifestStoreReader = new ManifestStoreReader();
                var manifestStore = manifestStoreReader.ReadJsonFromFile(filename);

                if (manifestStore == null)
                {
                    response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync<object>(null);
                }
                else
                {
                    response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(manifestStore);
                }

            }
            catch (Exception e)
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Failed to download image from URL: " + e.ToString());
            }
            finally
            {
                File.Delete(filename);
            }

            return response;
        }
    }
}
