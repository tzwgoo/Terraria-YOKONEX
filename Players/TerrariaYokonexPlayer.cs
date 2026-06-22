using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Models;
using TerrariaYokonex.Core.Services;
using TerrariaYokonex.Systems;

namespace TerrariaYokonex.Players
{
    public sealed class TerrariaYokonexPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Player.whoAmI != Main.myPlayer || Main.gameMenu)
            {
                return;
            }

            if (TerrariaYokonex.OpenConfigHotkey?.JustPressed == true)
            {
                TerrariaYokonexUiSystem.Instance?.ToggleConfigUi();
            }
        }

        public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo)
        {
            if (Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            TerrariaYokonexRuntime.Instance?.QueueEvent(
                TerrariaEventRecord.Create(
                    "player_hit_by_npc",
                    "玩家被 NPC 命中: " + npc.FullName,
                    npc.FullName,
                    hurtInfo.Damage,
                    "npc",
                    "hit",
                    "npc:" + npc.type,
                    npc.FullName));
        }

        public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo)
        {
            if (Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            TerrariaYokonexRuntime.Instance?.QueueEvent(
                TerrariaEventRecord.Create(
                    "player_hit_by_projectile",
                    "玩家被弹幕命中: " + proj.Name,
                    proj.Name,
                    hurtInfo.Damage,
                    "projectile",
                    "hit",
                    "projectile:" + proj.type,
                    proj.Name));
        }

        public override void PostHurt(Player.HurtInfo info)
        {
            if (Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            TerrariaYokonexRuntime.Instance?.QueueEvent(
                TerrariaEventRecord.Create(
                    "player_hurt",
                    "玩家受伤: " + info.Damage + " 点",
                    "player",
                    info.Damage,
                    "player",
                    "hurt",
                    "damage:" + info.Damage));
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            if (Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            TerrariaYokonexRuntime.Instance?.QueueEvent(
                TerrariaEventRecord.Create(
                    "player_death",
                    "玩家死亡",
                    "player",
                    (int)damage,
                    "player",
                    "death"));
        }

        public override void OnRespawn()
        {
            if (Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            TerrariaYokonexRuntime.Instance?.QueueEvent(
                TerrariaEventRecord.Create(
                    "player_respawn",
                    "玩家复活",
                    "player",
                    0,
                    "player",
                    "respawn"));
        }
    }
}
