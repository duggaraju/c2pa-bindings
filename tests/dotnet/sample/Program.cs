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
            
        }

        class TrustedSigner(TokenCredential credential) : ISignerCallback
        {
            const string EndpointUri = "https://eus.codesigning.azure.net/";
            static readonly Azure.CodeSigning.Models.SignatureAlgorithm Algorithm = Azure.CodeSigning.Models.SignatureAlgorithm.PS384;
            const string CertificateProfile = "media-provenance-sign";
            const string AccountName = "ts-80221a56b4b24529a43e";

            private readonly CertificateProfileClient _client = new(credential, new Uri(EndpointUri));

            public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
            {

                byte[] digest = GetDigest(data);

                SignRequest req = new(Algorithm, digest);
                CertificateProfileSignOperation operation = _client.StartSign(AccountName, CertificateProfile, req);
                SignStatus status = operation.WaitForCompletion();
                status.Signature.CopyTo(hash);

                return status.Signature.Length;
            }

            private static byte[] GetDigest(ReadOnlySpan<byte> data)
            {
                byte[] digest = SHA384.HashData(data.ToArray());
                return digest;
            }

            public string GetCertificates() {
                Random random = new();
                byte[] hash = new byte[32];
                random.NextBytes(hash);
                byte[] digest = GetDigest(hash);
                
                SignRequest request = new (Algorithm, digest);
                CertificateProfileSignOperation operation = _client.StartSign(AccountName, CertificateProfile, request);
                SignStatus status = operation.WaitForCompletion();

                SignedCms cmsData = new();
                char[] cdata = Encoding.ASCII.GetChars(status.SigningCertificate);
                byte[] certificate = Convert.FromBase64CharArray(cdata, 0, cdata.Length);
                
                cmsData.Decode(certificate);

                StringBuilder builder = new();

                foreach (var cert in cmsData.Certificates)
                {
                    Console.WriteLine("Subject = {0} Issuer = {1} Expiry = {2}", cert.Subject, cert.Issuer, cert.GetExpirationDateString());
                    builder.AppendLine($"subject={cert.Subject}");
                    builder.AppendLine($"issuer={cert.Issuer}");
                    var data = PemEncoding.Write("CERTIFICATE", cert.RawData);
                    builder.AppendLine(new String(data));
                }

                string pem = builder.ToString();
                return pem;
            }

            public SignerConfig Config => new()
            {
                Alg = "ps384",
                Certs = GetCertificates(),
                TimeAuthorityUrl = "http://timestamp.digicert.com",
                UseOcsp = false
            };
        }
    }
}