using System.Text.Json.Serialization;

namespace Shizou.MpvDiscordPresence;

// ReSharper disable InconsistentNaming
public record PipeRequest(string[] command, int request_id);
// ReSharper restore InconsistantNaming

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(PipeRequest), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class RequestContext : JsonSerializerContext;
