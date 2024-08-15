using C2pa.Bindings;
using System.Text.Json;

namespace C2pa
{
    public class ManifestBuilder
    {

        private ManifestDefinition _definition;
        private readonly ManifestBuilderSettings _settings;
        private readonly ISignerCallback _callback;
        private C2pa.Bindings.ManifestBuilder? _builder;
        private readonly C2paSigner? _signer;

        private ResourceStore? _resources;

        public unsafe ManifestBuilder(ManifestBuilderSettings settings, ISignerCallback callback, ManifestDefinition definition)
        {
            _settings = settings;
            _callback = callback;
            _definition = definition;
            _builder = c2pa.C2paCreateManifestBuilder(_settings.Settings, _definition.ToJson());

            C2pa.Bindings.SignerCallback c = (data, len, hash, max_len) => Sign(data, len, hash, max_len);
            _signer = c2pa.C2paCreateSigner(c, callback.Config.Config);
        }

        public unsafe ManifestBuilder(ManifestBuilderSettings settings, ISignerCallback callback, string manifestDefintionJsonString):
            this(settings, callback, ManifestDefinition.FromJson(manifestDefintionJsonString))
        {
        }

        private void RebuildBuilder()
        {
            if (_builder != null)
            {
                c2pa.C2paReleaseManifestBuilder(_builder);
                Sdk.CheckError();
            }
            _builder = c2pa.C2paCreateManifestBuilder(_settings.Settings, _definition.ToJson());
            Sdk.CheckError();
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
            if (_builder == null)
            {
                Sdk.CheckError();
            }

            var ret = c2pa.C2paManifestBuilderSign(_builder, _signer, inputStream.CreateStream(), outputStream.CreateStream());
            if (ret != 0)
            {
                Sdk.CheckError();
            }
        }

        public static ManifestBuilderSettings CreateBuilderSettings(string claimGenerator, string TrustSettings = "{}")
        {
            return new ManifestBuilderSettings() { ClaimGenerator = claimGenerator, TrustSettings = TrustSettings };
        }

        public ManifestDefinition GetManifestDefinition()
        {
            return _definition;
        }

        public unsafe string GetBuilderDefinition()
        {
            string defintion = Utils.FromCString(c2pa.C2paGetBuilderDefinition(_builder));
            return defintion;
        }

        public void SetManifestDefinition(ManifestDefinition manifest)
        {
            _definition = manifest;
            RebuildBuilder();
        }

        public void SetFormat(string format)
        {
            _definition.Format = format;
            c2pa.C2paSetBuilderFormat(_builder, format);
            Sdk.CheckError();
        }

        public void SetFormatFromFilename(string filename)
        {
            SetFormat(filename[(filename.LastIndexOf('.') + 1)..]);
        }

        public void SetTitle(string title)
        {
            _definition.Title = title;
            RebuildBuilder();
        }

        public void SetThumbnail(Thumbnail thumbnail)
        {
            _definition.Thumbnail = thumbnail;
            using StreamAdapter dataStream = new(new FileStream(thumbnail.Identifier, FileMode.Open));
            c2pa.C2paSetBuilderThumbnail(_builder, thumbnail.Format, dataStream.CreateStream());
            dataStream.Dispose();
            Sdk.CheckError();
            AddResource(GetBuilderThumbnailUri(), thumbnail.Identifier);
        }

        public void AddAssertion(Assertion assertion)
        {
            _definition.Assertions.Add(assertion);
            c2pa.C2paAddBuilderAssertion(_builder, assertion.Label, assertion.DataAsJson());
            Sdk.CheckError();
        }

        public void AddIngredient(Ingredient ingredient)
        {
            _definition.Ingredients.Add(ingredient);
            using StreamAdapter dataStream = new(new FileStream(ingredient.Title, FileMode.Open));
            c2pa.C2paAddBuilderIngredient(_builder, JsonSerializer.Serialize(ingredient, Utils.JsonOptions), ingredient.Format, dataStream.CreateStream());
            Sdk.CheckError();
        }

        public void AddClaimGeneratorInfo(ClaimGeneratorInfo claimGeneratorInfo)
        {
            _definition.ClaimGeneratorInfo.Add(claimGeneratorInfo);
            RebuildBuilder();
        }

        public unsafe string GetBuilderThumbnailUri(){
            return Utils.FromCString(c2pa.C2paGetBuilderThumbnailUrl(_builder));
        }

        public void AddResource(string identifier, string path)
        {
            _resources ??= new ResourceStore();
            _resources.Resources.Add(identifier, path);
            using StreamAdapter resourceStream = new(new FileStream(path, FileMode.Open));
            c2pa.C2paAddBuilderResource(_builder, identifier, resourceStream.CreateStream());
        }

        public static string GenerateInstanceID()
        {
            return "xmp:iid:" + Guid.NewGuid().ToString();
        }

    }

}