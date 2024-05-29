using System.Text.Json.Serialization;

namespace Shizou.MpvDiscordPresence;

// ReSharper disable InconsistentNaming
public record MpvPipeRequest(string[] command, int request_id);
// ReSharper restore InconsistantNaming

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MpvPipeRequest))]
internal partial class RequestContext : JsonSerializerContext;
