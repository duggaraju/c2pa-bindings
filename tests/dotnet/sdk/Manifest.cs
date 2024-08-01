using System.Text.Json;

namespace C2pa
{

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

    // Ingredient
    public class HashedUri(string url, string alg, byte[] hash, byte[] salt)
    {
        public string Url { get; set; } = url;
        public string Alg { get; set; } = alg;
        public byte[] Hash { get; set; } = hash;
        public byte[] Salt { get; set; } = salt;
    }

    public record ValidationStatus(string code, string url = "", string explanation = "");

    public enum Relationship
    {
        ParentOf,
        ComponentOf,
        InputTo,
        None
    }

    // Manifest
    public record ClaimGeneratorInfo(string Name = "", string Version = "");

    public class Ingredient(string title = "", string format = "", Relationship relationship = Relationship.None)
    {
        public string Title { get; set; } = title;
        public string Format { get; set; } = format;
        public Relationship Relationship { get; set; } = relationship;
        public string DocumentID { get; set; } = "";
        public string InstanceID { get; set; } = "";
        public HashedUri? HashedManifestUri { get; set; } = null;
        public List<ValidationStatus>? ValidationStatus { get; set; } = [];
        public HashedUri? Thumbnail { get; set; } = null;
        public HashedUri? Data { get; set; } = null;
        public string Description { get; set; } = "";
        public string InformationalUri { get; set; } = "";
    }


    public class Manifest
    {
        public string ClaimGenerator { get; set; } = string.Empty;

        public List<ClaimGeneratorInfo> ClaimGeneratorInfo { get; set; } = [];

        public string Format { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public List<Ingredient> Ingredients { get; set; } = [];

        public List<BaseAssertion> Assertions { get; set; } = [];

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
}
