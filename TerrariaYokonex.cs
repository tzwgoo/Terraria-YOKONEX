using System.IO;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using TerrariaYokonex.Core.Models;
using TerrariaYokonex.Core.Networking;
using TerrariaYokonex.Core.Services;

namespace TerrariaYokonex
{
    public sealed class TerrariaYokonex : Mod
    {
        internal static ModKeybind? OpenConfigHotkey { get; private set; }

        public override void Load()
        {
            if (!Main.dedServ)
            {
                // 默认不预设快捷键，避免和其他 Mod、录屏工具或覆盖层发生按键冲突。
                // 用户可以在 tModLoader 的按键设置里手动绑定自己习惯的键位。
                OpenConfigHotkey = KeybindLoader.RegisterKeybind(this, "Open YOKONEX Config", Keys.None);
            }
        }

        public override void Unload()
        {
            OpenConfigHotkey = null;
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            YokonexMessageType messageType = (YokonexMessageType)reader.ReadByte();
            switch (messageType)
            {
                case YokonexMessageType.SyncEvent:
                    TerrariaEventRecord eventRecord = YokonexPacketSerializer.ReadEvent(reader);
                    // 联机时由服务端下发权威事件，客户端收到后直接进入本地路由队列。
                    TerrariaYokonexRuntime.Instance?.QueueEvent(eventRecord);
                    break;
            }
        }
    }
}
