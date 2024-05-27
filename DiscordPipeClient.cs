using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading.Channels;

namespace Shizou.MpvDiscordPresence;

public class DiscordPipeClient
{
    private readonly ConcurrentDictionary<int, Channel<DiscordPipeResponse>> _responses = new();
    private readonly string _discordClientId;
    private NamedPipeClientStream? _pipeClientStream;
    private StreamReader? _lineReader;
    private StreamWriter? _lineWriter;

    public DiscordPipeClient(string discordClientId) => _discordClientId = discordClientId;


    public async Task ReadLoop()
    {
    }


    public async Task UpdatePresenceLoop()
    {
    }

    private async Task Connect(CancellationToken cancelToken)
    {
        for (var i = 0; i < 10; ++i)
        {
            var newPipe = new NamedPipeClientStream(".", GetPipeName(i), PipeDirection.InOut, PipeOptions.Asynchronous);
            await newPipe.ConnectAsync(TimeSpan.FromMilliseconds(200), cancelToken);
            if (newPipe.IsConnected)
            {
                _pipeClientStream = newPipe;
                break;
            }
        }

        static string GetTemporaryDirectory()
        {
            var temp = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            temp ??= Environment.GetEnvironmentVariable("TMPDIR");
            temp ??= Environment.GetEnvironmentVariable("TMP");
            temp ??= Environment.GetEnvironmentVariable("TEMP");
            temp ??= "/tmp";
            return temp;
        }

        static string GetPipeName(int pipe)
        {
            var pipeName = $"discord-ipc-{pipe}";
            return Environment.OSVersion.Platform is PlatformID.Unix ? Path.Combine(GetTemporaryDirectory(), pipeName) : pipeName;
        }
    }
}

public record DiscordPipeRequest;

public record DiscordPipeResponse;
