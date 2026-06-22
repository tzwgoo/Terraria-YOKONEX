# Steam Workshop 发布清单

## 发布前检查

- 确认 `build.txt` 中版本号已更新
- 确认 `description.txt` 与 Workshop 页面文案一致
- 确认 `settings.json` / `routes.json` 默认值适合首次使用
- 确认 `README.md` 中事件列表与当前代码一致
- 确认 `Bililive-YOKONEX` 侧接口地址与协议没有变更

## 建议发布流程

1. 用 `tModLoader` 正式版启动游戏
2. 在 `Develop Mods` 中重新构建一次
3. 本地启用 Mod 做一轮联动回归
4. 确认生成的 `.tmod` 包可正常加载
5. 在 `Workshop` 页面填写标题、描述、更新说明
6. 首次发布建议标记为测试版说明，方便后续快速迭代

## 首次发布建议写清楚

- 这是一个需要配合 `Bililive-YOKONEX` 使用的联动 Mod
- 仅安装本 Mod 不会直接控制设备
- 需要本地运行 `Bililive-YOKONEX`
- 当前多人联动事件同步只覆盖部分关键事件

## 每次更新建议写进更新日志

- 新增了哪些 Terraria 事件
- 是否修改了配置文件结构
- 是否需要用户重新生成默认配置
- 是否修复了多人模式或触发时序问题
