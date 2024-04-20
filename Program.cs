using Shizou.MpvDiscordPresence;

if (args.Length != 1)
    throw new InvalidOperationException("Must provide single argument: discord client id");

var discordClientId = long.Parse(args[0]);

var cancelSource = new CancellationTokenSource();
await using var client = new MpvPipeClient("shizou-socket", discordClientId, cancelSource);

try
{
    var tasks = new[] { Task.Run(client.ReadLoop), Task.Run(client.QueryLoop) };

    Task.WaitAny(tasks);
    cancelSource.Cancel();
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
