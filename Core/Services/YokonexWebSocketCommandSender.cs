using System;
using System.IO;
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
        private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);
        private ClientWebSocket? _socket;
        private string _loggedInUserId = string.Empty;
        private string _activeWsUrl = string.Empty;
        private string _activeUid = string.Empty;
        private string _activeToken = string.Empty;

        public async Task<YokonexDispatchResult> SendCommandAsync(
            YokonexWebSocketSettings settings,
            YokonexRouteRule rule,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rule.CommandId))
            {
                return new YokonexDispatchResult
                {
                    Success = false,
                    Message = "WebSocket 输出配置不完整",
                };
            }

            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                // 事件发送前先确保 IM 会话可复用，避免每条事件都重复登录退出。
                YokonexDispatchResult loginResult = await EnsureLoggedInAsync(settings, cancellationToken);
                if (!loginResult.Success || _socket == null || string.IsNullOrWhiteSpace(_loggedInUserId))
                {
                    return loginResult;
                }

                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, settings.ReceiveTimeoutMs)));

                await SendJsonAsync(_socket, new
                {
                    type = "sendCommand",
                    userId = _loggedInUserId,
                    commandId = rule.CommandId,
                }, linkedCts.Token);

                return await WaitForResultAsync(_socket, "commandResult", linkedCts.Token, message =>
                {
                    bool success = message.RootElement.TryGetProperty("success", out JsonElement successElement) &&
                                   successElement.GetBoolean();
                    return new YokonexDispatchResult
                    {
                        Success = success,
                        Message = ReadMessage(message, success ? "指令发送成功" : "指令发送失败"),
                    };
                });
            }
            catch (Exception ex)
            {
                await CleanupSocketAsync();
                return new YokonexDispatchResult
                {
                    Success = false,
                    Message = "WebSocket 指令发送异常: " + ex.Message,
                };
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
                return await EnsureLoggedInAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                await CleanupSocketAsync();
                return new YokonexDispatchResult
                {
                    Success = false,
                    Message = "WebSocket 登录异常: " + ex.Message,
                };
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
                if (_socket == null || _socket.State != WebSocketState.Open || string.IsNullOrWhiteSpace(_loggedInUserId))
                {
                    await CleanupSocketAsync();
                    return new YokonexDispatchResult
                    {
                        Success = true,
                        Message = "当前未登录",
                    };
                }

                await SendJsonAsync(_socket, new
                {
                    type = "logout",
                    userId = _loggedInUserId,
                }, cancellationToken);

                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "logout", CancellationToken.None);
                }

                await CleanupSocketAsync();
                return new YokonexDispatchResult
                {
                    Success = true,
                    Message = "已退出登录",
                };
            }
            catch (Exception ex)
            {
                await CleanupSocketAsync();
                return new YokonexDispatchResult
                {
                    Success = false,
                    Message = "WebSocket 退出登录异常: " + ex.Message,
                };
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private async Task<YokonexDispatchResult> EnsureLoggedInAsync(
            YokonexWebSocketSettings settings,
            CancellationToken cancellationToken)
        {
            string resolvedUid = settings.ResolveUid();
            if (!settings.Enabled)
            {
                return new YokonexDispatchResult
                {
                    Success = false,
                    Message = "WebSocket 输出未启用",
                };
            }

            if (string.IsNullOrWhiteSpace(settings.WsUrl) ||
                string.IsNullOrWhiteSpace(resolvedUid) ||
                string.IsNullOrWhiteSpace(settings.Token))
            {
                return new YokonexDispatchResult
                {
                    Success = false,
                    Message = "WebSocket 输出配置不完整",
                };
            }

            if (_socket != null &&
                _socket.State == WebSocketState.Open &&
                string.Equals(_activeWsUrl, settings.WsUrl, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_activeUid, resolvedUid, StringComparison.Ordinal) &&
                string.Equals(_activeToken, settings.Token, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(_loggedInUserId))
            {
                // 连接参数未变化时直接复用当前登录态，减少频繁重连对下游服务的压力。
                return new YokonexDispatchResult
                {
                    Success = true,
                    Message = "已登录: " + _loggedInUserId,
                };
            }

            await CleanupSocketAsync();

            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, settings.ConnectTimeoutMs)));

            ClientWebSocket socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(settings.WsUrl), linkedCts.Token);

            linkedCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, settings.ReceiveTimeoutMs)));
            await SendJsonAsync(socket, new
            {
                type = "login",
                uid = resolvedUid,
                token = settings.Token,
            }, linkedCts.Token);

            string resolvedUserId = settings.ResolveUserId();
            YokonexDispatchResult loginResult = await WaitForResultAsync(socket, "loginResult", linkedCts.Token, message =>
            {
                if (!message.RootElement.TryGetProperty("success", out JsonElement successElement) ||
                    !successElement.GetBoolean())
                {
                    return new YokonexDispatchResult
                    {
                        Success = false,
                        Message = ReadMessage(message, "下游 WebSocket 登录失败"),
                    };
                }

                if (message.RootElement.TryGetProperty("data", out JsonElement dataElement) &&
                    dataElement.ValueKind == JsonValueKind.Object &&
                    dataElement.TryGetProperty("userId", out JsonElement userIdElement))
                {
                    string? userId = userIdElement.GetString();
                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        resolvedUserId = userId;
                    }
                }

                return new YokonexDispatchResult
                {
                    Success = true,
                    Message = "登录成功",
                };
            });

            if (!loginResult.Success)
            {
                socket.Dispose();
                return loginResult;
            }

            if (string.IsNullOrWhiteSpace(resolvedUserId))
            {
                socket.Dispose();
                return new YokonexDispatchResult
                {
                    Success = false,
                    Message = "WebSocket 登录成功，但没有拿到可用的 userId",
                };
            }

            // 只有登录完全成功后才切换活动会话，避免异常时污染当前连接状态。
            _socket = socket;
            _loggedInUserId = resolvedUserId;
            _activeWsUrl = settings.WsUrl;
            _activeUid = resolvedUid;
            _activeToken = settings.Token;

            return new YokonexDispatchResult
            {
                Success = true,
                Message = "已登录: " + resolvedUserId,
            };
        }

        private async Task CleanupSocketAsync()
        {
            if (_socket != null)
            {
                try
                {
                    if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                    {
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "cleanup", CancellationToken.None);
                    }
                }
                catch
                {
                }

                _socket.Dispose();
                _socket = null;
            }

            _loggedInUserId = string.Empty;
            _activeWsUrl = string.Empty;
            _activeUid = string.Empty;
            _activeToken = string.Empty;
        }

        private static async Task SendJsonAsync(ClientWebSocket socket, object payload, CancellationToken cancellationToken)
        {
            string raw = JsonSerializer.Serialize(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(raw);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private static async Task<YokonexDispatchResult> WaitForResultAsync(
            ClientWebSocket socket,
            string expectedType,
            CancellationToken cancellationToken,
            Func<JsonDocument, YokonexDispatchResult> converter)
        {
            while (true)
            {
                string raw = await ReceiveMessageAsync(socket, cancellationToken);
                using JsonDocument document = JsonDocument.Parse(raw);

                if (!document.RootElement.TryGetProperty("type", out JsonElement typeElement))
                {
                    continue;
                }

                string? messageType = typeElement.GetString();
                if (!string.Equals(messageType, expectedType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return converter(document);
            }
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
                    throw new InvalidOperationException("服务端主动关闭了 WebSocket 连接");
                }

                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        private static string ReadMessage(JsonDocument document, string fallback)
        {
            if (document.RootElement.TryGetProperty("message", out JsonElement messageElement))
            {
                string? message = messageElement.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }

            return fallback;
        }

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
                try
                {
                    _sessionLock.Release();
                }
                catch
                {
                }

                _sessionLock.Dispose();
            }
        }
    }
}
