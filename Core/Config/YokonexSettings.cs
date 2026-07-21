using System;
using System.Collections.Generic;

namespace TerrariaYokonex.Core.Config
{
    public static class YokonexOutputModes
    {
        public const string WebSocketCommand = "websocket_command";
    }

    public sealed class YokonexRuntimeSettings
    {
        public bool Enabled { get; set; } = true;

        public bool DebugLogging { get; set; } = true;

        public int GlobalCooldownMs { get; set; } = 200;

        public YokonexWebSocketSettings WebSocket { get; set; } = new YokonexWebSocketSettings();
    }

    public sealed class YokonexWebSocketSettings
    {
        public bool Enabled { get; set; } = true;

        public string WsUrl { get; set; } = YokonexKnownValues.DefaultWebSocketUrl;

        public string Uid { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public int ConnectTimeoutMs { get; set; } = 5000;

        public int ReceiveTimeoutMs { get; set; } = 5000;

        public string ResolveUserId()
        {
            string normalized = (UserId ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            normalized = ResolveUid();
            if (normalized.StartsWith("game_", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(5);
            }

            return normalized;
        }

        public string ResolveUid()
        {
            string normalized = (Uid ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            normalized = (UserId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return normalized.StartsWith("game_", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : "game_" + normalized;
        }
    }

    public sealed class YokonexRouteConfig
    {
        public List<YokonexRouteRule> Rules { get; set; } = new List<YokonexRouteRule>();
    }

    public sealed class YokonexRouteRule
    {
        public string Id { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public string EventKey { get; set; } = string.Empty;

        public string MatchValue { get; set; } = string.Empty;

        public string OutputMode { get; set; } = YokonexOutputModes.WebSocketCommand;

        public string CommandId { get; set; } = string.Empty;

        public int MinIntervalMs { get; set; } = 0;
    }

    public sealed class YokonexConfigSnapshot
    {
        public YokonexRuntimeSettings Settings { get; set; } = new YokonexRuntimeSettings();

        public YokonexRouteConfig Routes { get; set; } = new YokonexRouteConfig();
    }
}
