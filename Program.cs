using System.Reflection;
using System.Runtime.InteropServices;
using Discord;
using Shizou.MpvDiscordPresence;

if (args.Length != 1)
    throw new InvalidOperationException("Must provide single argument: discord client id");

var discordClientId = long.Parse(args[0]);

NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

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

static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
{
    if (libraryName == Constants.DllName)
    {
        // On systems with AVX2 support, load a different library.
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            return NativeLibrary.Load("DiscordGameSDK/lib/x86_64/" + Constants.DllName, assembly, searchPath);
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            return NativeLibrary.Load("DiscordGameSDK/lib/aarch64/" + Constants.DllName, assembly, searchPath);
        if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            return NativeLibrary.Load("DiscordGameSDK/lib/x86/" + Constants.DllName, assembly, searchPath);
    }

    // Otherwise, fallback to default import resolver.
    return IntPtr.Zero;
}
