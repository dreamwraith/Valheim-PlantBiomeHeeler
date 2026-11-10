using Jotunn.Configs;
using Jotunn.Managers;
using PlantBiomeHeeler.Configuration;

namespace PlantBiomeHeeler.Core.Input
{
    public static class ModInputManager
    {
        public static ButtonConfig ToggleOverlayButton { get; private set; } = null!;

        public static void Initialize()
        {
            ToggleOverlayButton = new ButtonConfig
            {
                Name = "ToggleOverlay",
                ShortcutConfig = ModConfig.ToggleOverlayKey,
                ActiveInGUI = false,
                ActiveInCustomGUI = false,
                BlockOtherInputs = false,
                HintToken = null
            };

            InputManager.Instance.AddButton(Plugin.ModGUID, ToggleOverlayButton);
        }
    }
}
