using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Shizou.AnimePresence;

public class MpvPipeClient : IDisposable
{
    private readonly NamedPipeClientStream _pipeClientStream;
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<int, Channel<MpvPipeResponse>> _responses = new();
    private readonly DiscordPipeClient _discordClient;
    private StreamReader? _lineReader;
    private StreamWriter? _lineWriter;

    public MpvPipeClient(string serverPath, DiscordPipeClient discordClient)
    {
        _discordClient = discordClient;
        _pipeClientStream = new NamedPipeClientStream(".", serverPath, PipeDirection.InOut, PipeOptions.Asynchronous);
    }

    public async Task Connect(CancellationToken cancelToken)
    {
        await _pipeClientStream.ConnectAsync(TimeSpan.FromMilliseconds(200), cancelToken);
        cancelToken.ThrowIfCancellationRequested();

        if (!_pipeClientStream.IsConnected)
            throw new InvalidOperationException("Failed to connect to mpv ipc");

        _lineReader = new StreamReader(_pipeClientStream);
        _lineWriter = new StreamWriter(_pipeClientStream) { AutoFlush = true };
    }

    public async Task ReadLoop(CancellationToken cancelToken)
    {
        if (_lineReader is null)
            throw new InvalidOperationException("Stream reader cannot be null");
        while (!cancelToken.IsCancellationRequested)
        {
            var line = await _lineReader.ReadLineAsync(cancelToken);
            cancelToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(line))
                continue;
            var response = JsonSerializer.Deserialize(line, ResponseContext.Default.MpvPipeResponse)!;
            if (response.@event is not null && response.@event == "shutdown")
                break;

            if (response is { @event: null, request_id: not null })
            {
                _responses.TryGetValue(response.request_id.Value, out var channel);
                if (channel is not null)
                {
                    await channel.Writer.WriteAsync(response, cancelToken);
                    cancelToken.ThrowIfCancellationRequested();
                    channel.Writer.Complete();
                }
            }
        }
    }

    public async Task QueryLoop(CancellationToken cancelToken)
    {
        await Task.Yield();
        for (; !cancelToken.IsCancellationRequested; await Task.Delay(TimeSpan.FromSeconds(1), cancelToken))
        {
            var path = await GetPropertyStringAsync("path", cancelToken);
            var queryInfo = QueryInfo.GetQueryInfo(path);
            if (queryInfo is null)
                return;

            var speed = (await GetPropertyAsync("speed", cancelToken)).GetDouble();
            var timeLeft = (await GetPropertyAsync("playtime-remaining", cancelToken)).GetDouble();
            var playbackTime = (await GetPropertyAsync("playback-time", cancelToken)).GetDouble() / speed;
            var paused = (await GetPropertyAsync("pause", cancelToken)).GetBoolean();

            var newPresence = _discordClient.CreateNewPresence(queryInfo, paused, playbackTime, timeLeft);
            await _discordClient.SetPresenceAsync(newPresence, cancelToken);
        }
    }

    public void Dispose()
    {
        _pipeClientStream.Dispose();
    }

    private MpvPipeRequest NewRequest(params string[] command)
    {
        var requestId = _random.Next();
        _responses[requestId] = Channel.CreateBounded<MpvPipeResponse>(1);
        return new MpvPipeRequest(command, requestId);
    }

    private async Task<JsonElement> GetPropertyAsync(string key, CancellationToken cancelToken)
    {
        var request = NewRequest("get_property", key);
        var response = await ExecuteQueryAsync(request, cancelToken);
        return response;
    }

    private async Task<string> GetPropertyStringAsync(string key, CancellationToken cancelToken)
    {
        var request = NewRequest("get_property_string", key);
        var response = await ExecuteQueryAsync(request, cancelToken);
        return response.GetString() ?? "";
    }

    private async Task<JsonElement> ExecuteQueryAsync(MpvPipeRequest request, CancellationToken cancelToken)
    {
        if (_lineWriter is null)
            throw new InvalidOperationException("Stream writer cannot be null");
        await SendRequest();
        return await ReceiveResponse();

        async Task SendRequest()
        {
            var requestJson = JsonSerializer.Serialize(request, RequestContext.Default.MpvPipeRequest);
            await _lineWriter.WriteLineAsync(requestJson.ToCharArray(), cancelToken);
            cancelToken.ThrowIfCancellationRequested();
        }

        async Task<JsonElement> ReceiveResponse()
        {
            if (!_responses.TryGetValue(request.request_id, out var channel))
                throw new InvalidOperationException("Response channel not found");
            var response = await channel.Reader.ReadAsync(cancelToken);
            cancelToken.ThrowIfCancellationRequested();
            _responses.TryRemove(request.request_id, out _);
            if (response.error != "success")
                throw new InvalidOperationException(
                    $"Response for request: ({string.Join(',', request.command)}) returned an error {response.error} ({response.data})");
            return response.data;
        }
    }
}

// ReSharper disable InconsistentNaming
public record MpvPipeRequest(string[] command, int request_id);

// ReSharper restore InconsistantNaming

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MpvPipeRequest))]
internal partial class RequestContext : JsonSerializerContext;

// ReSharper disable InconsistentNaming
public record MpvPipeResponse(string? error, JsonElement data, int? request_id, string? @event);

// ReSharper restore InconsistantNaming

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MpvPipeResponse))]
internal partial class ResponseContext : JsonSerializerContext;
