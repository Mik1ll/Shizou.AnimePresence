using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shizou.MpvDiscordPresence;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record Button
{
    private readonly string _label;
    private readonly string _url;

    public required string label
    {
        get => _label;
        [MemberNotNull(nameof(_label))]
        init => _label = RichPresence.GetBoundedString(value, 32);
    }

    public required string url
    {
        get => _url;
        [MemberNotNull(nameof(_url))]
        init => _url = RichPresence.GetBoundedString(value, 512);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record Assets
{
    private readonly string _smallImage;
    private readonly string _smallText;
    private readonly string _largeImage;
    private readonly string _largeText;

    public required string small_image
    {
        get => _smallImage;
        [MemberNotNull(nameof(_smallImage))]
        init => _smallImage = RichPresence.GetBoundedString(value, 256);
    }

    public required string small_text
    {
        get => _smallText;
        [MemberNotNull(nameof(_smallText))]
        init => _smallText = RichPresence.GetBoundedString(value, 128);
    }

    public required string large_image
    {
        get => _largeImage;
        [MemberNotNull(nameof(_largeImage))]
        init => _largeImage = RichPresence.GetBoundedString(value, 256);
    }

    public required string large_text
    {
        get => _largeText;
        [MemberNotNull(nameof(_largeText))]
        init => _largeText = RichPresence.GetBoundedString(value, 128);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
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

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record Party
{
    public string? id { get; init; }

    public required PartySize size { get; init; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record PartySize
{
    public int currentSize { get; init; }
    public int maxSize { get; init; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record RichPresence
{
    private readonly Button[]? _buttons;
    private readonly string _details;
    private readonly string _state;

    public required string details
    {
        get => _details;
        [MemberNotNull(nameof(_details))]
        init => _details = GetBoundedString(value, 128);
    }

    public required string state
    {
        get => _state;
        [MemberNotNull(nameof(_state))]
        init => _state = GetBoundedString(value, 128);
    }

    public Assets? assets { get; init; }

    public TimeStamps? timestamps { get; init; }

    public Party? party { get; init; }

    public Button[]? buttons
    {
        get => _buttons;
        init => _buttons = value?.Length > 2
            ? throw new ArgumentOutOfRangeException(nameof(buttons), value.Length, "Array can only hold up to two elements")
            : value;
    }

    public bool instance { get; init; } = true;

    public static string GetBoundedString(string value, int byteLength, [CallerMemberName] string? propertyName = null)
    {
        value = value.Trim();
        if (Encoding.UTF8.GetByteCount(value) > byteLength)
            throw new ArgumentOutOfRangeException(propertyName, value, $"Must be within {byteLength} bytes");
        return value;
    }
}
