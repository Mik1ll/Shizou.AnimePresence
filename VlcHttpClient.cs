using System.Net.Http.Headers;
using System.Text.Json;

namespace Shizou.AnimePresence;

public class VlcHttpClient : IDisposable
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
        await Task.Yield();
        for (; !cancelToken.IsCancellationRequested; await Task.Delay(TimeSpan.FromSeconds(3), cancelToken))
        {
            using var statusResp = await _httpClient.GetAsync("status.json", cancelToken);
            statusResp.EnsureSuccessStatusCode();
            var statusJson = await statusResp.Content.ReadAsStringAsync(cancelToken);
            var json = JsonDocument.Parse(statusJson);
            var uri = json.RootElement.GetProperty("uri").GetString();
            var queryInfo = QueryInfo.GetQueryInfo(uri);
            if (queryInfo is null)
                return;
            var speed = json.RootElement.GetProperty("speed").GetDouble();
            var duration = json.RootElement.GetProperty("duration").GetDouble();
            var playbackTime = json.RootElement.GetProperty("time").GetDouble() / speed;
            var timeLeft = (duration - playbackTime) / speed;
            var paused = json.RootElement.GetProperty("paused").GetBoolean();

            var newPresence = _discordClient.CreateNewPresence(queryInfo, paused, playbackTime, timeLeft);
            await _discordClient.SetPresenceAsync(newPresence, cancelToken);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
