using System.Net.Http.Headers;
using System.Text.Json;

namespace Shizou.AnimePresence;

public sealed class VlcHttpClient : IDisposable
{
    private readonly DiscordPipeClient _discordClient;
    private readonly HttpClient _httpClient;

    public VlcHttpClient(int port, DiscordPipeClient discordClient)
    {
        _discordClient = discordClient;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.234.133.79:{port}"),
            Timeout = TimeSpan.FromSeconds(2),
            DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(":password"u8)) },
            DefaultRequestVersion = new Version(1, 0),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
    }

    public async Task QueryLoop(CancellationToken cancelToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancelToken);
        for (; !cancelToken.IsCancellationRequested; await Task.Delay(TimeSpan.FromSeconds(1), cancelToken))
        {
            using var statusResp = await _httpClient.GetAsync("status.json", cancelToken);
            statusResp.EnsureSuccessStatusCode();
            var statusJson = await statusResp.Content.ReadAsStringAsync(cancelToken);
            var json = JsonDocument.Parse(statusJson);
            var uri = json.RootElement.TryGetProperty("uri", out var uriElem) ? uriElem.GetString() : null;
            var queryInfo = QueryInfo.GetQueryInfo(uri);
            if (queryInfo is null)
            {
                await _discordClient.SetPresenceAsync(null, cancelToken);
                continue;
            }

            var speed = json.RootElement.TryGetProperty("speed", out var speedElem) ? speedElem.GetDouble() : 1;
            var duration = json.RootElement.TryGetProperty("duration", out var durationElem) ? durationElem.GetDouble() : (double?)null;
            var playbackTime = json.RootElement.GetProperty("time").GetDouble();
            var timeStamps = TimeStamps.FromDurationTimeSpeed(playbackTime, speed, duration);
            var paused = json.RootElement.GetProperty("paused").GetBoolean();

            var newPresence = _discordClient.CreateNewPresence(queryInfo, paused, timeStamps);
            await _discordClient.SetPresenceAsync(newPresence, cancelToken);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
