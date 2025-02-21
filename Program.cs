using System.Text.Json;
using System.Text.Json.Serialization;
using Shizou.AnimePresence;

await using var settingsStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Shizou.AnimePresence.jsonc"));
var settings = await JsonSerializer.DeserializeAsync(settingsStream, MyContext.Default.Settings) ??
               throw new JsonException("Couldn't deserialize settings");

if (args.Length != 2)
    throw new InvalidOperationException("Requires two arguments, the player and port/socket name");

try
{
    var cancelSource = new CancellationTokenSource();
    using var discordClient = new DiscordPipeClient(settings.DiscordClientId, settings.AllowRestricted);
    Task[] tasks;
    switch (args[0])
    {
        case "mpv":
        {
            using var mpvClient = new MpvPipeClient(args[1], discordClient);
            await mpvClient.Connect(cancelSource.Token);
            tasks = [discordClient.ReadLoop(cancelSource.Token), mpvClient.ReadLoop(cancelSource.Token), mpvClient.QueryLoop(cancelSource.Token)];
            await Run(tasks, cancelSource);
            break;
        }
        case "vlc":
        {
            using var vlcClient = new VlcHttpClient(Convert.ToInt32(args[1]), discordClient);
            tasks = [discordClient.ReadLoop(cancelSource.Token), vlcClient.QueryLoop(cancelSource.Token)];
            await Run(tasks, cancelSource);
            break;
        }
        default:
            throw new InvalidOperationException(args[0] + " is not an accepted player name");
    }
}
catch (AggregateException ae)
{
    ae.Handle(ex => ex is OperationCanceledException or IOException);
}
catch (Exception e) when (e is OperationCanceledException or IOException)
{
}

return;

async Task Run(Task[] tasks, CancellationTokenSource cancellationTokenSource)
{
    await Task.WhenAny(tasks);
    await cancellationTokenSource.CancelAsync();
    await Task.WhenAll(tasks);
}

public static partial class Program
{
    public const string AppId = "07a58b50-5109-5aa3-abbc-782fed0df04f";
}

public record Settings(string DiscordClientId, bool AllowRestricted);

[JsonSerializable(typeof(Settings))]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class MyContext : JsonSerializerContext;
