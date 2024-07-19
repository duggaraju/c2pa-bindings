using System;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace C2pa{

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
            switch (label)
            {
                case "base":
                    return typeof(BaseAssertion);
                case "c2pa.action":
                    return typeof(ActionAssertion);
                case "stds.schema-org.CreativeWork":
                    return typeof(CreativeWorkAssertion);
                default:
                    return typeof(CustomAssertion);
            }
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

    public class ActionAssertion (ActionAssertionData data, string kind = "Json") : BaseAssertion("c2pa.action", data, kind) {
        new public ActionAssertionData Data { get; set; } = data;
    }

    public class ActionAssertionData : BaseAssertionData {
        public string Action { get; set; } = "";
        public string When { get; set; } = "";
        public string SoftwareAgent { get; set; } = "";
        public string Changed { get; set; } = "";
        public string InstanceID { get; set; } = "";
        public List<dynamic> Actors { get; set; } = new List<dynamic>();
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
                ((IDictionary<string, object>)dataResult)[property.Name] = property.Value.ValueKind switch
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

    public class CreativeWorkAssertionData (string ? context, string ? type, AuthorInfo[] author) : BaseAssertionData
    {

        [JsonPropertyName("@context")]
        public string? Context { get; init; } = context;

        [JsonPropertyName("@type")]
        public string? Type { get; init; } = type;

        public AuthorInfo[] Author { get; init; } = author;
    }

}