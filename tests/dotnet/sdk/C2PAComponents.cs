// This file contains C# forms of the Rust C2PA sub-components used in signing

using System;
using System.Text.Json.Serialization;
using C2pa;
using C2pa.Bindings;

namespace C2pa{

    // Ingredient

    // Manifest

    // General
    public class Thumbnail (string format = "", string identifier = "") : ResourceRef (format, identifier);

    public class ResourceRef (string format = "", string identifier = ""){
        public string Format { get; set; } = format;
        public string Identifier { get; set; } = identifier;
        public List<AssetType>? DataTypes { get; set; } = [];
        public string? Alg { get; set; } = null;
        public string? Hash { get; set; } = null;
    }

    public class AssetType (string assetType = "", string? version = null){
        [JsonPropertyName("asset_type")]
        public string Type { get; set; } = assetType;
        public string? Version { get; set; } = version;
    }

    public class ResourceStore {
        public Dictionary<string, string> Resources { get; set; } = [];
        public string? Label { get; set; } = null;
    }
}