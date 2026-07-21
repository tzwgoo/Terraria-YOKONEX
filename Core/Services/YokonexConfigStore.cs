using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Terraria;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Config;

namespace TerrariaYokonex.Core.Services
{
    public sealed class YokonexConfigStore
    {
        private readonly Mod _mod;
        private readonly JsonSerializerOptions _jsonOptions;

        public YokonexConfigStore(Mod mod)
        {
            _mod = mod;
            ConfigDirectoryPath = Path.Combine(Main.SavePath, "ModConfigs", "TerrariaYokonex");
            SettingsPath = Path.Combine(ConfigDirectoryPath, "settings.json");
            RoutesPath = Path.Combine(ConfigDirectoryPath, "routes.json");
            _jsonOptions = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                WriteIndented = true,
            };
        }

        public string ConfigDirectoryPath { get; }

        public string SettingsPath { get; }

        public string RoutesPath { get; }

        public YokonexConfigSnapshot LoadSnapshot()
        {
            EnsureFilesExist();

            return NormalizeSnapshot(new YokonexConfigSnapshot
            {
                Settings = ReadOrDefault(SettingsPath, CreateDefaultSettings),
                Routes = ReadOrDefault(RoutesPath, CreateDefaultRoutes),
            });
        }

        public void SaveSnapshot(YokonexConfigSnapshot snapshot)
        {
            EnsureFilesExist();

            YokonexConfigSnapshot normalizedSnapshot = NormalizeSnapshot(snapshot);
            WriteJson(SettingsPath, normalizedSnapshot.Settings);
            WriteJson(RoutesPath, normalizedSnapshot.Routes);
        }

        public YokonexConfigSnapshot CloneSnapshot(YokonexConfigSnapshot snapshot)
        {
            return DeepClone(NormalizeSnapshot(snapshot), CreateDefaultSnapshot);
        }

        public void EnsureFilesExist()
        {
            Directory.CreateDirectory(ConfigDirectoryPath);

            if (!File.Exists(SettingsPath))
            {
                WriteJson(SettingsPath, CreateDefaultSettings());
            }

            if (!File.Exists(RoutesPath))
            {
                WriteJson(RoutesPath, CreateDefaultRoutes());
            }
        }

        public YokonexConfigSnapshot NormalizeSnapshot(YokonexConfigSnapshot snapshot)
        {
            YokonexRuntimeSettings settings = snapshot?.Settings ?? CreateDefaultSettings();
            YokonexRouteConfig routes = snapshot?.Routes ?? CreateDefaultRoutes();

            settings.WebSocket ??= new YokonexWebSocketSettings();
            routes.Rules ??= new List<YokonexRouteRule>();

            // GameHub 统一负责 IM 登录和设备输出，Mod 只允许连接本机事件入口。
            settings.WebSocket.Enabled = true;
            settings.WebSocket.WsUrl = YokonexKnownValues.DefaultWebSocketUrl;

            settings.WebSocket.Uid = NormalizeString(settings.WebSocket.Uid);
            settings.WebSocket.Token = NormalizeString(settings.WebSocket.Token);
            settings.WebSocket.UserId = NormalizeString(settings.WebSocket.UserId);
            routes.Rules = NormalizeRules(routes.Rules);

            settings.GlobalCooldownMs = Math.Max(0, settings.GlobalCooldownMs);
            settings.WebSocket.ConnectTimeoutMs = Math.Max(0, settings.WebSocket.ConnectTimeoutMs);
            settings.WebSocket.ReceiveTimeoutMs = Math.Max(0, settings.WebSocket.ReceiveTimeoutMs);

            return new YokonexConfigSnapshot
            {
                Settings = settings,
                Routes = routes,
            };
        }

        private T ReadOrDefault<T>(string path, Func<T> defaultFactory) where T : class
        {
            try
            {
                string raw = File.ReadAllText(path);
                T? value = JsonSerializer.Deserialize<T>(raw, _jsonOptions);
                return value ?? defaultFactory();
            }
            catch (Exception ex)
            {
                _mod.Logger.Error("读取 YOKONEX 配置失败，已回退到默认配置。", ex);
                return defaultFactory();
            }
        }

        private void WriteJson<T>(string path, T value)
        {
            string raw = JsonSerializer.Serialize(value, _jsonOptions);
            File.WriteAllText(path, raw);
        }

        private T DeepClone<T>(T value, Func<T> defaultFactory) where T : class
        {
            try
            {
                string raw = JsonSerializer.Serialize(value, _jsonOptions);
                T? cloned = JsonSerializer.Deserialize<T>(raw, _jsonOptions);
                return cloned ?? defaultFactory();
            }
            catch (Exception ex)
            {
                _mod.Logger.Error("复制 YOKONEX 配置快照失败，已回退到默认配置。", ex);
                return defaultFactory();
            }
        }

