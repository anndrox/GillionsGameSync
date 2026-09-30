using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GillionsGameSync;

// Only legacy local backup subtrees use this converter. Values are never
// interpreted as types, uploaded, exported or used to control another plugin.
public sealed class LegacyPlanDataConverter : JsonConverter<JToken> {
    public override JToken? ReadJson(JsonReader reader, Type objectType, JToken? existingValue,
        bool hasExistingValue, JsonSerializer serializer) {
        var dateHandling = reader.DateParseHandling;
        try {
            reader.DateParseHandling = DateParseHandling.None;
            return JToken.Load(reader);
        } finally {
            reader.DateParseHandling = dateHandling;
        }
    }

    public override void WriteJson(JsonWriter writer, JToken? value, JsonSerializer serializer) {
        if (value is null) writer.WriteNull();
        else value.WriteTo(writer);
    }
}
