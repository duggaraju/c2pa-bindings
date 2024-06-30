using System.Runtime.InteropServices;
using System.Text.Json;
using C2pa.Bindings;

namespace C2pa
{
    class C2paException : Exception
    {
        public C2paException(string message) : base(message) 
        {
        }
    }

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


    public interface ISignerCallback
    {
        int Sign(ReadOnlySpan<byte> data, Span<byte> hash);

        SignerConfig Config { get; }
    }

    public interface ICustomSignerCallback : ISignerCallback
    {
        int Timestamp(ReadOnlySpan<byte> data, Span<byte> hash);
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

    public sealed class ManifestBuilder: IDisposable
    {
        private readonly C2pa.Bindings.ManifestBuilder _builder;

        public unsafe ManifestBuilder(ManifestBuilderSettings settings, string manifest)
        {
            _builder = c2pa.C2paCreateManifestBuilder(settings.Settings, manifest);
        }

        public void Dispose()
        {
            c2pa.C2paReleaseManifestBuilder(_builder);
        }

        unsafe static C2paSigner CreateSigner(ISignerCallback callback)
        {
            C2pa.Bindings.SignerCallback c = (data, len, hash, max_len) => Sign(callback, data, len, hash, max_len);
            return c2pa.C2paCreateSigner(c, callback.Config.Config);
        }

        unsafe static C2paCustomSigner CreateSigner(ICustomSignerCallback callback)
        {
            C2pa.Bindings.SignerCallback c = (data, len, hash, max_len) => Sign(callback, data, len, hash, max_len);
            C2pa.Bindings.TimestamperCallback t = (data, len, hash, max_len) => Timestamp(callback, data, len, hash, max_len);
            return c2pa.C2paCreateCustomSigner(c, t, callback.Config.Config);
        }

        unsafe static long Sign(ISignerCallback callback, byte* data, ulong len, byte* signature, long sig_max_size)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            return callback.Sign(span, hash);
        }

        unsafe static long Timestamp(ICustomSignerCallback callback, byte* data, ulong len, byte* signature, long sig_max_size)
        {
            var span = new ReadOnlySpan<byte>(data, (int)len);
            var hash = new Span<byte>(signature, (int)sig_max_size);
            return callback.Timestamp(span, hash);
        }

        public void Sign(string input, string output, ISignerCallback callback)
        {
            using var inputStream = new StreamAdapter(new FileStream(input, FileMode.Open));
            using var outputStream = new StreamAdapter(new FileStream(output, FileMode.Create));
            var signer = CreateSigner(callback);
            var ret = c2pa.C2paManifestBuilderSign(_builder, signer, inputStream.CreateStream(), outputStream.CreateStream());
            if (ret != 0)
            {
                throw new C2paException(Sdk.Error);
            }
        }

        public void Sign(string input, string output, ICustomSignerCallback callback)
        {
            using var inputStream = new StreamAdapter(new FileStream(input, FileMode.Open));
            using var outputStream = new StreamAdapter(new FileStream(output, FileMode.Create));
            var signer = CreateSigner(callback);
            var ret = c2pa.C2paManifestBuilderCustomSign(_builder, signer, inputStream.CreateStream(), outputStream.CreateStream());
            if (ret != 0)
            {
                throw new C2paException(Sdk.Error);
            }
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

        internal unsafe static string Error => Utils.FromCString(c2pa.C2paError());
    }
}