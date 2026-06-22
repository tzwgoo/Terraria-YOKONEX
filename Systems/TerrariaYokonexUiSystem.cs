using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using TerrariaYokonex.Core.Services;
using TerrariaYokonex.UI;

namespace TerrariaYokonex.Systems
{
    public sealed class TerrariaYokonexUiSystem : ModSystem
    {
        private TerrariaYokonexConfigUiState? _configUiState;

        internal static TerrariaYokonexUiSystem? Instance { get; private set; }

        public override void OnModLoad()
        {
            if (!Main.dedServ)
            {
                Instance = this;
            }
        }

        public override void OnModUnload()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _configUiState = null;
        }

        public void OpenConfigUi()
        {
            if (Main.dedServ || Main.gameMenu)
            {
                return;
            }

            TerrariaYokonexRuntime? runtime = TerrariaYokonexRuntime.Instance;
            if (runtime == null)
            {
                return;
            }

            _configUiState = new TerrariaYokonexConfigUiState(runtime.GetEditableSnapshot());
            _configUiState.Activate();
            IngameFancyUI.OpenUIState(_configUiState);
        }

        public void CloseConfigUi()
        {
            _configUiState = null;
            IngameFancyUI.Close();
        }

        public void ToggleConfigUi()
        {
            if (_configUiState != null)
            {
                CloseConfigUi();
                return;
            }

            OpenConfigUi();
        }
    }
}
