using Terraria;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Models;
using TerrariaYokonex.Core.Services;

namespace TerrariaYokonex.Globals
{
    public sealed class TerrariaYokonexGlobalItem : GlobalItem
    {
        public override bool OnPickup(Item item, Player player)
        {
            if (player.whoAmI == Main.myPlayer)
            {
                // 拾取事件通常非常频繁，所以只采集最关键的信息，把节流交给规则层处理。
                TerrariaYokonexRuntime.Instance?.QueueEvent(
                    TerrariaEventRecord.Create(
                        "item_pickup",
                        "拾取物品: " + item.Name + " x" + item.stack,
                        item.Name,
                        item.stack,
                        "item",
                        "item:" + item.type,
                        item.Name));
            }

            return true;
        }
    }
}
