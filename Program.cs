using Shizou.MpvDiscordPresence;

if (args.Length != 2)
    throw new InvalidOperationException("Must provide two arguments: discord client id and ipc socket/pipe name");

var discordClientId = args[0];
var socketName = args[1];

var cancelSource = new CancellationTokenSource();
using var client = new MpvPipeClient(socketName, discordClientId);

try
{
    await client.Connect(cancelSource.Token);
    var tasks = new[] { client.ReadLoop(cancelSource.Token), client.QueryLoop(cancelSource.Token) };

    Task.WaitAny(tasks);
    await cancelSource.CancelAsync();
    Task.WaitAll(tasks);
}
catch (AggregateException ae)
{
    ae.Handle(ex => ex is OperationCanceledException or IOException);
}
finally
{
    Console.WriteLine("Stopping subprocess");
}
