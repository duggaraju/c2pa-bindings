using System.Runtime.InteropServices;
using System.Text.Json;
using C2pa.Bindings;

namespace C2pa
{

    public static partial class Utils
    {
        public unsafe static string FromCString(sbyte* ptr, bool ownsResource = false)
        {
            if (ptr == null)
            {
                return string.Empty;
            }
            var value = Marshal.PtrToStringUTF8(new nint(ptr))!;
            if (!ownsResource) 
                c2pa.C2paReleaseString(ptr);
            
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

#if WINDOWS
        private static int Seek(nint context, int offset, SeekMode mode)
#else
        private static int Seek(nint context, long offset, SeekMode mode)
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


    public class Ingredient(string title = "", string format = "", Relationship relationship = Relationship.parentOf) {
        public string Title { get; set; } = title;
        public string Format { get; set; } = format;
        public Relationship Relationship { get; set; } = relationship;
        public string? DocumentID { get; set; } = null;
        public string? InstanceID { get; set; } = null;
        public HashedUri? C2paManifest { get; set; } = null;
        public HashedUri? HashedManifestUri { get; set; } = null;
        public List<ValidationStatus>? ValidationStatus { get; set; } = null;
        public Thumbnail? Thumbnail { get; set; } = null;
        public HashedUri? Data { get; set; } = null;
        public string? Description { get; set; } = null;
        public string? InformationalUri { get; set; } = null;
    }


    public class Manifest (string format = "application/octet-stream")
    {
        public List<ClaimGeneratorInfoData> ClaimGeneratorInfo { get; set; } = [];
        
        public string Format { get; set; } = format;
        
        public string? Title { get; set; } = null;

        public List<Ingredient> Ingredients { get; set; } = [];

        public List<BaseAssertion> Assertions { get; set; } = [];

        public string GetManifestJson()
        {
            return JsonSerializer.Serialize(this, BaseAssertion.JsonOptions);
        }
    }

    public class ManifestDefinition (string format = "application/octet-stream")
    {
        public string? Vendor { get; set; } = null;
        public List<ClaimGeneratorInfoData> ClaimGeneratorInfo { get; set; } = [];
        public string? Title { get; set; } = null;
        public string Format { get; set; } = format;
        public string InstanceID { get; set; } = ManifestBuilder.GenerateInstanceID();
        public Thumbnail? Thumbnail { get; set; } = null;
        public List<Ingredient> Ingredients { get; set; } = [];
        public List<BaseAssertion> Assertions { get; set; } = [];
        public List<string>? Redactions { get; set; } = null;
        public string? Label { get; set; } = null;

        public string GetManifestJson()
        {
            return JsonSerializer.Serialize(this, BaseAssertion.JsonOptions);
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

    public class ManifestStoreReader
    {
        public unsafe string? ReadJsonFromFile(string path)
        {
            if (!Utils.FilePathValid(path))
            {
                throw new ArgumentException("Invalid file path provided.", nameof(path));
            }
            using var adapter = new StreamAdapter(new FileStream(path, FileMode.Open));
            var c2paStream = adapter.CreateStream();
            try
            {
                var manifest = c2pa.C2paVerifyStream(c2paStream);
                Sdk.CheckError();
                var json = Utils.FromCString(manifest);
                Console.WriteLine("Manifest: {0}", json);
                return json;
            }
            finally
            {
                c2pa.C2paReleaseStream(c2paStream);
            }
        }

        public unsafe ManifestStore? ReadFromFile(string path)
        {
            var json = ReadJsonFromFile(path);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            return JsonSerializer.Deserialize<ManifestStore>(json, BaseAssertion.JsonOptions);
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
        public static string Version
        {
            get
            {
                unsafe
                {
                    return Utils.FromCString(c2pa.C2paVersion());
                }
            }
        }

        public static string[] SupportedExtensions
        {
            get
            {
                unsafe
                {
                    var json = Utils.FromCString(c2pa.C2paSupportedExtensions());
                    var doc = JsonDocument.Parse(json);
                    var extensions = doc.RootElement.EnumerateArray().Select(e => e.GetString()!).ToArray();
                    return extensions;
                }
            }
        }

        public static void CheckError () {
            string err;
            unsafe
            {
                err = Utils.FromCString(c2pa.C2paError());
            }
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