// This file contains C# forms of the Rust C2PA sub-components used in signing

using System;
using System.Text.Json.Serialization;
using C2pa;
using C2pa.Bindings;

namespace C2pa{

    // Ingredient
    public class HashedUri (string url, string alg, byte[] hash, byte[] salt){
        public string Url { get; set; } = url;
        public string Alg { get; set; } = alg;
        public byte[] Hash { get; set; } = hash;
        public byte[] Salt { get; set; } = salt;
    }

    public record ValidationStatus(string Code = "", string Url = "", string Explanation = "");

    public enum Relationship {
        parentOf,
        componentOf,
        inputTo,
    }

    // Manifest
    public record ClaimGeneratorInfoData(string Name = "", string Version = "");

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