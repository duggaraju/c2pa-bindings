using System;
using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.CodeSigning;
using Azure.CodeSigning.Models;

using C2pa;
using C2paExceptions;

namespace C2paSample{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }

        class TrustedSigningCallback : ISignerCallback
        {
            private readonly CertificateProfileClient _client;

            const string EndpointUri = "https://eus.codesigning.azure.net/";
            static readonly Azure.CodeSigning.Models.SignatureAlgorithm Algorithm = Azure.CodeSigning.Models.SignatureAlgorithm.PS384;
            const string CertificateProfile = "media-provenance-sign";
            const string AccountName = "ts-80221a56b4b24529a43e";

            public TrustedSigningCallback()
            {
                string endpoint = "https://eus.codesigning.azure.net/";
                _client = new (new DefaultAzureCredential(), new Uri(endpoint), null);
            }

            public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
            {

                byte[] digest = SHA384.HashData(data.ToArray());

                SignRequest req = new(Algorithm, digest);
                CertificateProfileSignOperation operation = _client.StartSign(AccountName, CertificateProfile, req);
                SignStatus status = operation.WaitForCompletion();
                status.Signature.CopyTo(hash);

                return status.Signature.Length;
            }

            public string GetCertificates() {
                Random random = new();
                byte[] hash = new byte[32];
                random.NextBytes(hash);
                byte[] digest = SHA384.HashData(hash);
                
                SignRequest request = new (Algorithm, digest);
                CertificateProfileSignOperation operation = _client.StartSign(AccountName, CertificateProfile, request);
                SignStatus status = operation.WaitForCompletion();
                var cmsData = new SignedCms();

                return "";
            }

            public SignerConfig Config => new()
            {
                Alg = "ps384",
                Certs = "This is a sample certificate.",
                TimeAuthorityUrl = "http://timestamp.digicert.com",
                UseOcsp = false
            };
        }
    }
}