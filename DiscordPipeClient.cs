using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shizou.MpvDiscordPresence;

public class DiscordPipeClient : IDisposable
{
    private static readonly int ProcessId = Process.GetCurrentProcess().Id;
    private static readonly JsonSerializerOptions JsonOpts = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly string _discordClientId;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private NamedPipeClientStream? _pipeClientStream;
    private int _nonce;
    private bool _isReady;

    public DiscordPipeClient(string discordClientId) => _discordClientId = discordClientId;


    public async Task ReadLoop(CancellationToken cancelToken)
    {
        if (_pipeClientStream is null)
            return;
        while (!cancelToken.IsCancellationRequested)
        {
            var opCode = (Opcode)await ReadUInt32Async(_pipeClientStream, cancelToken);
            var length = await ReadUInt32Async(_pipeClientStream, cancelToken);
            var payload = new byte[length];
            await _pipeClientStream.ReadExactlyAsync(payload, 0, Convert.ToInt32(length), cancelToken);
            cancelToken.ThrowIfCancellationRequested();
            switch (opCode)
            {
                case Opcode.Close:
                    var close = JsonSerializer.Deserialize<Close>(payload)!;
                    throw new InvalidOperationException($"Discord closed the connection with error {close.code}: {close.message}");
                case Opcode.Frame:
                    var message = JsonSerializer.Deserialize<Message>(payload);
                    if (message?.evt is Event.ERROR)
                        throw new InvalidOperationException($"Discord returned error: {message.data}");
                    if (message?.evt is Event.READY)
                        _isReady = true;
                    break;
                case Opcode.Ping:
                    var buff = new byte[length + 8];
                    BitConverter.GetBytes(Convert.ToUInt32(Opcode.Pong)).CopyTo(buff, 0);
                    BitConverter.GetBytes(length).CopyTo(buff, 4);
                    payload.CopyTo(buff, 8);
                    await _writeLock.WaitAsync(cancelToken);
                    await _pipeClientStream.WriteAsync(buff, cancelToken);
                    await _pipeClientStream.FlushAsync(cancelToken);
                    _writeLock.Release();
                    cancelToken.ThrowIfCancellationRequested();
                    break;
                case Opcode.Pong:
                    break;
                default:
                    throw new InvalidOperationException($"Discord sent unexpected payload: {opCode}: {Encoding.UTF8.GetString(payload)}");
            }
        }
    }

    public async Task Connect(CancellationToken cancelToken)
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

        await WriteFrameAsync(new HandShake(_discordClientId), cancelToken);

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

    public async Task SetPresenceAsync(RichPresence presence, CancellationToken cancelToken)
    {
        if (!_isReady)
            return;
        var cmd = new PresenceCommand(ProcessId, presence);
        var frame = new Message(Command.SET_ACTIVITY, null, (++_nonce).ToString(), null, cmd);
        await WriteFrameAsync(frame, cancelToken);
    }

    public void Dispose()
    {
        _pipeClientStream?.Dispose();
    }

    private async Task<uint> ReadUInt32Async(Stream stream, CancellationToken cancelToken)
    {
        var buff = new byte[4];
        await stream.ReadExactlyAsync(buff, cancelToken);
        cancelToken.ThrowIfCancellationRequested();
        return BitConverter.ToUInt32(buff);
    }

    private async Task WriteFrameAsync<T>(T payload, CancellationToken cancelToken)
    {
        if (_pipeClientStream is null)
            throw new InvalidOperationException("Pipe client can't be null");
        var opCodeBytes = BitConverter.GetBytes(Convert.ToUInt32(payload switch
        {
            Message => Opcode.Frame,
            Close => Opcode.Close,
            HandShake => Opcode.Handshake,
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload, null)
        }));
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
        var lengthBytes = BitConverter.GetBytes(Convert.ToUInt32(payloadBytes.Length));
        var buff = new byte[opCodeBytes.Length + lengthBytes.Length + payloadBytes.Length];
        opCodeBytes.CopyTo(buff, 0);
        lengthBytes.CopyTo(buff, opCodeBytes.Length);
        payloadBytes.CopyTo(buff, opCodeBytes.Length + lengthBytes.Length);
        await _writeLock.WaitAsync(cancelToken);
        await _pipeClientStream.WriteAsync(buff, cancelToken);
        await _pipeClientStream.FlushAsync(cancelToken);
        _writeLock.Release();
        cancelToken.ThrowIfCancellationRequested();
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record HandShake(string client_id, int v = 1);

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record Message(Command cmd, Event? evt, string? nonce, object? data, object? args);

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record Close(int code, string message);

[JsonConverter(typeof(JsonStringEnumConverter<Command>))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum Command
{
    DISPATCH,
    SET_ACTIVITY
}

[JsonConverter(typeof(JsonStringEnumConverter<Event>))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum Event
{
    READY,
    ERROR
}

public enum Opcode : uint
{
    Handshake = 0,
    Frame = 1,
    Close = 2,
    Ping = 3,
    Pong = 4
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record PresenceCommand(int pid, RichPresence activity);
