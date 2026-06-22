using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Models;
using TerrariaYokonex.Core.Services;

namespace TerrariaYokonex.Systems
{
    public sealed class TerrariaYokonexSystem : ModSystem
    {
        private readonly Dictionary<int, BossSnapshot> _bossSnapshots = new Dictionary<int, BossSnapshot>();
        private bool _worldStateInitialized;
        private bool _lastDayTime;
        private bool _lastBloodMoon;
        private bool _lastRaining;
        private bool _lastSlimeRain;
        private bool _lastEclipse;
        private int _lastInvasionType;

        public override void OnModLoad()
        {
            TerrariaYokonexRuntime.Initialize(Mod);
        }

        public override void OnModUnload()
        {
            TerrariaYokonexRuntime.ShutdownInstance();
        }

        public override void OnWorldLoad()
        {
            ResetWorldState();
        }

        public override void OnWorldUnload()
        {
            ResetWorldState();
        }

        public override void PostUpdateEverything()
        {
            if (Main.gameMenu || Main.dedServ || Main.netMode == NetmodeID.Server)
            {
                return;
            }

            if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
            {
                return;
            }

            if (!_worldStateInitialized)
            {
                InitializeWorldState();
                return;
            }

            // 统一在这一层做世界事件采样，确保昼夜、血月、入侵和 Boss 状态只按本地客户端视角判断一次。
            TrackDayNight();
            TrackBloodMoon();
            TrackRain();
            TrackSlimeRain();
            TrackEclipse();
            TrackInvasions();
            TrackBosses();
        }

        private void InitializeWorldState()
        {
            _worldStateInitialized = true;
            _lastDayTime = Main.dayTime;
            _lastBloodMoon = Main.bloodMoon;
            _lastRaining = Main.raining;
            _lastSlimeRain = Main.slimeRain;
            _lastEclipse = Main.eclipse;
            _lastInvasionType = Main.invasionType;
            _bossSnapshots.Clear();

            for (int index = 0; index < Main.maxNPCs; index++)
            {
                NPC npc = Main.npc[index];
                if (IsTrackableBoss(npc))
                {
                    _bossSnapshots[ResolveBossKey(npc)] = CreateBossSnapshot(npc);
                }
            }
        }

        private void ResetWorldState()
        {
            _worldStateInitialized = false;
            _lastDayTime = false;
            _lastBloodMoon = false;
            _lastRaining = false;
            _lastSlimeRain = false;
            _lastEclipse = false;
            _lastInvasionType = 0;
            _bossSnapshots.Clear();
        }

        private void TrackDayNight()
        {
            if (_lastDayTime != Main.dayTime)
            {
                _lastDayTime = Main.dayTime;
                if (Main.dayTime)
                {
                    Publish(TerrariaEventRecord.Create(
                        "day_start",
                        "白天开始",
                        "day",
                        0,
                        "day",
                        "time"));
                }
                else
                {
                    Publish(TerrariaEventRecord.Create(
                        "night_start",
                        "夜晚开始",
                        "night",
                        0,
                        "night",
                        "time"));
                }
            }
        }

        private void TrackBloodMoon()
        {
            if (!_lastBloodMoon && Main.bloodMoon)
            {
                Publish(TerrariaEventRecord.Create(
                    "blood_moon_start",
                    "血月开始",
                    "blood_moon",
                    0,
                    "blood_moon",
                    "moon"));
            }

            _lastBloodMoon = Main.bloodMoon;
        }

        private void TrackRain()
        {
            if (!_lastRaining && Main.raining)
            {
                Publish(TerrariaEventRecord.Create(
                    "rain_start",
                    "下雨开始",
                    "rain",
                    0,
                    "rain",
                    "weather"));
            }
            else if (_lastRaining && !Main.raining)
            {
                Publish(TerrariaEventRecord.Create(
                    "rain_stop",
                    "下雨结束",
                    "rain",
                    0,
                    "rain",
                    "weather"));
            }

            _lastRaining = Main.raining;
        }

        private void TrackSlimeRain()
        {
            if (!_lastSlimeRain && Main.slimeRain)
            {
                Publish(TerrariaEventRecord.Create(
                    "slime_rain_start",
                    "史莱姆雨开始",
                    "slime_rain",
                    0,
                    "slime_rain",
                    "event"));
            }
            else if (_lastSlimeRain && !Main.slimeRain)
            {
                Publish(TerrariaEventRecord.Create(
                    "slime_rain_stop",
                    "史莱姆雨结束",
                    "slime_rain",
                    0,
                    "slime_rain",
                    "event"));
            }

            _lastSlimeRain = Main.slimeRain;
        }

