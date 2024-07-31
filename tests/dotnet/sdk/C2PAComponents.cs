// This file contains C# forms of the Rust C2PA sub-components used in signing

using System;
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

    public class Thumbnail (string format = "", string identifier = ""){
        public string Format { get; set; } = format;
        public string Identifier { get; set; } = identifier;
    }

    public record ValidationStatus(string Code = "", string Url = "", string Explanation = "");

    public enum Relationship {
        ParentOf,
        ComponentOf,
        InputTo,
    }

    // Manifest
    public record ClaimGeneratorInfoData(string Name = "", string Version = "");
}