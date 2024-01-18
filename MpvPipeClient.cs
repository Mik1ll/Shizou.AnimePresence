using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace Shizou.MpvDiscordPresence;

public class MpvPipeClient : IDisposable
{
    private int _nextRequestId;

    // ReSharper disable InconsistentNaming
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record Request(string[] command, int request_id);

    // ReSharper disable once ClassNeverInstantiated.Local
    private record Response(string error, JsonElement data, int request_id);
    // ReSharper restore InconsistantNaming

    private readonly NamedPipeClientStream _pipeClientStream;
    private readonly int _timeout = 500;
    private readonly object _exchangeLock = new();
    private readonly Random _random = new();

    public MpvPipeClient(string serverPath)
    {
        _pipeClientStream = new NamedPipeClientStream(".", serverPath, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Anonymous);
    }

    private void Connect()
    {
        if (!_pipeClientStream.IsConnected)
            _pipeClientStream.Connect(_timeout);
    }

    private Request NewRequest(params string[] command)
    {
        _nextRequestId = _random.Next();
        return new Request(command, _nextRequestId);
    }

    public string GetPropertyString(string key)
    {
        var request = NewRequest("get_property_string", key);
        var (response, error) = ExecuteQuery(request);
        if (error is not null)
            return "";
        return response?.GetString() ?? "";
    }

    private (JsonElement? data, string? error) ExecuteQuery(Request request)
    {
        lock (_exchangeLock)
        {
            SendRequest();
            return ReceiveResponse();
        }

        void SendRequest()
        {
            Connect();
            var requestJson = JsonSerializer.Serialize(request) + '\n';
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            _pipeClientStream.Write(requestBytes);
        }

        (JsonElement? data, string? error) ReceiveResponse()
        {
            try
            {
                var buffer = new byte[1024];
                var bytesRead = 0;

                using var ms = new MemoryStream();
                do
                {
                    bytesRead += _pipeClientStream.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, bytesRead);
                } while (bytesRead != 0 && buffer[bytesRead - 1] != '\n');

                ms.Position = 0;
                var response = JsonSerializer.Deserialize<Response>(ms);
                if (response?.request_id != request.request_id)
                    return (null, "Request ID does not match");
                if (response.error != "success")
                    return (null, "Response returned error");
                return (response.data, null);
            }
            catch (Exception ex)
            {
                return (null, $"Read threw exception: {ex}");
            }
        }
    }

    public void Dispose()
    {
        _pipeClientStream.Dispose();
    }
}