        private void TrackEclipse()
        {
            if (!_lastEclipse && Main.eclipse)
            {
                Publish(TerrariaEventRecord.Create(
                    "eclipse_start",
                    "日食开始",
                    "eclipse",
                    0,
                    "eclipse",
                    "event"));
            }
            else if (_lastEclipse && !Main.eclipse)
            {
                Publish(TerrariaEventRecord.Create(
                    "eclipse_stop",
                    "日食结束",
                    "eclipse",
                    0,
                    "eclipse",
                    "event"));
            }

            _lastEclipse = Main.eclipse;
        }

        private void TrackInvasions()
        {
            if (_lastInvasionType == 0 && Main.invasionType > 0)
            {
                string invasionTag = "invasion:" + Main.invasionType;
                string invasionName = ResolveInvasionName(Main.invasionType);
                Publish(TerrariaEventRecord.Create(
                    "invasion_start",
                    "入侵开始: " + invasionName,
                    invasionName,
                    Main.invasionType,
                    "invasion",
                    invasionTag,
                    invasionName));
            }
            else if (_lastInvasionType > 0 && Main.invasionType == 0)
            {
                string invasionTag = "invasion:" + _lastInvasionType;
                string invasionName = ResolveInvasionName(_lastInvasionType);
                Publish(TerrariaEventRecord.Create(
                    "invasion_complete",
                    "入侵结束: " + invasionName,
                    invasionName,
                    _lastInvasionType,
                    "invasion",
                    invasionTag,
                    invasionName));
            }

            _lastInvasionType = Main.invasionType;
        }

        private void TrackBosses()
        {
            Dictionary<int, BossSnapshot> currentBosses = new Dictionary<int, BossSnapshot>();
            for (int index = 0; index < Main.maxNPCs; index++)
            {
                NPC npc = Main.npc[index];
                if (!IsTrackableBoss(npc))
                {
                    continue;
                }

                currentBosses[ResolveBossKey(npc)] = CreateBossSnapshot(npc);
            }

            foreach (KeyValuePair<int, BossSnapshot> currentBoss in currentBosses)
            {
                if (_bossSnapshots.ContainsKey(currentBoss.Key))
                {
                    continue;
                }

                Publish(TerrariaEventRecord.Create(
                    "boss_spawn",
                    "Boss 出现: " + currentBoss.Value.DisplayName,
                    currentBoss.Value.DisplayName,
                    currentBoss.Value.Type,
                    "boss",
                    "npc:" + currentBoss.Value.Type,
                    currentBoss.Value.DisplayName));
            }

            _bossSnapshots.Clear();
            foreach (KeyValuePair<int, BossSnapshot> currentBoss in currentBosses)
            {
                _bossSnapshots[currentBoss.Key] = currentBoss.Value;
            }
        }

        private void Publish(TerrariaEventRecord eventRecord)
        {
            TerrariaYokonexRuntime.Instance?.QueueEvent(eventRecord);
        }

        private static bool IsTrackableBoss(NPC npc)
        {
            return npc.active &&
                   npc.boss &&
                   (npc.realLife < 0 || npc.realLife == npc.whoAmI);
        }

        private static int ResolveBossKey(NPC npc)
        {
            return npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
        }

        private static BossSnapshot CreateBossSnapshot(NPC npc)
        {
            return new BossSnapshot
            {
                Type = npc.type,
                DisplayName = Lang.GetNPCNameValue(npc.type),
            };
        }

        private static string ResolveInvasionName(int invasionType)
        {
            switch (invasionType)
            {
                case 1:
                    return Language.GetTextValue("Game.GoblinArmy");
                case 2:
                    return Language.GetTextValue("Game.FrostLegion");
                case 3:
                    return Language.GetTextValue("Game.PirateInvasion");
                case 4:
                    return "火星暴乱";
                default:
                    return "入侵#" + invasionType;
            }
        }

        private sealed class BossSnapshot
        {
            public int Type { get; set; }

            public string DisplayName { get; set; } = "";
        }
    }
}
