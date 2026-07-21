using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TerrariaYokonex.Core.Config;
using TerrariaYokonex.Core.Models;

namespace TerrariaYokonex.Core.Services
{
    public sealed class YokonexWebSocketCommandSender : IDisposable
    {
        private static readonly Uri AdapterConfigUri = new Uri("http://127.0.0.1:43002/v1/game-integrations/terraria/adapter-config");
        private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);
        private readonly HttpClient _http = new HttpClient();
        private readonly string _sessionId = "terraria-" + Guid.NewGuid().ToString("N");
        private ClientWebSocket? _socket;
        private AdapterConfig _config = new AdapterConfig();
        private DateTimeOffset _nextConfigRefreshAt = DateTimeOffset.MinValue;
        private string _activeEndpoint = string.Empty;

        public async Task<YokonexDispatchResult> SendCommandAsync(
            YokonexWebSocketSettings settings,
            YokonexRouteRule rule,
            TerrariaEventRecord eventRecord,
            CancellationToken cancellationToken)
        {
            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                await RefreshConfigAsync(settings, cancellationToken);
                if (!_config.Enabled)
                {
                    return Success("GameHub 中的泰拉瑞亚联动已停用");
                }

                if (!_config.Mappings.TryGetValue(eventRecord.EventKey, out string? commandId) || string.IsNullOrWhiteSpace(commandId))
                {
                    return Success("该事件已在 GameHub 中停用");
                }

                await EnsureConnectedAsync(settings, cancellationToken);
                string eventId = Guid.NewGuid().ToString("N");
                await SendJsonAsync(_socket!, new
                {
                    source = "terraria",
                    eventKey = eventRecord.EventKey,
                    commandId,
                    occurredAt = eventRecord.OccurredAt.ToUniversalTime().ToString("O"),
                    sessionId = _sessionId,
                    eventId,
                    matchValue = string.IsNullOrWhiteSpace(eventRecord.MatchValue) ? null : eventRecord.MatchValue,
                    data = new
                    {
                        displayText = eventRecord.DisplayText,
                        amount = eventRecord.Amount,
                    },
                }, cancellationToken);

                return await WaitForEventResultAsync(_socket!, eventId, settings, cancellationToken);
            }
            catch (Exception ex)
            {
                await CleanupSocketAsync();
                _nextConfigRefreshAt = DateTimeOffset.MinValue;
                return Failure("Yokonex-ModHub 暂不可用: " + ex.Message);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task<YokonexDispatchResult> LoginAsync(YokonexWebSocketSettings settings, CancellationToken cancellationToken)
        {
            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                await RefreshConfigAsync(settings, cancellationToken, true);
                await EnsureConnectedAsync(settings, cancellationToken);
                return Success("已连接 Yokonex-ModHub");
            }
            catch (Exception ex)
            {
                await CleanupSocketAsync();
                return Failure("连接 Yokonex-ModHub 失败: " + ex.Message);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task<YokonexDispatchResult> LogoutAsync(CancellationToken cancellationToken)
        {
            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                await CleanupSocketAsync();
                return Success("已断开 Yokonex-ModHub");
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private async Task RefreshConfigAsync(
            YokonexWebSocketSettings settings,
            CancellationToken cancellationToken,
            bool force = false)
        {
            if (!force && DateTimeOffset.UtcNow < _nextConfigRefreshAt)
            {
                return;
            }

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, settings.ConnectTimeoutMs)));
            string raw = await _http.GetStringAsync(AdapterConfigUri, timeout.Token);
            AdapterConfig? loaded = JsonSerializer.Deserialize<AdapterConfig>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            _config = loaded ?? throw new InvalidOperationException("GameHub 返回了无效配置");
            _config.Mappings = new Dictionary<string, string>(_config.Mappings, StringComparer.OrdinalIgnoreCase);
            _nextConfigRefreshAt = DateTimeOffset.UtcNow.AddSeconds(5);
        }

        private async Task EnsureConnectedAsync(YokonexWebSocketSettings settings, CancellationToken cancellationToken)
        {
            string endpoint = string.IsNullOrWhiteSpace(_config.Endpoint)
                ? YokonexKnownValues.DefaultWebSocketUrl
                : _config.Endpoint;
            if (_socket != null && _socket.State == WebSocketState.Open &&
                string.Equals(endpoint, _activeEndpoint, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await CleanupSocketAsync();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, settings.ConnectTimeoutMs)));
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(new Uri(endpoint), timeout.Token);
            _activeEndpoint = endpoint;
        }

        private static async Task<YokonexDispatchResult> WaitForEventResultAsync(
            ClientWebSocket socket,
            string eventId,
            YokonexWebSocketSettings settings,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, settings.ReceiveTimeoutMs)));
            while (true)
            {
                string raw = await ReceiveMessageAsync(socket, timeout.Token);
                using JsonDocument document = JsonDocument.Parse(raw);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("type", out JsonElement type) || type.GetString() != "eventResult")
                {
                    continue;
                }
                if (root.TryGetProperty("eventId", out JsonElement returnedId) && returnedId.GetString() != eventId)
                {
                    continue;
                }
                bool accepted = root.TryGetProperty("accepted", out JsonElement acceptedElement) && acceptedElement.GetBoolean();
                string message = root.TryGetProperty("message", out JsonElement messageElement)
                    ? messageElement.GetString() ?? (accepted ? "事件已接收" : "事件被拒绝")
                    : (accepted ? "事件已接收" : "事件被拒绝");
                return accepted ? Success(message) : Failure(message);
            }
        }

        private static async Task SendJsonAsync(ClientWebSocket socket, object payload, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private static async Task<string> ReceiveMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            using MemoryStream stream = new MemoryStream();
            while (true)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException("GameHub 已断开连接");
                }
                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        private async Task CleanupSocketAsync()
        {
            ClientWebSocket? old = _socket;
            _socket = null;
            _activeEndpoint = string.Empty;
            if (old == null)
            {
                return;
            }
            try
            {
                if (old.State == WebSocketState.Open)
                {
                    await old.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None);
                }
            }
            catch
            {
            }
            old.Dispose();
        }

        private static YokonexDispatchResult Success(string message) => new YokonexDispatchResult { Success = true, Message = message };
        private static YokonexDispatchResult Failure(string message) => new YokonexDispatchResult { Success = false, Message = message };

        public void Dispose()
        {
            try
            {
                _sessionLock.Wait();
                CleanupSocketAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }
            finally
            {
                try { _sessionLock.Release(); } catch { }
                _http.Dispose();
                _sessionLock.Dispose();
            }
        }

        private sealed class AdapterConfig
        {
            public bool Enabled { get; set; }
            public string Endpoint { get; set; } = YokonexKnownValues.DefaultWebSocketUrl;
            public Dictionary<string, string> Mappings { get; set; } = new Dictionary<string, string>();
        }
    }
}
