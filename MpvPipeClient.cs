using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;
using Discord;

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
    private readonly CancellationTokenSource _cancelSource;

    public MpvPipeClient(string serverPath, CancellationTokenSource cancelSource)
    {
        _cancelSource = cancelSource;
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

    private async Task<JsonElement> GetPropertyAsync(string key)
    {
        var request = NewRequest("get_property", key);
        var response = await ExecuteQueryAsync(request);
        return response;
    }

    private async Task<string> GetPropertyStringAsync(string key)
    {
        var request = NewRequest("get_property_string", key);
        var response = await ExecuteQueryAsync(request);
        return response.GetString() ?? "";
    }

    public async Task ReadLoop()
    {
        while (!_cancelSource.Token.IsCancellationRequested)
        {
            var line = await _lineReader.ReadLineAsync(_cancelSource.Token);

            if (_cancelSource.Token.IsCancellationRequested)
                break;
            if (string.IsNullOrEmpty(line))
                continue;
            var response = JsonSerializer.Deserialize<Response>(line)!;
            if (response.@event is not null && response.@event == "shutdown")
            {
                _cancelSource.Cancel();
                break;
            }

            if (response is { @event: null, request_id: not null })
            {
                _responses.TryGetValue(response.request_id.Value, out var channel);
                if (channel is not null)
                {
                    await channel.Writer.WriteAsync(response, _cancelSource.Token);
                    if (_cancelSource.Token.IsCancellationRequested)
                        break;
                    channel.Writer.Complete();
                }
            }
        }
    }

    public async Task QueryLoop()
    {
        using var discord = new Discord.Discord(737663962677510245, (ulong)CreateFlags.NoRequireDiscord);
        var activity = new Activity();
        for (; !_cancelSource.Token.IsCancellationRequested; await Task.Delay(TimeSpan.FromMilliseconds(200), _cancelSource.Token))
        {
            var playlistPos = (await GetPropertyAsync("playlist-pos")).GetInt32();
            var duration = (await GetPropertyAsync("duration")).GetDouble();
            var paused = (await GetPropertyAsync("pause")).GetBoolean();
            var playlistTitle = await GetPropertyStringAsync($"playlist/{playlistPos}/title");
            var splitIdx = playlistTitle.LastIndexOf('-');
            var title = playlistTitle[..splitIdx].Trim();
            var epNo = playlistTitle[splitIdx..].Trim();

            var newActivity = new Activity
            {
                Details = title,
                State = epNo[0] switch
                {
                    'S' => "Special " + epNo[1..],
                    'C' => "Credit " + epNo[1..],
                    'T' => "Trailer " + epNo[1..],
                    'P' => "Parody " + epNo[1..],
                    'O' => "Other " + epNo[1..],
                    _ => "Episode " + epNo
                },
                Timestamps = new ActivityTimestamps
                {
                    End = paused ? default : (DateTimeOffset.UtcNow + TimeSpan.FromSeconds(duration)).ToUnixTimeSeconds()
                },
                Assets = new ActivityAssets
                {
                    LargeImage = "mpv",
                    LargeText = "mpv",
                    SmallImage = paused ? "pause" : "play",
                    SmallText = paused ? "Paused" : "Playing"
                }
            };

            if (!ActivityEqual(newActivity, activity))
                discord.GetActivityManager().UpdateActivity(newActivity, _ => { });
            activity = newActivity;
            discord.RunCallbacks();
        }

        bool ActivityEqual(Activity a, Activity b) => a.Assets.SmallText == b.Assets.SmallText;
    }

    private async Task<JsonElement> ExecuteQueryAsync(Request request)
    {
        await SendRequest();
        return await ReceiveResponse();

        async Task SendRequest()
        {
            var requestJson = JsonSerializer.Serialize(request);
            await _lineWriter.WriteLineAsync(requestJson.ToCharArray(), _cancelSource.Token);
            _cancelSource.Token.ThrowIfCancellationRequested();
            await _lineWriter.FlushAsync();
        }

        async Task<JsonElement> ReceiveResponse()
        {
            _responses.TryGetValue(request.request_id, out var channel);
            if (channel is null) throw new InvalidOperationException("Channel returned null");
            var response = await channel.Reader.ReadAsync(_cancelSource.Token);
            _cancelSource.Token.ThrowIfCancellationRequested();
            _responses.TryRemove(new KeyValuePair<int, Channel<Response>>(request.request_id, channel));
            if (response.error != "success")
                throw new InvalidOperationException(
                    $"Response for request: ({string.Join(',', request.command)}) returned an error {response.error} ({response.data})");
            return response.data!.Value;
        }
    }

    public void Dispose()
    {
        _pipeClientStream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _pipeClientStream.DisposeAsync();
    }
}
