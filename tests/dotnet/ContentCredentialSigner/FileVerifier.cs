using System.Net;
using System.Text;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using C2pa;

namespace Isaiah.DemoVerify
{
    public class verify_file
    {

        private static HttpClient sharedClient = new HttpClient();

        private readonly ILogger<verify_file> _logger;

        public verify_file(ILogger<verify_file> logger)
        {
            _logger = logger;
        }

        [Function("verify_file")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req, FunctionContext executionContext)
        {
            string? url = req.Query["image_url"];

            HttpResponseData response;

            if (string.IsNullOrEmpty(url))
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Expected a query parameter named 'image_url' with a valid URL. Ex: ?image_url=https://example.com/image.jpg");
                return response;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("The 'image_url' query parameter must be a valid URL. Ex: ?image_url=https://example.com/image.jpg");
                return response;
            }

            byte[] imageResponse;
            try
            {
                imageResponse = await sharedClient.GetByteArrayAsync(url);
                string decodedString = Encoding.UTF8.GetString(imageResponse);
                if (decodedString.Contains("html"))
                {
                    response = req.CreateResponse(HttpStatusCode.BadRequest);
                    await response.WriteStringAsync("Failed to download image from URL: The URL provided does not point to an image file. Be sure to use the image url directly.");
                    return response;
                }
            }
            catch (Exception e)
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Failed to download image from URL: " + e.Message);
                return response;
            }

            string filename = Path.GetTempFileName();

            using Stream fstream = new FileStream(filename, FileMode.Open, FileAccess.Write);
            await fstream.WriteAsync(imageResponse, 0, imageResponse.Length);
            fstream.Close();

            ManifestStoreReader manifestStoreReader = new ManifestStoreReader();
            ManifestStore? manifestStore = manifestStoreReader.ReadFromFile(filename);

            if (manifestStore == null || manifestStore.Manifests[manifestStore.ActiveManifest] == null)
            {
                response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync("{\"error\": \"No manifest was found on the image\"}");
                return response;
            }

            Manifest manifest = manifestStore.Manifests[manifestStore.ActiveManifest];

            response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(manifest.GetManifestJson());

            return response;
        }
    }
}
