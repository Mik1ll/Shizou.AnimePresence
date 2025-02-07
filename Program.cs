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

var mpvCommand = new Command("mpv", "Run with mpv external player");

rootCommand.AddArgument(discordClientIdArg);
rootCommand.AddArgument(allowRestrictedArg);
mpvCommand.AddArgument(socketNameArg);
rootCommand.AddCommand(mpvCommand);
mpvCommand.SetHandler(HandleMpv, discordClientIdArg, allowRestrictedArg, socketNameArg);

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
