// See https://aka.ms/new-console-template for more information

using Discord;
using Shizou.MpvDiscordPresence;

//var discord = new Discord.Discord(737663962677510245, (ulong)CreateFlags.NoRequireDiscord);

//var activityManager = discord.ActivityManagerInstance;

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


    //activityManager.UpdateActivity(activity, result => {});

    Thread.Sleep(TimeSpan.FromSeconds(4));
}
