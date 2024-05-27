using System.Collections.Concurrent;
using System.IO.Pipes;
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
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<int, Channel<MpvPipeResponse>> _responses = new();
    private readonly DiscordRpcClient _discordClient;
    private bool _discordReady;

    public MpvPipeClient(string serverPath, string discordClientId)
    {
        _discordClient = new DiscordRpcClient(discordClientId);
        _pipeClientStream = new NamedPipeClientStream(".", serverPath, PipeDirection.InOut, PipeOptions.Asynchronous);
        _pipeClientStream.Connect(TimeSpan.FromMilliseconds(500));
        _lineReader = new StreamReader(_pipeClientStream);
        _lineWriter = new StreamWriter(_pipeClientStream) { AutoFlush = true };
    }

    private static string SmartStringTrim(string str, int length)
    {
        if (str.Length <= length)
            return str;
        return str[..str[..(length + 1)].LastIndexOf(' ')] + "...";
    }

    public async Task ReadLoop(CancellationToken cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            var line = await _lineReader.ReadLineAsync(cancelToken);

            if (cancelToken.IsCancellationRequested)
                break;
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
                    if (cancelToken.IsCancellationRequested)
                        break;
                    channel.Writer.Complete();
                }
            }
        }
    }

    public async Task QueryLoop(CancellationToken cancelToken)
    {
        _discordClient.OnReady += (_, _) => _discordReady = true;
        _discordClient.Initialize();
        await Task.Yield();
        for (; !cancelToken.IsCancellationRequested; await Task.Delay(TimeSpan.FromSeconds(1), cancelToken))
        {
            if (!_discordReady)
                continue;
            var path = await GetPropertyStringAsync("path", cancelToken);
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
            var animeId = fileQuery.Get("animeId") ?? throw new NullReferenceException("Anime ID cannot be null");
            var animeName = fileQuery.Get("animeName") ?? throw new NullReferenceException("Anime name cannot be null");
            var epNo = fileQuery.Get("epNo") ?? throw new NullReferenceException("Episode Number cannot be null");

            var timeLeft = (await GetPropertyAsync("playtime-remaining", cancelToken)).GetDouble();
            var paused = (await GetPropertyAsync("pause", cancelToken)).GetBoolean();

            var newPresence = new RichPresence
            {
                Details = SmartStringTrim(animeName, 64),
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
                },
                Buttons = [new Button { Url = $"https://anidb.net/anime/{animeId}", Label = "View Anime" }]
            };
            _discordClient.SetPresence(newPresence);
        }
    }

    public void Dispose()
    {
        _pipeClientStream.Dispose();
        _discordClient.Dispose();
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
