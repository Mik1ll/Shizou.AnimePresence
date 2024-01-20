using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;

namespace Shizou.MpvDiscordPresence;

public class MpvPipeClient : IDisposable, IAsyncDisposable
{
    private int _nextRequestId;

    // ReSharper disable InconsistentNaming
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record Request(string[] command, int request_id);

    // ReSharper disable once ClassNeverInstantiated.Local
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record Response(string? error, JsonElement? data, int? request_id, string? @event);
    // ReSharper restore InconsistantNaming

    private readonly NamedPipeClientStream _pipeClientStream;
    private readonly StreamReader _lineReader;
    private readonly StreamWriter _lineWriter;
    private readonly int _timeout = 500;
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<int, Channel<Response>> _responses = new();

    public MpvPipeClient(string serverPath)
    {
        _pipeClientStream = new NamedPipeClientStream(".", serverPath, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous);
        _pipeClientStream.Connect(_timeout);
        _lineReader = new StreamReader(_pipeClientStream);
        _lineWriter = new StreamWriter(_pipeClientStream);
    }

    private Request NewRequest(params string[] command)
    {
        _nextRequestId = _random.Next();
        _responses[_nextRequestId] = Channel.CreateBounded<Response>(1);
        return new Request(command, _nextRequestId);
    }

    public async Task<JsonElement> GetPropertyAsync(string key)
    {
        var request = NewRequest("get_property", key);
        var response = await ExecuteQueryAsync(request);
        return response;
    }

    public async Task<string> GetPropertyStringAsync(string key)
    {
        var request = NewRequest("get_property_string", key);
        var response = await ExecuteQueryAsync(request);
        return response.GetString() ?? "";
    }

    public async Task ReadLoop()
    {
        while (true)
        {
            var line = await _lineReader.ReadLineAsync() ?? throw new JsonException("Json response empty");
            var response = JsonSerializer.Deserialize<Response>(line) ?? throw new JsonException("Json response empty");
            if (response.@event is not null && response.@event == "shutdown")
                throw new OperationCanceledException();

            if (response is { @event: null, request_id: not null })
            {
                _responses.TryGetValue(response.request_id.Value, out var channel);
                if (channel is not null)
                {
                    await channel.Writer.WriteAsync(response);
                    channel.Writer.Complete();
                }
            }
        }
    }

    private async Task<JsonElement> ExecuteQueryAsync(Request request)
    {
        await SendRequest();
        return await ReceiveResponse();

        async Task SendRequest()
        {
            var requestJson = JsonSerializer.Serialize(request);
            await _lineWriter.WriteLineAsync(requestJson);
            await _lineWriter.FlushAsync();
        }

        async Task<JsonElement> ReceiveResponse()
        {
            _responses.TryGetValue(request.request_id, out var channel);
            if (channel is null) throw new InvalidOperationException("Channel returned null");
            var response = await channel.Reader.ReadAsync();
            _responses.TryRemove(new KeyValuePair<int, Channel<Response>>(request.request_id, channel));
            if (response.error != "success")
                throw new InvalidOperationException(
                    $"Response for request: ({string.Join(',', request.command)}) returned an error {response.error} ({response.data})");
            return response.data!.Value;
        }
    }

    public void Dispose()
    {
        Console.WriteLine("Cleanup");
        _pipeClientStream.Dispose();
        _lineReader.Dispose();
        _lineWriter.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("Cleanup Async");
        await _pipeClientStream.DisposeAsync();
        await _lineWriter.DisposeAsync();
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (_lineReader is IAsyncDisposable lineReaderAsyncDisposable)
            await lineReaderAsyncDisposable.DisposeAsync();
        else
            _lineReader.Dispose();
    }
}
