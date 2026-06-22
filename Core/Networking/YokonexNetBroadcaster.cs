using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Models;

namespace TerrariaYokonex.Core.Networking
{
    public static class YokonexNetBroadcaster
    {
        public static void BroadcastEvent(Mod mod, TerrariaEventRecord eventRecord)
        {
            if (Main.netMode != NetmodeID.Server)
            {
                return;
            }

            ModPacket packet = mod.GetPacket();
            packet.Write((byte)YokonexMessageType.SyncEvent);
            YokonexPacketSerializer.WriteEvent(packet, eventRecord);
            packet.Send();
        }
    }
}
