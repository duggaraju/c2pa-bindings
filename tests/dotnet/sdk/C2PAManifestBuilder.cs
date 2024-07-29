using System;
using C2pa;
using C2pa.Bindings;
using System.Text.Json;

namespace C2pa
{
    public class ManifestBuilder{

        private Manifest _manifest;
        private readonly ManifestBuilderSettings _settings;
        private readonly ISignerCallback _callback;
        private C2pa.Bindings.ManifestBuilder? _builder;
        private C2paSigner? _signer;

        public unsafe ManifestBuilder (ManifestBuilderSettings settings, ISignerCallback callback, Manifest manifest){
            _settings = settings;
            _callback = callback;
            _manifest = manifest;

            C2pa.Bindings.SignerCallback c = (data, len, hash, max_len) => Sign(data, len, hash, max_len);
            _signer = c2pa.C2paCreateSigner(c, callback.Config.Config);
        }

        public unsafe ManifestBuilder ( ManifestBuilderSettings settings, ISignerCallback callback){
            _settings = settings;
            _callback = callback;
            _manifest = new Manifest();

            C2pa.Bindings.SignerCallback c = (data, len, hash, max_len) => Sign(data, len, hash, max_len);
            _signer = c2pa.C2paCreateSigner(c, callback.Config.Config);
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
            _builder = c2pa.C2paCreateManifestBuilder(_settings.Settings, _manifest.GetManifestJson());
            var ret = c2pa.C2paManifestBuilderSign(_builder, _signer, inputStream.CreateStream(), outputStream.CreateStream());
            c2pa.C2paReleaseManifestBuilder(_builder);
            Sdk.CheckError();
        }

        public static ManifestBuilderSettings CreateBuilderSettings(string claimGenerator, string TrustSettings = "{}"){
            return new ManifestBuilderSettings(){ClaimGenerator = claimGenerator, TrustSettings = TrustSettings};
        }

        public Manifest GetManifest()
        {
            return _manifest;
        }

        public void FromJsonFile(string path)
        {
            string json = System.IO.File.ReadAllText(path);
            FromJson(json);
        }
        
        public void FromJson(string json)
        {
            _manifest = JsonSerializer.Deserialize<Manifest>(json, BaseAssertion.JsonOptions) ?? throw new JsonException("Manifest is null || Invalid JSON provided.");
        }

        public void FromManifest(Manifest manifest)
        {
            _manifest = manifest;
        }

        public void AddClaimGeneratorInfo(ClaimGeneratorInfoData claimGeneratorInfo)
        {
            _manifest.ClaimGeneratorInfo.Add(claimGeneratorInfo);
        }

        public void AddClaimGeneratorInfo(string name, string version)
        {
            _manifest.ClaimGeneratorInfo.Add(new ClaimGeneratorInfoData(name, version));
        }

        public void SetFormat(string format)
        {
            _manifest.Format = format;
        }

        public void SetFormatFromFilename(string filename){
            _manifest.Format = filename[(filename.LastIndexOf('.') + 1)..];
        }

        public void SetTitle(string title)
        {
            _manifest.Title = title;
        }

        public void AddAssertion(BaseAssertion assertion)
        {
           _manifest.Assertions.Add(assertion);
        }

        public void AddIngredient(Ingredient ingredient)
        {
            _manifest.Ingredients.Add(ingredient);
        }
    }
}