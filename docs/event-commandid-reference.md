# Terraria YOKONEX IM 事件与 Command ID 对照

这份文档用于说明当前模组支持的事件，以及每个事件对应的 `commandId`。

## 规则说明

- 当前版本中，`commandId` 与事件键 `eventKey` 保持一致。
- 配置页面中的 `Command ID` 为只读展示项，不建议手动改写。
- 如果新增事件，建议同步更新代码中的 `SupportedEventKeys` 与本文件。

## 事件对照表

| 中文事件名称 | eventKey | commandId | 说明 |
| --- | --- | --- | --- |
| 玩家被 NPC 命中 | `player_hit_by_npc` | `player_hit_by_npc` | 玩家被 NPC 直接攻击命中时触发 |
| 玩家被弹幕命中 | `player_hit_by_projectile` | `player_hit_by_projectile` | 玩家被投射物或弹幕命中时触发 |
| 玩家受伤 | `player_hurt` | `player_hurt` | 玩家受到伤害时触发 |
| 玩家死亡 | `player_death` | `player_death` | 玩家死亡时触发 |
| 玩家复活 | `player_respawn` | `player_respawn` | 玩家复活时触发 |
| 拾取物品 | `item_pickup` | `item_pickup` | 玩家拾取物品时触发 |
| 白天开始 | `day_start` | `day_start` | 游戏进入白天时触发 |
| 夜晚开始 | `night_start` | `night_start` | 游戏进入夜晚时触发 |
| 血月开始 | `blood_moon_start` | `blood_moon_start` | 血月开始时触发 |
| 下雨开始 | `rain_start` | `rain_start` | 开始下雨时触发 |
| 下雨结束 | `rain_stop` | `rain_stop` | 雨停时触发 |
| 史莱姆雨开始 | `slime_rain_start` | `slime_rain_start` | 史莱姆雨开始时触发 |
| 史莱姆雨结束 | `slime_rain_stop` | `slime_rain_stop` | 史莱姆雨结束时触发 |
| 日食开始 | `eclipse_start` | `eclipse_start` | 日食开始时触发 |
| 日食结束 | `eclipse_stop` | `eclipse_stop` | 日食结束时触发 |
| 入侵开始 | `invasion_start` | `invasion_start` | 哥布林军团等入侵事件开始时触发 |
| 入侵结束 | `invasion_complete` | `invasion_complete` | 入侵事件结束时触发 |
| Boss 出现 | `boss_spawn` | `boss_spawn` | Boss 出现时触发 |
| Boss 击败 | `boss_defeat` | `boss_defeat` | Boss 被击败时触发 |

## 给 IM 侧的建议

- 如果 IM 设备侧需要做事件区分，直接按 `commandId` 判断即可。
- 推荐将 `commandId` 作为唯一事件标识，不再依赖中文名称。
- 中文名称更适合用于配置界面展示或文档说明。
