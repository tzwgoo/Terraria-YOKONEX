namespace TerrariaYokonex.Core.Config
{
    public static class YokonexKnownValues
    {
        public const string DefaultWebSocketUrl = "ws://127.0.0.1:43002/v1/events";

        public const string LegacyLocalWebSocketUrl = "ws://103.236.55.92:43001/";

        public static readonly string[] SupportedEventKeys =
        {
            "player_hit_by_npc",
            "player_hit_by_projectile",
            "player_hurt",
            "player_death",
            "player_respawn",
            "item_pickup",
            "day_start",
            "night_start",
            "blood_moon_start",
            "rain_start",
            "rain_stop",
            "slime_rain_start",
            "slime_rain_stop",
            "eclipse_start",
            "eclipse_stop",
            "invasion_start",
            "invasion_complete",
            "boss_spawn",
            "boss_defeat",
        };

        public static string GetEventDisplayName(string eventKey)
        {
            switch ((eventKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "player_hit_by_npc":
                    return "玩家被 NPC 命中";
                case "player_hit_by_projectile":
                    return "玩家被弹幕命中";
                case "player_hurt":
                    return "玩家受伤";
                case "player_death":
                    return "玩家死亡";
                case "player_respawn":
                    return "玩家复活";
                case "item_pickup":
                    return "拾取物品";
                case "day_start":
                    return "白天开始";
                case "night_start":
                    return "夜晚开始";
                case "blood_moon_start":
                    return "血月开始";
                case "rain_start":
                    return "下雨开始";
                case "rain_stop":
                    return "下雨结束";
                case "slime_rain_start":
                    return "史莱姆雨开始";
                case "slime_rain_stop":
                    return "史莱姆雨结束";
                case "eclipse_start":
                    return "日食开始";
                case "eclipse_stop":
                    return "日食结束";
                case "invasion_start":
                    return "入侵开始";
                case "invasion_complete":
                    return "入侵结束";
                case "boss_spawn":
                    return "Boss 出现";
                case "boss_defeat":
                    return "Boss 击败";
                default:
                    return string.IsNullOrWhiteSpace(eventKey) ? "未命名事件" : eventKey.Trim();
            }
        }
    }
}
