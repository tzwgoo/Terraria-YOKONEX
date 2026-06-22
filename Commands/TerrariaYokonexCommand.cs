using Terraria;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Models;
using TerrariaYokonex.Core.Services;
using TerrariaYokonex.Systems;

namespace TerrariaYokonex.Commands
{
    public sealed class TerrariaYokonexCommand : ModCommand
    {
        public override string Command => "yokonex";

        public override CommandType Type => CommandType.Chat;

        public override string Usage => "/yokonex <status|reload|paths|config|trigger>";

        public override string Description => "查看或重载 Terraria YOKONEX 配置";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                caller.Reply("YOKONEX 运行时尚未初始化");
                return;
            }

            if (args.Length == 0)
            {
                caller.Reply("用法: " + Usage);
                return;
            }

            switch (args[0])
            {
                case "status":
                    caller.Reply(runtime.GetStatusText());
                    break;
                case "reload":
                    runtime.ReloadConfig();
                    caller.Reply("YOKONEX 配置已重新加载");
                    break;
                case "paths":
                    caller.Reply("ConfigDir: " + runtime.ConfigDirectoryPath);
                    caller.Reply("Settings: " + runtime.SettingsPath);
                    caller.Reply("Routes: " + runtime.RoutesPath);
                    break;
                case "config":
                    if (Main.gameMenu)
                    {
                        caller.Reply("请先进入世界，再打开 YOKONEX 图形化配置界面");
                        break;
                    }

                    TerrariaYokonexUiSystem.Instance?.OpenConfigUi();
                    caller.Reply("YOKONEX 图形化配置界面已打开");
                    break;
                case "trigger":
                    if (args.Length < 2)
                    {
                        throw new UsageException("用法: /yokonex trigger <eventKey> [matchValue]");
                    }

                    string eventKey = args[1];
                    string matchValue = args.Length >= 3
                        ? string.Join(" ", args, 2, args.Length - 2)
                        : string.Empty;

                    // 手动注入测试事件，方便在不依赖战斗流程的情况下验证 IM 路由是否命中。
                    runtime.QueueEvent(
                        TerrariaEventRecord.Create(
                            eventKey,
                            "手动触发: " + eventKey,
                            matchValue,
                            0,
                            matchValue));
                    caller.Reply("已加入测试事件队列: " + eventKey);
                    break;
                default:
                    caller.Reply("未知子命令，支持: status / reload / paths / config / trigger");
                    break;
            }
        }
    }
}
