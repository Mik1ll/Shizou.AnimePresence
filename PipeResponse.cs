﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shizou.MpvDiscordPresence;

// ReSharper disable InconsistentNaming
public record PipeResponse(string? error, JsonElement? data, int? request_id, string? @event);

// ReSharper restore InconsistantNaming

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(PipeResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class ResponseContext : JsonSerializerContext;
