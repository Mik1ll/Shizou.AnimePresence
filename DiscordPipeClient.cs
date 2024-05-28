using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Threading.Channels;

namespace Shizou.MpvDiscordPresence;

public class DiscordPipeClient : IDisposable
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

    public void Dispose()
    {
        _pipeClientStream?.Dispose();
    }

    private async Task Connect(CancellationToken cancelToken)
    {
        for (var i = 0; i < 10; ++i)
        {
            _pipeClientStream = new NamedPipeClientStream(".", GetPipeName(i), PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipeClientStream.ConnectAsync(TimeSpan.FromMilliseconds(200), cancelToken);
            cancelToken.ThrowIfCancellationRequested();
            if (_pipeClientStream.IsConnected)
                break;
        }

        if (_pipeClientStream?.IsConnected is not true)
            throw new InvalidOperationException("Failed to connect to discord ipc");

        _lineReader = new StreamReader(_pipeClientStream);
        _lineWriter = new StreamWriter(_pipeClientStream) { AutoFlush = true };

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
