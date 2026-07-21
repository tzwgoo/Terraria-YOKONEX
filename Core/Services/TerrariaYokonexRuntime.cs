using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Config;
using TerrariaYokonex.Core.Models;

namespace TerrariaYokonex.Core.Services
{
    public sealed class TerrariaYokonexRuntime : IDisposable
    {
        private readonly Mod _mod;
        private readonly YokonexConfigStore _configStore;
        private readonly YokonexWebSocketCommandSender _webSocketSender;
        private readonly ConcurrentQueue<TerrariaEventRecord> _eventQueue;
        private readonly SemaphoreSlim _queueSignal;
        private readonly Dictionary<string, DateTimeOffset> _lastRuleDispatchAt;
        private readonly object _snapshotLock;
        private CancellationTokenSource _cancellationTokenSource;
        private Task? _workerTask;
        private YokonexConfigSnapshot _snapshot;
        private DateTimeOffset _lastEventDispatchAt;

        private TerrariaYokonexRuntime(Mod mod)
        {
            _mod = mod;
            _configStore = new YokonexConfigStore(mod);
            _webSocketSender = new YokonexWebSocketCommandSender();
            _eventQueue = new ConcurrentQueue<TerrariaEventRecord>();
            _queueSignal = new SemaphoreSlim(0);
            _lastRuleDispatchAt = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
            _snapshotLock = new object();
            _cancellationTokenSource = new CancellationTokenSource();
            _snapshot = _configStore.LoadSnapshot();
            _lastEventDispatchAt = DateTimeOffset.MinValue;
        }

        public static TerrariaYokonexRuntime? Instance { get; private set; }

        public static TerrariaYokonexRuntime Initialize(Mod mod)
        {
            if (Instance == null)
            {
                Instance = new TerrariaYokonexRuntime(mod);
                Instance.StartWorker();
            }

            return Instance;
        }

        public static void ShutdownInstance()
        {
            if (Instance != null)
            {
                Instance.Dispose();
                Instance = null;
            }
        }

        public string ConfigDirectoryPath => _configStore.ConfigDirectoryPath;

        public string SettingsPath => _configStore.SettingsPath;

        public string RoutesPath => _configStore.RoutesPath;

        public int RuleCount
        {
            get
            {
                lock (_snapshotLock)
                {
                    return _snapshot.Routes.Rules.Count;
                }
            }
        }

        public void ReloadConfig()
        {
            lock (_snapshotLock)
            {
                _snapshot = _configStore.LoadSnapshot();
                _lastRuleDispatchAt.Clear();
                _lastEventDispatchAt = DateTimeOffset.MinValue;
            }
        }

        public YokonexConfigSnapshot GetEditableSnapshot()
        {
            return _configStore.CloneSnapshot(GetSnapshot());
        }

        public void SaveSnapshot(YokonexConfigSnapshot snapshot)
        {
            // 先落盘再切换运行时快照，避免异常时出现“界面已切换但文件未保存”的状态。
            YokonexConfigSnapshot normalizedSnapshot = _configStore.CloneSnapshot(snapshot);
            _configStore.SaveSnapshot(normalizedSnapshot);

            lock (_snapshotLock)
            {
                _snapshot = normalizedSnapshot;
                _lastRuleDispatchAt.Clear();
                _lastEventDispatchAt = DateTimeOffset.MinValue;
            }
        }

        public void QueueEvent(TerrariaEventRecord eventRecord)
        {
            YokonexConfigSnapshot snapshot = GetSnapshot();
            if (!snapshot.Settings.Enabled)
            {
                return;
            }

            _eventQueue.Enqueue(eventRecord);
            _queueSignal.Release();
        }

        public string GetStatusText()
        {
            YokonexConfigSnapshot snapshot = GetSnapshot();
            int enabledRules = 0;
            foreach (YokonexRouteRule rule in snapshot.Routes.Rules)
            {
                if (rule.Enabled)
                {
                    enabledRules++;
                }
            }

            return "Enabled=" + snapshot.Settings.Enabled +
                   ", Rules=" + snapshot.Routes.Rules.Count +
                   ", EnabledRules=" + enabledRules +
                   ", WebSocketEnabled=" + snapshot.Settings.WebSocket.Enabled +
                   ", ConfigDir=" + ConfigDirectoryPath;
        }

        public async Task<YokonexDispatchResult> LoginWebSocketAsync(CancellationToken cancellationToken)
        {
            return await _webSocketSender.LoginAsync(GetSnapshot().Settings.WebSocket, cancellationToken);
        }

        public async Task<YokonexDispatchResult> LogoutWebSocketAsync(CancellationToken cancellationToken)
        {
            return await _webSocketSender.LogoutAsync(cancellationToken);
        }

