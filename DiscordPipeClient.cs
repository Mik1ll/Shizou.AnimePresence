﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Shizou.AnimePresence;

public class DiscordPipeClient : IDisposable
{
    private static readonly int ProcessId = Process.GetCurrentProcess().Id;
    private readonly string _discordClientId;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private NamedPipeClientStream? _pipeClientStream;
    private bool _isReady;

    public DiscordPipeClient(string discordClientId) => _discordClientId = discordClientId;


    public async Task ReadLoop(CancellationToken cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            try
            {
                if (_pipeClientStream is null)
                {
                    await Connect(cancelToken);
                    continue;
                }

                var opCode = (Opcode)await ReadUInt32Async(_pipeClientStream, cancelToken);
                var length = await ReadUInt32Async(_pipeClientStream, cancelToken);
                var payload = new byte[length];
                await _pipeClientStream.ReadExactlyAsync(payload, 0, Convert.ToInt32(length), cancelToken);
                cancelToken.ThrowIfCancellationRequested();

                switch (opCode)
                {
                    case Opcode.Close:
                        var close = JsonSerializer.Deserialize(payload, CloseContext.Default.Close)!;
                        throw new InvalidOperationException($"Discord closed the connection with error {close.code}: {close.message}");
                    case Opcode.Frame:
                        var message = JsonSerializer.Deserialize(payload, MessageContext.Default.Message);
                        if (message?.evt == Event.ERROR.ToString())
                            throw new InvalidOperationException($"Discord returned error: {message.data}");
                        if (message?.evt == Event.READY.ToString())
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
            catch (EndOfStreamException)
            {
                _isReady = false;
                _pipeClientStream = null;
            }
        }
    }

    public async Task SetPresenceAsync(RichPresence? presence, CancellationToken cancelToken)
    {
        if (!_isReady)
            return;

        var cmd = new PresenceCommand(ProcessId, presence);
        var frame = new Message(Command.SET_ACTIVITY.ToString(), null, Guid.NewGuid().ToString(), null, cmd);
        await WriteFrameAsync(frame, cancelToken);
    }

    public void Dispose()
    {
        _pipeClientStream?.Dispose();
    }

    private async Task Connect(CancellationToken cancelToken)
    {
        while (_pipeClientStream?.IsConnected is not true)
        {
            for (var i = 0; i < 10; ++i)
            {
                _pipeClientStream = new NamedPipeClientStream(".", GetPipeName(i), PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    await _pipeClientStream.ConnectAsync(TimeSpan.FromMilliseconds(200), cancelToken);
                    cancelToken.ThrowIfCancellationRequested();
                }
                catch (TimeoutException)
                {
                }

                if (_pipeClientStream.IsConnected)
                    break;
            }

            if (_pipeClientStream?.IsConnected is true)
                break;
            await Task.Delay(TimeSpan.FromSeconds(10), cancelToken);
            cancelToken.ThrowIfCancellationRequested();
        }

        await WriteFrameAsync(new HandShake(_discordClientId), cancelToken);
        cancelToken.ThrowIfCancellationRequested();

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
        Opcode opcode;
        JsonTypeInfo jsonTypeInfo;
        switch (payload)
        {
            case Message:
                opcode = Opcode.Frame;
                jsonTypeInfo = MessageContext.Default.Message;
                break;
            case Close:
                opcode = Opcode.Close;
                jsonTypeInfo = CloseContext.Default.Close;
                break;
            case HandShake:
                opcode = Opcode.Handshake;
                jsonTypeInfo = HandShakeContext.Default.HandShake;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(payload), payload, null);
        }

        var opCodeBytes = BitConverter.GetBytes(Convert.ToUInt32(opcode));
//        var payloadString = JsonSerializer.Serialize(payload, jsonTypeInfo);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, jsonTypeInfo);
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

    private static string SmartStringTrim(string str, int length)
    {
        if (str.Length <= length)
            return str;
        return str[..str[..(length + 1)].LastIndexOf(' ')] + "...";
    }

    public static RichPresence? CreateNewPresence(QueryInfo queryInfo, bool paused, double playbackTime, double timeLeft)
    {
        if (paused)
            return null;
        return new RichPresence
        {
            details = SmartStringTrim(queryInfo.AnimeName, 64),
            state = $"{queryInfo.EpisodeType} {queryInfo.EpisodeNumber}" +
                    (queryInfo.EpisodeType != "Episode" || queryInfo.EpisodeCount is null
                        ? string.Empty
                        : $" of {queryInfo.EpisodeCount}"),
            timestamps = TimeStamps.FromPlaybackPosition(playbackTime, timeLeft),
            assets = new Assets
            {
                large_image = string.IsNullOrWhiteSpace(queryInfo.PosterUrl) ? "mpv" : queryInfo.PosterUrl,
                large_text = string.IsNullOrWhiteSpace(queryInfo.EpisodeName) ? "mpv" : SmartStringTrim(queryInfo.EpisodeName, 64),
            },
            buttons = string.IsNullOrWhiteSpace(queryInfo.AnimeUrl) ? [] : [new Button { label = "View Anime", url = queryInfo.AnimeUrl }]
        };
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record HandShake(string client_id, int v = 1);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HandShake))]
internal partial class HandShakeContext : JsonSerializerContext;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record Message(string cmd, string? evt, string? nonce, object? data, object? args);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(PresenceCommand))]
internal partial class MessageContext : JsonSerializerContext;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public record Close(int code, string message);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Close))]
internal partial class CloseContext : JsonSerializerContext;

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

public enum Opcode
{
    Handshake = 0,
    Frame = 1,
    Close = 2,
    Ping = 3,
    Pong = 4
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record PresenceCommand(int pid, RichPresence? activity);
