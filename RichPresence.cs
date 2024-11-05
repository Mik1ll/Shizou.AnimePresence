// ReSharper disable InconsistentNaming

namespace Shizou.AnimePresence;

public enum ActivityType
{
    Playing = 0,
    Streaming = 1,
    Listening = 2,
    Watching = 3,
    Custom = 4,
    Competing = 5
}

public record Button
{
    /// <summary>
    /// Max 32 bytes
    /// </summary>
    public required string label { get; init; }

    /// <summary>
    /// Max 512 bytes
    /// </summary>
    public required string url { get; init; }
}

public record Assets
{
    /// <summary>
    /// Max 256 bytes
    /// </summary>
    public string? small_image { get; init; }

    /// <summary>
    /// Max 128 bytes
    /// </summary>
    public string? small_text { get; init; }

    /// <summary>
    /// Max 256 bytes
    /// </summary>
    public string? large_image { get; init; }

    /// <summary>
    /// Max 128 bytes
    /// </summary>
    public string? large_text { get; init; }
}

public record TimeStamps
{
    public long? start { get; init; }
    public long? end { get; init; }

    public static TimeStamps FromPlaybackPosition(double played, double remaining) => new()
    {
        start = (DateTimeOffset.UtcNow - TimeSpan.FromSeconds(played)).ToUnixTimeSeconds(),
        end = (DateTimeOffset.UtcNow + TimeSpan.FromSeconds(remaining)).ToUnixTimeSeconds()
    };
}

public record RichPresence
{
    public int type { get; init; } = (int)ActivityType.Watching;

    /// <summary>
    /// Max 128 bytes
    /// </summary>
    public string? details { get; init; }

    /// <summary>
    /// Max 128 bytes
    /// </summary>
    public string? state { get; init; }

    public Assets? assets { get; init; }

    public TimeStamps? timestamps { get; init; }

    /// <summary>
    /// Max 2 buttons
    /// </summary>
    public Button[]? buttons { get; init; }
}
