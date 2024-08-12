using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace C2pa
{
    public enum AssertionKind
    {
        Cbor,
        Json
    }

    public class AssertionTypeConverter : JsonConverter<Assertion>
    {
        public override Assertion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;
            string? label = root.GetProperty("label").GetString() ?? throw new JsonException();
            Type assertionType = GetAssertionTypeFromLabel(label);

            return JsonSerializer.Deserialize(root.GetRawText(), assertionType, options) as Assertion;
        }

        public override void Write(Utf8JsonWriter writer, Assertion value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }

        private static Type GetAssertionTypeFromLabel(string label)
        {
            return Utils.GetAssertionTypeFromLabel(label) ?? throw new JsonException();
        }
    }

    public class Assertion(string label, object data, AssertionKind kind = AssertionKind.Json)
    {
        public string Label { get; set; } = label;
        public object Data { get; set; } = data;
        public AssertionKind Kind { get; set; } = kind;

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, Utils.JsonOptions);
        }

        public static T? FromJson<T>(string json) where T : Assertion
        {
            return JsonSerializer.Deserialize<T>(json, Utils.JsonOptions);
        }
    }

    public class AssertionData
    {
    }

    public class ThumbnailAssertionData : AssertionData
    {
        public string Thumbnail { get; set; } = "";
        public string InstanceID { get; set; } = "";
    }

    public class ThumbnailAssertion(ThumbnailAssertionData data) : Assertion("c2pa.thumbnail", data)
    {
        new public ThumbnailAssertionData Data { get; set; } = data;
    }

    public class ClaimThumbnailAssertion(ThumbnailAssertionData data) : Assertion("c2pa.thumbnail.claim", data)
    {
        new public ThumbnailAssertionData Data { get; set; } = data;
    }

    public class IngredientThumbnailAssertion(ThumbnailAssertionData data) : Assertion("c2pa.thumbnail.ingredient", data)
    {
        new public ThumbnailAssertionData Data { get; set; } = data;
    }

    public class ActionAssertion(ActionAssertionData data) : Assertion("c2pa.action", data)
    {
        new public ActionAssertionData Data { get; set; } = data;
    }


    public class C2paAction(string action = "", string when = "", string softwareAgent = "", string changed = "", string instanceID = "", List<dynamic>? actors = null) : AssertionData
    {
        public string Action { get; set; } = action;
        public string When { get; set; } = when;
        public string SoftwareAgent { get; set; } = softwareAgent;
        public string Changed { get; set; } = changed;
        public string InstanceID { get; set; } = instanceID;
        public List<dynamic> Actors { get; set; } = actors ?? [];
    }

    public class ActionAssertionData : AssertionData
    {
        public List<C2paAction> Actions { get; set; } = new();
    }

    public class CustomAssertion(string label, dynamic data) : Assertion(label, (object)data)
    {
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
                    JsonValueKind.Array => property.Value.EnumerateArray().Select(x => ConvertElementToExpandoObject(x)).ToArray(),
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

    public class CreativeWorkAssertion(CreativeWorkAssertionData data) : Assertion("stds.schema-org.CreativeWork", data)
    {
        new public CreativeWorkAssertionData Data { get; set; } = data;
    }

    public class CreativeWorkAssertionData(string? context = "", string? type = "", AuthorInfo[]? authors = null) : AssertionData
    {
        [JsonPropertyName("@context")]
        public string? Context { get; set; } = context;

        [JsonPropertyName("@type")]
        public string? Type { get; set; } = type;

        public AuthorInfo[] Authors { get; set; } = authors ?? [];
    }

    public class AuthorInfo(string type, string name)
    {
        [JsonPropertyName("@type")]
        public string Type { get; set; } = type;

        public string Name { get; set; } = name;
    }
}