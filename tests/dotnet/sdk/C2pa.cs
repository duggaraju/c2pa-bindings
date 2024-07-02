using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using C2pa.Bindings;
using C2paExceptions;

namespace C2pa
{

    public static class Utils
    {
        public unsafe static string FromCString(sbyte* ptr)
        {
            if (ptr == null)
            {
                return string.Empty;
            }
            var value = Marshal.PtrToStringUTF8(new nint(ptr))!;
            c2pa.C2paReleaseString(ptr);
            // Sdk.CheckError();
            return value;
        }

        public static bool FilePathValid(string path)
        {
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        }
    }

    unsafe class StreamAdapter : StreamContext
    {
        private readonly Stream _stream;
        public StreamAdapter(Stream stream) :
            base(GCHandle.ToIntPtr(GCHandle.Alloc(stream)).ToPointer())
        {
            _stream = stream;
        }

        internal protected override void Dispose(bool disposing, bool callNativeDtor)
        {
            if (disposing)
            {
                GCHandle.FromIntPtr(__Instance).Free();
                _stream.Dispose();
            }
        }

        public C2paStream CreateStream()
        {
            return c2pa.C2paCreateStream(this, Read, Seek, Write);
        }

#if LINUX
        private static int Seek(nint context, long offset, SeekMode mode)
#else
        private static int Seek(nint context, int offset, SeekMode mode)
#endif
        {
            var stream = (Stream)GCHandle.FromIntPtr(context).Target!;
            var origin = mode == SeekMode.Start ? SeekOrigin.Begin : mode == SeekMode.Current ? SeekOrigin.Current : SeekOrigin.End;
            stream.Seek(offset, origin);
            return 0;
        }

        private static long Write(nint context, byte* data, ulong len)
        {
            var stream = (Stream)GCHandle.FromIntPtr(context).Target!;
            var span = new ReadOnlySpan<byte>(data, (int)len);
            stream.Write(span);
            return span.Length;
        }

        private static long Read(nint context, byte* buffer, ulong len)
        {
            var stream = (Stream)GCHandle.FromIntPtr(context).Target!;
            var span = new Span<byte>(buffer, (int)len);
            return stream.Read(span);
        }
    }

    // Example manifest JSON
    // {
    //     "claim_generator_info": [
    //         {
    //             "name": "{{claimName}}",
    //             "version": "0.0.1"
    //         }
    //     ],
    //     "format": "{{ext}}",
    //     "title": "{{manifestTitle}}",
    //     "ingredients": [],
    //     "assertions": [
    //         {   "label": "stds.schema-org.CreativeWork",
    //             "data": {
    //                 "@context": "http://schema.org/",
    //                 "@type": "CreativeWork",
    //                 "author": [
    //                     {   "@type": "Person",
    //                         "name": "{{authorName}}"
    //                     }
    //                 ]
    //             },
    //             "kind": "Json"
    //         }
    //     ]
    // }

    public class AuthorInfo(string type, string name)
    {
        [JsonPropertyName("@type")]
        public string Type { get; set; } = type;

        public string Name { get; set; } = name;
    }

    public class AssertionData(string? context, string? type, AuthorInfo[] author)
    {
        [JsonPropertyName("@context")]
        public string? Context { get; init; } = context;

        [JsonPropertyName("@type")]
        public string? Type { get; init; } = type;

        public AuthorInfo[] Author { get; init; } = author;
    }


    public record Assertion(string Label, AssertionData Data, string Kind = "Json");

    public record Ingredient(string Title, string Format, string InstanceId);

    public record ClaimGeneratorInfoData(string Name = "", string Version = "");

    public class Manifest
    {
        private readonly JsonSerializerOptions _options;

        public Manifest () {
            _options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
        }

        public ClaimGeneratorInfoData[] ClaimGeneratorInfo { get; set; } = Array.Empty<ClaimGeneratorInfoData>();
        
        public string Format { get; set; } = string.Empty;
        
        public string Title { get; set; } = string.Empty;

        public Ingredient[] Ingredients { get; set; } = Array.Empty<Ingredient>();

        public Assertion[] Assertions { get; set; } = Array.Empty<Assertion>();

        public string GetManifestJson()
        {
            return JsonSerializer.Serialize(this, _options);
        }
    }

