using Shizou.AnimePresence;


if (args.Length != 3)
    throw new InvalidOperationException("Must provide three arguments: discord client id, ipc socket/pipe name, and allow restricted (bool)");

var discordClientId = args[0];
var socketName = args[1];
var allowRestricted = bool.Parse(args[2]);

try
{
    var cancelSource = new CancellationTokenSource();
    using var discordClient = new DiscordPipeClient(discordClientId, allowRestricted);
    using var mpvClient = new MpvPipeClient(socketName, discordClient);
    await mpvClient.Connect(cancelSource.Token);
    var tasks = new[] { mpvClient.ReadLoop(cancelSource.Token), mpvClient.QueryLoop(cancelSource.Token), discordClient.ReadLoop(cancelSource.Token) };

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

public static partial class Program
{
    public const string AppId = "07a58b50-5109-5aa3-abbc-782fed0df04f";
}
