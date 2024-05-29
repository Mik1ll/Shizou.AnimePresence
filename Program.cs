using Shizou.MpvDiscordPresence;

if (args.Length != 2)
    throw new InvalidOperationException("Must provide two arguments: discord client id and ipc socket/pipe name");

var discordClientId = args[0];
var socketName = args[1];

try
{
    var cancelSource = new CancellationTokenSource();
    using var discordClient = new DiscordPipeClient(discordClientId);
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
finally
{
    Console.WriteLine("Stopping subprocess");
}
