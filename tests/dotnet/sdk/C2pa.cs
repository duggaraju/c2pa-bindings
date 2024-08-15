using System.Runtime.InteropServices;
using System.Text.Json;
using C2pa.Bindings;

namespace C2pa
{
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

        public static void CheckError()
        {
            string err;
            unsafe
            {
                err = Utils.FromCString(c2pa.C2paError());
            }
            if (string.IsNullOrEmpty(err)) return;

            string errType = err.Split(' ')[0];
            string errMsg = err;

            Exception? exception = ExceptionFactory.GetException(errType, errMsg);
            if (exception != null)
            {
                throw exception;
            }
        }
    }
}