using System.Runtime.InteropServices;
using System.Text.Json;
using C2pa.Bindings;

namespace C2pa
{

    static class Utils
    {
        public unsafe static string FromCString(sbyte* ptr)
        {
            if (ptr == null)
            {
                return string.Empty;
            }
            var value = Marshal.PtrToStringUTF8(new nint(ptr))!;
            c2pa.C2paReleaseString(ptr);
            return value;
        }
    }

    unsafe sealed class StreamAdapter : StreamContext
    {
        private readonly Stream _stream;
        public StreamAdapter(Stream stream) :
            base(GCHandle.ToIntPtr(GCHandle.Alloc(stream)).ToPointer())
        {
            _stream = stream;
        }

        void Dispose(bool disposing)
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

        private static int Seek(nint context, int offset, SeekMode mode)
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

    public class Assertion
    {
        public string Label { get; set; } = string.Empty;

        public dynamic? Data { get; set; }
    }

    public class Manifest
    {

        public string Title { get; set; } = string.Empty;

        public string Format { get; set; } = string.Empty;

        public string ClaimGenerator { get; set; } = string.Empty;

        public object[] ClaimGeneratorInfo { get; set; } = Array.Empty<object>();

        public Assertion[] Assertions { get; set; } = Array.Empty<Assertion>();
    }

    public class ManifestStore
    {
        public string ActiveManifest { get; set; } = string.Empty;

        public Dictionary<string, Manifest> Manifests { get; set; } = new Dictionary<string, Manifest>();
    }


    public interface SignerCallback
    {
        int Sign(ReadOnlySpan<byte> data, Span<byte> hash);
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

        public ManifestBuilderSettingsC Settings => new ManifestBuilderSettingsC
        {
            ClaimGenerator = ClaimGenerator
        };
    }

    public class ManifestBuilder
    {
        private readonly C2pa.Bindings.ManifestBuilder _builder;
        private readonly SignerCallback _callback;
        private readonly C2paSigner _signer;

        public unsafe ManifestBuilder(ManifestBuilderSettings settings, SignerConfig config, SignerCallback callback, string manifest)
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
            return _callback.Sign(span, hash);
        }

        public void Sign(string input, string output)
        {
            using var inputStream = new StreamAdapter(new FileStream(input, FileMode.Open));
            using var outputStream = new StreamAdapter(new FileStream(output, FileMode.Create));
            var ret = c2pa.C2paManifestBuilderSign(_builder, _signer, inputStream.CreateStream(), outputStream.CreateStream());
            Console.WriteLine("Last error is {0} {1}", ret, Sdk.Error);
        }
    }

    internal class ManifestStoreReader
    {
        public unsafe ManifestStore? ReadFromFile(string path)
        {
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

        public unsafe static string Error => Utils.FromCString(c2pa.C2paError());
    }
}