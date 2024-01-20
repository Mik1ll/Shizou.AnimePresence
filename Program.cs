using System.Reflection;
using System.Runtime.InteropServices;
using Discord;
using Shizou.MpvDiscordPresence;

NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

using var discord = new Discord.Discord(737663962677510245, (ulong)CreateFlags.NoRequireDiscord);

await using var client = new MpvPipeClient("shizou-socket");

_ = Task.Run(client.ReadLoop);

for (;; await Task.Delay(TimeSpan.FromSeconds(5)))
{
    var playlistPos = (await client.GetPropertyAsync("playlist-pos")).GetInt32();
    var position = (await client.GetPropertyAsync("time-pos")).GetDouble();
    var duration = (await client.GetPropertyAsync("duration")).GetDouble();
    var paused = (await client.GetPropertyAsync("pause")).GetBoolean();
    var playlistTitle = await client.GetPropertyStringAsync($"playlist/{playlistPos}/title");
    var splitIdx = playlistTitle.LastIndexOf('-');
    var title = playlistTitle[..splitIdx].Trim();
    var epNo = playlistTitle[splitIdx..].Trim();

    var activity = new Activity
    {
        Details = title,
        State = epNo[0] switch
        {
            'S' => "Special " + epNo[1..],
            'C' => "Credit " + epNo[1..],
            'T' => "Trailer " + epNo[1..],
            'P' => "Parody " + epNo[1..],
            'O' => "Other " + epNo[1..],
            _ => "Episode " + epNo
        },
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
