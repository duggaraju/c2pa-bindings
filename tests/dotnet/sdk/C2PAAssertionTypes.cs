using System;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace C2pa{

    public static partial class Utils{
        public static object GetAssertionTypeFromLabel(string label){
            return label switch
            {
                "base" => typeof(BaseAssertion),
                "c2pa.action" => typeof(ActionAssertion),
                "c2pa.thumbnail" => typeof(ThumbnailAssertion),
                string s when Regex.IsMatch(s, @"c2pa\.thumbnail\.claim.*") => typeof(ClaimThumbnailAssertion),
                string s when Regex.IsMatch(s, @"c2pa\.thumbnail\.ingredient.*") => typeof(IngredientThumbnailAssertion),
                "stds.schema-org.CreativeWork" => typeof(CreativeWorkAssertion),
                _ => typeof(CustomAssertion),
            };
        }
    }

    public class AssertionTypeConverter : JsonConverter<BaseAssertion>
    {
        public override BaseAssertion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;
            string? label = root.GetProperty("label").GetString() ?? throw new JsonException();
            Type assertionType = GetAssertionTypeFromLabel(label);

            return JsonSerializer.Deserialize(root.GetRawText(), assertionType, options) as BaseAssertion;
        }

        public override void Write(Utf8JsonWriter writer, BaseAssertion value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }

        private static Type GetAssertionTypeFromLabel(string label){
            return Utils.GetAssertionTypeFromLabel(label) as Type ?? throw new JsonException();
        }
    }

    public class BaseAssertion(string label, BaseAssertionData? data, string kind = "Json")
    {
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Converters = { new AssertionTypeConverter() },
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public string Label { get; set; } = label;
        public BaseAssertionData Data { get; set; } = data ?? new ();
        public string Kind { get; set; } = kind;
    }

    public class BaseAssertionData {

    }

    public class ThumbnailAssertionData : BaseAssertionData {
        public string Thumbnail { get; set; } = "";
        public string InstanceID { get; set; } = "";
    }

    public class ThumbnailAssertion (ThumbnailAssertionData data, string kind = "Json") : BaseAssertion("c2pa.thumbnail", data, kind) {
        new public ThumbnailAssertionData Data { get; set; } = data;
    }

    public class ClaimThumbnailAssertion(ThumbnailAssertionData data, string kind = "Json") : BaseAssertion("c2pa.thumbnail.claim", data, kind) {
        new public ThumbnailAssertionData Data { get; set; } = data;
    }

    public class IngredientThumbnailAssertion(ThumbnailAssertionData data, string kind = "Json") : BaseAssertion("c2pa.thumbnail.ingredient", data, kind) {
        new public ThumbnailAssertionData Data { get; set; } = data;
    }

    public class ActionAssertion (ActionAssertionData data, string kind = "Json") : BaseAssertion("c2pa.action", data, kind) {
        new public ActionAssertionData Data { get; set; } = data;
    }

    public class ActionAssertionData (string action = "", string when = "", string softwareAgent = "", string changed = "", string instanceID = "", List<dynamic>? actors = null) : BaseAssertionData {
        public string Action { get; set; } = action;
        public string When { get; set; } = when;
        public string SoftwareAgent { get; set; } = softwareAgent;
        public string Changed { get; set; } = changed;
        public string InstanceID { get; set; } = instanceID;
        public List<dynamic> Actors { get; set; } = actors ?? [];
    }

    public class CustomAssertion (string label, dynamic data, string kind = "Json") : BaseAssertion(label, null, kind) {
        new public dynamic Data { get; set; } = data;

        public ExpandoObject GetDataAsExpandoObject()
        {
            return ConvertElementToExpandoObject(Data);
        }

        private ExpandoObject ConvertElementToExpandoObject(JsonElement element)
        {
            dynamic dataResult = new ExpandoObject();

            foreach (JsonProperty property in element.EnumerateObject())
            {
                string propertyName = property.Name.Replace("@", "");
                ((IDictionary<string, object>)dataResult)[propertyName] = property.Value.ValueKind switch
                {
                    JsonValueKind.Array => property.Value.EnumerateArray().Select(x => ConvertElementToExpandoObject(x)).ToList(),
                    JsonValueKind.Object => ConvertElementToExpandoObject(property.Value),
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => property.Value.ToString(),
                };
            }

            return dataResult;
        }
    }

    public class CreativeWorkAssertion(CreativeWorkAssertionData data, string kind = "Json") : BaseAssertion("stds.schema-org.CreativeWork", data, kind)  {
        new public CreativeWorkAssertionData Data { get; set; } = data;
    }

    public class CreativeWorkAssertionData (string? context = "", string? type = "", AuthorInfo[]? authors = null) : BaseAssertionData
    {

        [JsonPropertyName("@context")]
        public string? Context { get; set; } = context;

        [JsonPropertyName("@type")]
        public string? Type { get; set; } = type;

        public AuthorInfo[] Authors { get; set; } = authors ?? [];
    }
}