        private void StartWorker()
        {
            _workerTask = Task.Run(() => WorkerLoopAsync(_cancellationTokenSource.Token));
        }

        private async Task WorkerLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _queueSignal.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (_eventQueue.TryDequeue(out TerrariaEventRecord? eventRecord))
                {
                    await DispatchEventAsync(eventRecord, cancellationToken);
                }
            }
        }

        private async Task DispatchEventAsync(TerrariaEventRecord eventRecord, CancellationToken cancellationToken)
        {
            YokonexConfigSnapshot snapshot = GetSnapshot();
            if (!snapshot.Settings.Enabled)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (snapshot.Settings.GlobalCooldownMs > 0 &&
                _lastEventDispatchAt != DateTimeOffset.MinValue &&
                (now - _lastEventDispatchAt).TotalMilliseconds < snapshot.Settings.GlobalCooldownMs)
            {
                DebugLog("事件被全局冷却忽略: " + eventRecord.EventKey);
                return;
            }

            List<YokonexRouteRule> matchedRules = MatchRules(snapshot.Routes, eventRecord);
            if (matchedRules.Count == 0)
            {
                DebugLog("没有命中任何规则: " + eventRecord.EventKey);
                return;
            }

            bool anySuccess = false;

            // 事件采集和 IM 下发通过队列解耦，避免在主线程里直接等待网络请求造成卡顿。
            foreach (YokonexRouteRule rule in matchedRules)
            {
                if (IsRuleCoolingDown(rule, now))
                {
                    DebugLog("规则冷却中，已跳过: " + rule.Id);
                    continue;
                }

                YokonexDispatchResult result = await DispatchRuleAsync(snapshot.Settings, rule, eventRecord, cancellationToken);
                if (result.Success)
                {
                    anySuccess = true;
                    _lastRuleDispatchAt[rule.Id] = now;
                    _mod.Logger.Info("YOKONEX 触发成功: " + eventRecord.EventKey + " -> " + rule.OutputMode + " / " + result.Message);
                }
                else
                {
                    _mod.Logger.Warn("YOKONEX 触发失败: " + eventRecord.EventKey + " -> " + rule.OutputMode + " / " + result.Message);
                }
            }

            if (anySuccess)
            {
                _lastEventDispatchAt = now;
            }
        }

        private static List<YokonexRouteRule> MatchRules(YokonexRouteConfig config, TerrariaEventRecord eventRecord)
        {
            List<YokonexRouteRule> matched = new List<YokonexRouteRule>();

            foreach (YokonexRouteRule rule in config.Rules)
            {
                if (!rule.Enabled)
                {
                    continue;
                }

                if (!string.Equals(rule.EventKey?.Trim(), eventRecord.EventKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!eventRecord.Matches(rule.MatchValue))
                {
                    continue;
                }

                matched.Add(rule);
            }

            return matched;
        }

        private bool IsRuleCoolingDown(YokonexRouteRule rule, DateTimeOffset now)
        {
            if (rule.CommandId.Length == 0 || rule.MinIntervalMs <= 0)
            {
                return false;
            }

            if (!_lastRuleDispatchAt.TryGetValue(rule.Id, out DateTimeOffset lastDispatchAt))
            {
                return false;
            }

            return (now - lastDispatchAt).TotalMilliseconds < rule.MinIntervalMs;
        }

        private async Task<YokonexDispatchResult> DispatchRuleAsync(
            YokonexRuntimeSettings settings,
            YokonexRouteRule rule,
            TerrariaEventRecord eventRecord,
            CancellationToken cancellationToken)
        {
            if (string.Equals(rule.OutputMode, YokonexOutputModes.WebSocketCommand, StringComparison.OrdinalIgnoreCase))
            {
                return await _webSocketSender.SendCommandAsync(settings.WebSocket, rule, eventRecord, cancellationToken);
            }

            return new YokonexDispatchResult
            {
                Success = false,
                Message = "当前版本仅保留 IM 输出模式: " + rule.OutputMode,
            };
        }

        private YokonexConfigSnapshot GetSnapshot()
        {
            lock (_snapshotLock)
            {
                return _snapshot;
            }
        }

        private void DebugLog(string message)
        {
            YokonexConfigSnapshot snapshot = GetSnapshot();
            if (snapshot.Settings.DebugLogging)
            {
                _mod.Logger.Info("[Debug] " + message);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _queueSignal.Release();

            try
            {
                _workerTask?.Wait(2000);
            }
            catch
            {
            }

            _cancellationTokenSource.Dispose();
            _queueSignal.Dispose();
            _webSocketSender.Dispose();
        }
    }
}
