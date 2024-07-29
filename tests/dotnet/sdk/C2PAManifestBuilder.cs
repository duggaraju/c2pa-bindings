using System;
using C2pa;
using System.Text.Json;

namespace C2pa
{
    public partial class ManifestBuilder{

        private Manifest _manifest;

        public static ManifestBuilderSettings CreateBuilderSettings(string claimGenerator, string TrustSettings = "{}"){
            return new ManifestBuilderSettings(){ClaimGenerator = claimGenerator, TrustSettings = TrustSettings};
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