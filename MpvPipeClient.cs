using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Channels;
using System.Web;
using DiscordRPC;

namespace Shizou.MpvDiscordPresence;

public class MpvPipeClient : IDisposable
{
    private readonly NamedPipeClientStream _pipeClientStream;
    private readonly StreamReader _lineReader;
    private readonly StreamWriter _lineWriter;
    private readonly int _timeout = 500;
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<int, Channel<PipeResponse>> _responses = new();
    private readonly CancellationTokenSource _cancelSource;
    private readonly CancellationToken _cancelToken;
    private readonly DiscordRpcClient _discordClient;
    private int _nextRequestId;
    private bool _discordReady;

    public MpvPipeClient(string serverPath, string discordClientId, CancellationTokenSource cancelSource)
    {
        _cancelSource = cancelSource;
        _cancelToken = cancelSource.Token;
        _discordClient = new DiscordRpcClient(discordClientId);
        _pipeClientStream = new NamedPipeClientStream(".", serverPath, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous);
        _pipeClientStream.Connect(_timeout);
        _lineReader = new StreamReader(_pipeClientStream);
        _lineWriter = new StreamWriter(_pipeClientStream);
    }

    private static string SmartStringTrim(string str, int length)
    {
        if (str.Length <= length)
            return str;
        return str[..str[..(length + 1)].LastIndexOf(' ')];
    }

    public async Task ReadLoop()
    {
        while (!_cancelToken.IsCancellationRequested)
        {
            var line = await _lineReader.ReadLineAsync(_cancelToken);

            if (_cancelToken.IsCancellationRequested)
                break;
            if (string.IsNullOrEmpty(line))
                continue;
            var response = JsonSerializer.Deserialize(line, ResponseContext.Default.PipeResponse)!;
            if (response.@event is not null && response.@event == "shutdown")
            {
                await _cancelSource.CancelAsync();
                break;
            }

            if (response is { @event: null, request_id: not null })
            {
                _responses.TryGetValue(response.request_id.Value, out var channel);
                if (channel is not null)
                {
                    await channel.Writer.WriteAsync(response, _cancelToken);
                    if (_cancelToken.IsCancellationRequested)
                        break;
                    channel.Writer.Complete();
                }
            }
        }
    }

    public async Task QueryLoop()
    {
        _discordClient.OnReady += (_, _) => _discordReady = true;
        _discordClient.Initialize();
        await Task.Yield();
        for (; !_cancelToken.IsCancellationRequested; await Task.Delay(TimeSpan.FromSeconds(1), _cancelToken))
        {
            if (!_discordReady)
                continue;
            var path = await GetPropertyStringAsync("path");
            var uri = new Uri(path);
            var fileQuery = HttpUtility.ParseQueryString(uri.Query);
            var appId = fileQuery.Get("appId");
            if (!string.Equals(appId, "shizou", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("App id in query string did not match/exist, exiting");
                return;
            }

            var posterFilename = fileQuery.Get("posterFilename");
            var episodeName = fileQuery.Get("episodeName");

            var playlistPos = (await GetPropertyAsync("playlist-pos")).GetInt32();
            var timeLeft = (await GetPropertyAsync("playtime-remaining")).GetDouble();
            var paused = (await GetPropertyAsync("pause")).GetBoolean();
            var playlistTitle = await GetPropertyStringAsync($"playlist/{playlistPos}/title");
            var splitEnd = playlistTitle.LastIndexOf('-');
            var epNo = playlistTitle[(splitEnd + 1)..].Trim();
            var animeName = playlistTitle[..splitEnd].Trim();

            var newPresence = new RichPresence
            {
                Details = animeName,
                State = epNo[0] switch
                {
                    'S' => "Special " + epNo[1..],
                    'C' => "Credit " + epNo[1..],
                    'T' => "Trailer " + epNo[1..],
                    'P' => "Parody " + epNo[1..],
                    'O' => "Other " + epNo[1..],
                    _ => "Episode " + epNo
                },
                Timestamps = paused ? null : Timestamps.FromTimeSpan(timeLeft),
                Assets = new Assets
                {
                    LargeImageKey = string.IsNullOrWhiteSpace(posterFilename) ? "mpv" : $"https://cdn.anidb.net/images/main/{posterFilename}",
                    LargeImageText = string.IsNullOrWhiteSpace(episodeName) ? "mpv" : SmartStringTrim(episodeName, 64),
                    SmallImageKey = paused ? "pause" : "play",
                    SmallImageText = paused ? "Paused" : "Playing"
                }
            };
            _discordClient.SetPresence(newPresence);
        }
    }

    public void Dispose()
    {
        _pipeClientStream.Dispose();
        _discordClient.Dispose();
    }

    private PipeRequest NewRequest(params string[] command)
    {
        _nextRequestId = _random.Next();
        _responses[_nextRequestId] = Channel.CreateBounded<PipeResponse>(1);
        return new PipeRequest(command, _nextRequestId);
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

    private async Task<JsonElement> ExecuteQueryAsync(PipeRequest request)
    {
        await SendRequest();
        return await ReceiveResponse();

        async Task SendRequest()
        {
            var requestJson = JsonSerializer.Serialize(request, RequestContext.Default.PipeRequest);
            await _lineWriter.WriteLineAsync(requestJson.ToCharArray(), _cancelToken);
            _cancelToken.ThrowIfCancellationRequested();
            await _lineWriter.FlushAsync(_cancelToken);
            _cancelToken.ThrowIfCancellationRequested();
        }

        async Task<JsonElement> ReceiveResponse()
        {
            _responses.TryGetValue(request.request_id, out var channel);
            if (channel is null) throw new InvalidOperationException("Channel returned null");
            var response = await channel.Reader.ReadAsync(_cancelToken);
            _cancelToken.ThrowIfCancellationRequested();
            _responses.TryRemove(new KeyValuePair<int, Channel<PipeResponse>>(request.request_id, channel));
            if (response.error != "success")
                throw new InvalidOperationException(
                    $"Response for request: ({string.Join(',', request.command)}) returned an error {response.error} ({response.data})");
            return response.data!.Value;
        }
    }
}