    public class ManifestStore
    {
        public string ActiveManifest { get; set; } = string.Empty;

        public Dictionary<string, Manifest> Manifests { get; set; } = new Dictionary<string, Manifest>();
    }


    public interface ISignerCallback
    {
        int Sign(ReadOnlySpan<byte> data, Span<byte> hash);
        SignerConfig Config { get; }
    }

    public class SignerConfig
    {
        public string Alg { get; set; } = string.Empty;

        public string Certs { get; set; } = string.Empty;

        public string? TimeAuthorityUrl { get; set; }

        public bool UseOcsp { get; set; } = false;

        public SignerConfigC Config => new SignerConfigC
        {
                Alg = Alg,
                Certs = Certs,
                TimeAuthorityUrl = TimeAuthorityUrl,
                UseOcsp = UseOcsp
        };
    }

    public class ManifestBuilderSettings
    {
        public string ClaimGenerator { get; set; } = string.Empty;

        public string TrustSettings { get; set; } = "{}";

        public ManifestBuilderSettingsC Settings => new ManifestBuilderSettingsC
        {
            ClaimGenerator = ClaimGenerator,
            Settings = TrustSettings
        };
    }

    public class ManifestBuilder
    {
        private readonly C2pa.Bindings.ManifestBuilder _builder;
        private readonly ISignerCallback _callback;
        private readonly C2paSigner _signer;

        public unsafe ManifestBuilder(ManifestBuilderSettings settings, SignerConfig config, ISignerCallback callback, string manifest)
        {
            _builder = c2pa.C2paCreateManifestBuilder(settings.Settings, manifest);
            _callback = callback;
            C2pa.Bindings.SignerCallback c = (data, len, hash, max_len) => Sign(data, len, hash, max_len); 
            _signer = c2pa.C2paCreateSigner(c, config.Config);
        }

        unsafe long Sign(byte* data, ulong len, byte* signature, long sig_max_size)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            Sdk.CheckError();
            return _callback.Sign(span, hash);
        }

        public void Sign(string input, string output)
        {
            if (!Utils.FilePathValid(input))
            {
                throw new ArgumentException("Invalid file path provided.", nameof(input));
            }
            using var inputStream = new StreamAdapter(new FileStream(input, FileMode.Open));
            using var outputStream = new StreamAdapter(new FileStream(output, FileMode.Create));
            var ret = c2pa.C2paManifestBuilderSign(_builder, _signer, inputStream.CreateStream(), outputStream.CreateStream());
            Sdk.CheckError();
        }
    }

    public class ManifestStoreReader
    {
        public unsafe ManifestStore? ReadFromFile(string path)
        {
            if (!Utils.FilePathValid(path))
            {
                throw new ArgumentException("Invalid file path provided.", nameof(path));
            }
            using var adapter = new StreamAdapter(new FileStream(path, FileMode.Open));
            var c2paStream = adapter.CreateStream();
            var json = Utils.FromCString(c2pa.C2paVerifyStream(c2paStream));
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            // Console.WriteLine("Manifest: {0}", json);
            var manifestStore = JsonSerializer.Deserialize<ManifestStore>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                IgnoreReadOnlyProperties = false
            });
            c2pa.C2paReleaseStream(c2paStream);
            Sdk.CheckError();
            return manifestStore;
        }

    }

    /// <summary>
    /// Top  level SDK entry point.
    /// </summary>
    public static class Sdk
    {
        /// <summary>
        /// The version of the Sdk.
        /// </summary>
        public unsafe static string Version => Utils.FromCString(c2pa.C2paVersion());

        public unsafe static string[] SupportedExtensions
        {
            get
            {
                var json =  Utils.FromCString(c2pa.C2paSupportedExtensions());
                var doc = JsonDocument.Parse(json);
                var extensions = doc.RootElement.EnumerateArray().Select(e => e.GetString()!).ToArray();
                return extensions;
            }
        }

        public unsafe static void CheckError () {
            string err = Utils.FromCString(c2pa.C2paError());
            if (string.IsNullOrEmpty(err)) return;
            
            string errType = err.Split(' ')[0];
            string errMsg = err;

            Exception? exception = ExceptionFactory.GetException(errType, errMsg);
            if (exception != null) {
                throw exception;
            }
        }
    }
}