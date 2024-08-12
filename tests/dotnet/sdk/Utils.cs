using C2pa.Bindings;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace C2pa
{
    public static class Utils
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters =
            {
                new AssertionTypeConverter(),
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)
            },
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        public static Type GetAssertionTypeFromLabel(string label)
        {
            return label switch
            {
                "base" => typeof(Assertion),
                "c2pa.action" => typeof(ActionAssertion),
                "c2pa.thumbnail" => typeof(ThumbnailAssertion),
                string s when Regex.IsMatch(s, @"c2pa\.thumbnail\.claim.*") => typeof(ClaimThumbnailAssertion),
                string s when Regex.IsMatch(s, @"c2pa\.thumbnail\.ingredient.*") => typeof(IngredientThumbnailAssertion),
                "stds.schema-org.CreativeWork" => typeof(CreativeWorkAssertion),
                _ => typeof(CustomAssertion),
            };
        }

        public unsafe static string FromCString(sbyte* ptr, bool ownsResource = false)
        {
            if (ptr == null)
            {
                return string.Empty;
            }
            var value = Marshal.PtrToStringUTF8(new nint(ptr))!;
            if (!ownsResource)
                c2pa.C2paReleaseString(ptr);

            return value;
        }

        public static bool FilePathValid(string path)
        {
            return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
        }
    }

}
