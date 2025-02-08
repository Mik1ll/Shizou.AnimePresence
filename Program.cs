using System.CommandLine;
using Shizou.AnimePresence;
using Command = System.CommandLine.Command;

var rootCommand = new RootCommand()
{
    TreatUnmatchedTokensAsErrors = true,
};

var discordClientIdArg = new Argument<string>("discord-client-id", "Discord API client ID");

var allowRestrictedArg = new Argument<bool>("allow-restricted", "Whether to show presence for restricted series (hentai), will never show images");

var socketNameArg = new Argument<string>("socket-name", "The name of the ipc socket");


rootCommand.AddArgument(discordClientIdArg);
rootCommand.AddArgument(allowRestrictedArg);

var mpvCommand = new Command("mpv", "Run with mpv external player");
mpvCommand.AddArgument(socketNameArg);
rootCommand.AddCommand(mpvCommand);
mpvCommand.SetHandler(HandleMpv, discordClientIdArg, allowRestrictedArg, socketNameArg);

var vlcCommand = new Command("vlc", "Run with vlc external player");

var portArg = new Argument<int>("port", "The port number used for the http server");
vlcCommand.AddArgument(portArg);
rootCommand.AddCommand(vlcCommand);
vlcCommand.SetHandler(HandleVlc, discordClientIdArg, allowRestrictedArg, portArg);

await rootCommand.InvokeAsync(args);

return;

async Task HandleMpv(string discordClientId, bool allowRestricted, string socketName)
{
    var cancelSource = new CancellationTokenSource();
    using var discordClient = new DiscordPipeClient(discordClientId, allowRestricted);
    using var mpvClient = new MpvPipeClient(socketName, discordClient);
    await mpvClient.Connect(cancelSource.Token);
    var tasks = new[] { mpvClient.ReadLoop(cancelSource.Token), mpvClient.QueryLoop(cancelSource.Token), discordClient.ReadLoop(cancelSource.Token) };
    await Run(tasks, cancelSource);
}


async Task HandleVlc(string discordClientId, bool allowRestricted, int port)
{
    var cancelSource = new CancellationTokenSource();
    using var discordClient = new DiscordPipeClient(discordClientId, allowRestricted);
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
