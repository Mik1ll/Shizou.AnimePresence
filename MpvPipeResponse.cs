using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shizou.MpvDiscordPresence;

// ReSharper disable InconsistentNaming
public record MpvPipeResponse(string? error, JsonElement data, int? request_id, string? @event);

// ReSharper restore InconsistantNaming

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MpvPipeResponse))]
internal partial class ResponseContext : JsonSerializerContext;
