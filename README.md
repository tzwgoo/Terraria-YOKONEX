# Terraria 役次元 IM 联动

一个面向 `tModLoader 1.4.4 stable` 的 Terraria 与役次元联动 Mod。

当前版本只保留 `IM / WebSocket` 输出链路，负责采集游戏内事件，并将事件转发为发送给役次元的 IM 指令。

## 当前支持事件

- `player_hurt`
- `player_death`
- `player_respawn`
- `player_hit_by_npc`
- `player_hit_by_projectile`
- `item_pickup`
- `boss_spawn`
- `boss_defeat`
- `day_start`
- `night_start`
- `blood_moon_start`
- `rain_start`
- `rain_stop`
- `slime_rain_start`
- `slime_rain_stop`
- `eclipse_start`
- `eclipse_stop`
- `invasion_start`
- `invasion_complete`

## 配置文件位置

Mod 首次启动后会自动生成两个文件：

- `Documents\\My Games\\Terraria\\tModLoader\\ModConfigs\\TerrariaYokonex\\settings.json`
- `Documents\\My Games\\Terraria\\tModLoader\\ModConfigs\\TerrariaYokonex\\routes.json`

## 图形化配置界面

- 快捷键：默认 `F10`
- 命令：`/yokonex config`

当前界面包含：

- 基础设置页：全局开关、调试日志、全局冷却、WebSocket 参数
- 规则路由页：新增 / 复制 / 删除规则，编辑 `eventKey / matchValue / commandId / minIntervalMs`
- 工具面板：发送测试事件、查看运行状态、查看支持事件和配置路径

## settings.json 说明

`settings.json` 负责全局开关和役次元 IM 链路的连接参数。

当前 `webSocket.wsUrl` 默认值为：

- `ws://103.236.55.92:43001/`

关键字段：

- `enabled`
- `debugLogging`
- `globalCooldownMs`
- `webSocket.enabled`
- `webSocket.wsUrl`
- `webSocket.uid`
- `webSocket.token`
- `webSocket.userId`
- `webSocket.connectTimeoutMs`
- `webSocket.receiveTimeoutMs`

## routes.json 说明

`routes.json` 负责“什么 Terraria 事件映射到什么役次元 IM 指令”。

每条规则支持：

- `id`
- `notes`
- `enabled`
- `eventKey`
- `matchValue`
- `outputMode`
- `commandId`
- `minIntervalMs`

### 规则示例

```json
{
  "id": "player-hurt-im",
  "notes": "玩家受伤后发送 WebSocket 指令",
  "enabled": true,
  "eventKey": "player_hurt",
  "matchValue": "",
  "outputMode": "websocket_command",
  "commandId": "player_hurt",
  "minIntervalMs": 1500
}
```

`matchValue` 会和事件的候选关键字做不区分大小写的包含匹配。

例如 `boss_spawn` 事件里会内置：

- Boss 显示名
- `npc:BossType`
- `boss`

## 役次元 IM 模式使用流程

1. 准备可与役次元联动的 IM / WebSocket 下游服务
2. 在 `settings.json` 中填写 `wsUrl / uid / token / userId`
3. 在 `routes.json` 中配置 `outputMode = websocket_command`
4. 为每条规则填写 `commandId`
5. 进入世界后使用工具面板或 `/yokonex trigger` 验证链路

## 游戏内命令

- `/yokonex status`
- `/yokonex reload`
- `/yokonex paths`
- `/yokonex config`
- `/yokonex trigger <eventKey> [matchValue]`

## 迁移说明

- 旧版蓝牙相关实现已从 Mod 中移除
- 旧配置里 `outputMode = bluetooth_waveform` 的规则会在加载时自动停用
- 迁移到当前版本后，需要手动把原本的波形语义改写成对应的 `commandId`

## 开发说明

这个仓库当前采用 `tModLoader 1.4.4 stable` 的 Hook 设计。

当前仓库已经在本地 `.NET 8` + `tModLoader 1.4.4 stable` 环境下完成构建验证。
