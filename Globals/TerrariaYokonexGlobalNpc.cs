using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Models;
using TerrariaYokonex.Core.Networking;
using TerrariaYokonex.Core.Services;

namespace TerrariaYokonex.Globals
{
    public sealed class TerrariaYokonexGlobalNpc : GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            if (!npc.boss)
            {
                return;
            }

            if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
            {
                return;
            }

            TerrariaEventRecord eventRecord = TerrariaEventRecord.Create(
                "boss_defeat",
                "Boss 击败: " + npc.FullName,
                npc.FullName,
                npc.type,
                "boss",
                "npc:" + npc.type,
                npc.FullName);

            // 单机直接本地触发；联机则由服务端广播权威事件，避免各客户端各自猜测击杀时机。
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                TerrariaYokonexRuntime.Instance?.QueueEvent(eventRecord);
                return;
            }

            if (Main.netMode == NetmodeID.Server)
            {
                YokonexNetBroadcaster.BroadcastEvent(Mod, eventRecord);
            }
        }
    }
}
