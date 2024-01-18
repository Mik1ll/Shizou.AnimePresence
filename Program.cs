// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Runtime.InteropServices;
using Discord;
using Shizou.MpvDiscordPresence;

NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

var discord = new Discord.Discord(737663962677510245, (ulong)CreateFlags.NoRequireDiscord);

using var client = new MpvPipeClient("tmp/mpvsocket");

while (true)
{
    var playlistCount = client.GetProperty("playlist-count").GetInt32();
    var playlistPos = playlistCount > 1 ? client.GetProperty("playlist-pos").GetInt32() : 0;
    var position = client.GetProperty("time-pos").GetDouble();
    var duration = client.GetProperty("duration").GetDouble();
    var paused = client.GetProperty("pause").GetBoolean();
    var playlistTitle = client.GetPropertyString($"playlist/{playlistPos}/title");

    var activity = new Activity
    {
        Details = playlistTitle,
        State = "Placeholder", // Will use episode number here later
        Timestamps = new ActivityTimestamps
        {
            Start = (DateTimeOffset.UtcNow - TimeSpan.FromSeconds(position)).ToUnixTimeSeconds(),
            End = (DateTimeOffset.UtcNow + TimeSpan.FromSeconds(duration)).ToUnixTimeSeconds()
        },
        Assets = new ActivityAssets
        {
            LargeImage = "mpv",
            LargeText = "mpv",
            SmallImage = paused ? "pause" : "play",
            SmallText = paused ? "Paused" : "Playing"
        }
    };


    discord.GetActivityManager().UpdateActivity(activity, _ => { });
    discord.RunCallbacks();

    Thread.Sleep(TimeSpan.FromSeconds(5));
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
