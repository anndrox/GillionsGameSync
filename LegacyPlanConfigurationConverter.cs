using System;
using Newtonsoft.Json;

namespace GillionsGameSync;

// A property converter runs after its first value token has been read. Intercept
// only the two legacy properties before that read so a scalar date stays a string.
// Populate retains the normal contracts/converters without recursively invoking
// this root converter; writing still uses Dalamud's ordinary serializer.
public sealed class LegacyPlanConfigurationConverter : JsonConverter<PluginConfiguration> {
    public override bool CanWrite => false;

    public override PluginConfiguration? ReadJson(JsonReader reader, Type objectType,
        PluginConfiguration? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null) return null;
        if (reader.TokenType != JsonToken.StartObject)
            throw new JsonSerializationException("Plugin configuration must be an object.");
        var configuration = existingValue ?? new PluginConfiguration();
        using var scopedReader = new LegacyPlanPropertyReader(reader);
        serializer.Populate(scopedReader, configuration);
        return configuration;
    }

    public override void WriteJson(JsonWriter writer, PluginConfiguration? value, JsonSerializer serializer) =>
        throw new NotSupportedException("Configuration writes use the ordinary serializer.");
}

internal sealed class LegacyPlanPropertyReader : JsonReader, IJsonLineInfo {
    private readonly JsonReader inner;
    private readonly int objectDepth;

    public LegacyPlanPropertyReader(JsonReader inner) {
        this.inner = inner;
        objectDepth = inner.Depth;
        CloseInput = false;
        Culture = inner.Culture;
        DateParseHandling = inner.DateParseHandling;
        DateTimeZoneHandling = inner.DateTimeZoneHandling;
        FloatParseHandling = inner.FloatParseHandling;
        DateFormatString = inner.DateFormatString;
        MaxDepth = inner.MaxDepth;
        SetToken(inner.TokenType, inner.Value);
    }

    private bool IsLegacyValue => inner.TokenType == JsonToken.PropertyName
        && inner.Depth == objectDepth + 1
        // Match Json.NET's normal property-name casing rules, not nested keys.
        && (string.Equals(inner.Value as string, nameof(PluginConfiguration.AutoRetainerVenturePlanBackups), StringComparison.OrdinalIgnoreCase)
            || string.Equals(inner.Value as string, nameof(PluginConfiguration.AutoRetainerPlanOwnershipStates), StringComparison.OrdinalIgnoreCase));

    private T ReadWithPolicy<T>(Func<T> read) {
        var originalDateHandling = inner.DateParseHandling;
        try {
            inner.DateParseHandling = IsLegacyValue ? DateParseHandling.None : DateParseHandling;
            var result = read();
            SetToken(inner.TokenType, inner.Value);
            return result;
        } finally {
            inner.DateParseHandling = originalDateHandling;
        }
    }

    public override bool Read() => ReadWithPolicy(inner.Read);
    public override string? ReadAsString() => ReadWithPolicy(inner.ReadAsString);
    public override int? ReadAsInt32() => ReadWithPolicy(inner.ReadAsInt32);
    public override byte[]? ReadAsBytes() => ReadWithPolicy(inner.ReadAsBytes);
    public override decimal? ReadAsDecimal() => ReadWithPolicy(inner.ReadAsDecimal);
    public override double? ReadAsDouble() => ReadWithPolicy(inner.ReadAsDouble);
    public override bool? ReadAsBoolean() => ReadWithPolicy(inner.ReadAsBoolean);
    public override DateTime? ReadAsDateTime() => ReadWithPolicy(inner.ReadAsDateTime);
    public override DateTimeOffset? ReadAsDateTimeOffset() => ReadWithPolicy(inner.ReadAsDateTimeOffset);
    public bool HasLineInfo() => inner is IJsonLineInfo info && info.HasLineInfo();
    public int LineNumber => (inner as IJsonLineInfo)?.LineNumber ?? 0;
    public int LinePosition => (inner as IJsonLineInfo)?.LinePosition ?? 0;
}
