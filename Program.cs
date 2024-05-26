using Shizou.MpvDiscordPresence;

if (args.Length != 1)
    throw new InvalidOperationException("Must provide single argument: discord client id");

var discordClientId = args[0];

var cancelSource = new CancellationTokenSource();
using var client = new MpvPipeClient("shizou-socket", discordClientId, cancelSource);

try
{
    var tasks = new[] { client.ReadLoop(), client.QueryLoop() };

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
