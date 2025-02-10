using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shizou.AnimePresence;
using Command = System.CommandLine.Command;

await using var settingsStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Shizou.AnimePresence.jsonc"));
var settings = await JsonSerializer.DeserializeAsync(settingsStream, MyContext.Default.Settings) ??
               throw new JsonException("Couldn't deserialize settings");

var socketNameArg = new Argument<string>("socket-name", "The name of the ipc socket");

var portArg = new Argument<int>("port", "The port number used for the http server");

var rootCommand = new RootCommand { TreatUnmatchedTokensAsErrors = true };

var mpvCommand = new Command("mpv", "Run with mpv external player");
mpvCommand.AddArgument(socketNameArg);
rootCommand.AddCommand(mpvCommand);

var vlcCommand = new Command("vlc", "Run with vlc external player");
vlcCommand.AddArgument(portArg);
rootCommand.AddCommand(vlcCommand);

mpvCommand.SetHandler(HandleMpv, socketNameArg);

vlcCommand.SetHandler(HandleVlc, portArg);

await rootCommand.InvokeAsync(args);

return;

async Task HandleMpv(string socketName)
{
    var cancelSource = new CancellationTokenSource();
    using var discordClient = new DiscordPipeClient(settings.DiscordClientId, settings.AllowRestricted);
    using var mpvClient = new MpvPipeClient(socketName, discordClient);
    await mpvClient.Connect(cancelSource.Token);
    var tasks = new[] { mpvClient.ReadLoop(cancelSource.Token), mpvClient.QueryLoop(cancelSource.Token), discordClient.ReadLoop(cancelSource.Token) };
    await Run(tasks, cancelSource);
}


async Task HandleVlc(int port)
{
    var cancelSource = new CancellationTokenSource();
    using var discordClient = new DiscordPipeClient(settings.DiscordClientId, settings.AllowRestricted);
    using var vlcCLient = new VlcHttpClient(port, discordClient);
    var tasks = new[] { vlcCLient.QueryLoop(cancelSource.Token), discordClient.ReadLoop(cancelSource.Token) };
    await Run(tasks, cancelSource);
}

async Task Run(Task[] tasks, CancellationTokenSource cancelSource)
{
    try
    {
        await Task.WhenAny(tasks);
        await cancelSource.CancelAsync();
        await Task.WhenAll(tasks);
    }
    catch (AggregateException ae)
    {
        ae.Handle(ex => ex is OperationCanceledException or IOException);
    }
    catch (Exception e) when (e is OperationCanceledException or IOException)
    {
    }
}

public static partial class Program
{
    public const string AppId = "07a58b50-5109-5aa3-abbc-782fed0df04f";
}

public record Settings(string DiscordClientId, bool AllowRestricted);

[JsonSerializable(typeof(Settings))]
[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip)]
public partial class MyContext : JsonSerializerContext
{
}