        private static YokonexConfigSnapshot CreateDefaultSnapshot()
        {
            return new YokonexConfigSnapshot
            {
                Settings = CreateDefaultSettings(),
                Routes = CreateDefaultRoutes(),
            };
        }

        private static YokonexRuntimeSettings CreateDefaultSettings()
        {
            return new YokonexRuntimeSettings
            {
                Enabled = true,
                DebugLogging = true,
                GlobalCooldownMs = 200,
                WebSocket = new YokonexWebSocketSettings
                {
                    Enabled = true,
                    WsUrl = YokonexKnownValues.DefaultWebSocketUrl,
                    Uid = string.Empty,
                    Token = string.Empty,
                    UserId = string.Empty,
                    ConnectTimeoutMs = 5000,
                    ReceiveTimeoutMs = 5000,
                },
            };
        }

        private static YokonexRouteConfig CreateDefaultRoutes()
        {
            return new YokonexRouteConfig
            {
                Rules = NormalizeRules(new List<YokonexRouteRule>
                {
                    new YokonexRouteRule
                    {
                        EventKey = "player_hurt",
                        Enabled = true,
                        MinIntervalMs = 1500,
                    },
                    new YokonexRouteRule
                    {
                        EventKey = "boss_defeat",
                        Enabled = false,
                        MinIntervalMs = 2000,
                    },
                    new YokonexRouteRule
                    {
                        EventKey = "player_respawn",
                        Enabled = false,
                        MinIntervalMs = 1000,
                    },
                }),
            };
        }

        private static List<YokonexRouteRule> NormalizeRules(IReadOnlyList<YokonexRouteRule> sourceRules)
        {
            List<YokonexRouteRule> normalizedRules = new List<YokonexRouteRule>();

            // 规则页现在是“固定事件 -> 固定 command_id”的映射面板，
            // 这里统一按受支持事件生成标准规则，只保留启用状态和少量运行参数迁移。
            foreach (string eventKey in YokonexKnownValues.SupportedEventKeys)
            {
                YokonexRouteRule? existingRule = FindRule(sourceRules, eventKey);
                int minIntervalMs = existingRule?.MinIntervalMs ?? GetDefaultMinIntervalMs(eventKey);
                if (minIntervalMs < 0)
                {
                    minIntervalMs = 0;
                }

                normalizedRules.Add(new YokonexRouteRule
                {
                    Id = eventKey + "-im",
                    Notes = YokonexKnownValues.GetEventDisplayName(eventKey),
                    // 单事件开关由 GameHub 管理，Mod 侧始终采集完整事件集。
                    Enabled = true,
                    EventKey = eventKey,
                    MatchValue = string.Empty,
                    OutputMode = YokonexOutputModes.WebSocketCommand,
                    CommandId = eventKey,
                    MinIntervalMs = minIntervalMs,
                });
            }

            return normalizedRules;
        }

        private static YokonexRouteRule? FindRule(IReadOnlyList<YokonexRouteRule> sourceRules, string eventKey)
        {
            if (sourceRules == null)
            {
                return null;
            }

            for (int index = 0; index < sourceRules.Count; index++)
            {
                YokonexRouteRule? rule = sourceRules[index];
                if (rule == null)
                {
                    continue;
                }

                if (string.Equals(rule.EventKey?.Trim(), eventKey, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }
            }

            return null;
        }

        private static int GetDefaultMinIntervalMs(string eventKey)
        {
            switch (eventKey)
            {
                case "player_hurt":
                    return 1500;
                case "boss_spawn":
                case "boss_defeat":
                case "blood_moon_start":
                case "invasion_start":
                case "invasion_complete":
                    return 5000;
                case "player_respawn":
                    return 1000;
                default:
                    return 0;
            }
        }

        private static string NormalizeOutputMode(string outputMode)
        {
            string normalized = NormalizeString(outputMode, YokonexOutputModes.WebSocketCommand);
            return string.Equals(normalized, YokonexOutputModes.WebSocketCommand, StringComparison.OrdinalIgnoreCase)
                ? YokonexOutputModes.WebSocketCommand
                : YokonexOutputModes.WebSocketCommand;
        }

        private static string NormalizeString(string value, string fallback = "")
        {
            string normalized = (value ?? string.Empty).Trim();
            return normalized.Length == 0 ? fallback : normalized;
        }
    }
}
