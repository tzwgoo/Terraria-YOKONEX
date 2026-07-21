# Terraria 役次元 IM 联动

一个基于 `tModLoader 1.4.4 stable` 构建的 Terraria 联动 Mod，用于采集游戏内事件，并通过本机 Yokonex-ModHub 转发 IM 指令和设备波形。

当前版本不直接登录公网 IM，连接、commandId 和波形均由 Yokonex-ModHub 统一管理。

## 项目简介

- Mod 名称：`Terraria 役次元 IM 联动`
- Mod 作者：`辞年`
- 适配版本：`Terraria 1.4.4.9` / `tModLoader 1.4.4 stable`
- 默认 WebSocket 地址：`ws://127.0.0.1:43002/v1/events`

## 当前功能

- 采集 Terraria 游戏内关键事件
- 从 Yokonex-ModHub 同步事件开关和 `commandId`
- 通过本机 WebSocket 发送标准 GameHub 事件
- 提供图形化配置界面管理连接与规则
- 支持运行时手动登录、退出登录和测试事件注入

## 快速开始

1. 将 Mod 放入 tModLoader 并进入游戏世界
2. 使用 `/yokonex config` 打开配置界面
3. 保持 Yokonex-ModHub 运行，并在插件中心启用“泰拉瑞亚”
4. 在 GameHub 中配置事件 commandId 和波形
5. 使用 `/yokonex trigger <eventKey> [matchValue]` 或实际游戏行为验证联动

## 图形化配置界面

- 打开方式：`/yokonex config`
- 快捷键：默认 `未绑定`

当前界面只保留两个页面：

- `连接设置`
  - 全局开关
  - 调试日志
  - 全局冷却
  - WebSocket 地址
  - UserId
  - Token
  - 登录 IM / 退出登录
- `规则配置`
  - 中文事件名称
  - 只读 `command_id`
  - 启用状态

## 支持事件

当前支持以下事件键：

- `player_hit_by_npc`
- `player_hit_by_projectile`
- `player_hurt`
- `player_death`
- `player_respawn`
- `item_pickup`
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
- `boss_spawn`
- `boss_defeat`

事件与 `commandId` 的详细对照表见：

- [docs/event-commandid-reference.md](docs/event-commandid-reference.md)

## 配置文件

Mod 首次启动后会自动生成以下配置文件：

- `Documents\\My Games\\Terraria\\tModLoader\\ModConfigs\\TerrariaYokonex\\settings.json`
- `Documents\\My Games\\Terraria\\tModLoader\\ModConfigs\\TerrariaYokonex\\routes.json`

### settings.json

`settings.json` 用于保存 IM 连接与运行时总控参数。

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

说明：

- 当前版本优先使用 `userId`
- `uid` 作为兼容旧配置的保留字段
- 如果旧配置仍是本地地址 `ws://127.0.0.1:43001/`，加载时会迁移到默认公网地址

### routes.json

`routes.json` 用于保存事件路由规则。

关键字段：

- `id`
- `notes`
- `enabled`
- `eventKey`
- `matchValue`
- `outputMode`
- `commandId`
- `minIntervalMs`

当前版本的规则约束：

- `outputMode` 统一为 `websocket_command`
- `commandId` 与 `eventKey` 保持一致
- 图形化界面中 `commandId` 为只读项

## 游戏内命令

- `/yokonex status`
- `/yokonex reload`
- `/yokonex paths`
- `/yokonex config`
- `/yokonex trigger <eventKey> [matchValue]`

## 迁移说明

- 蓝牙连接与波形触发逻辑已完全移除
- 旧版 `bluetooth_waveform` 规则加载后会自动停用
- 迁移到当前版本后，建议按事件重新检查 IM 规则是否符合预期

## 构建说明

当前仓库已经在本地 `.NET 8 + tModLoader 1.4.4 stable` 环境下完成编译验证。

如果直接在 tModLoader 运行时构建失败，请先关闭 tModLoader，或者在游戏内停用该 Mod 后再重新构建，以避免 `.tmod` 文件被占用。
