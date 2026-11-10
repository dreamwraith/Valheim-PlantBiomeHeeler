using System;
using BepInEx.Configuration;
using Jotunn.Extensions;
using UnityEngine;

namespace PlantBiomeHeeler.Configuration
{
    public static class ModConfig
    {
        private const string GeneralSection = "General";
        private const string OverlaySection = "Overlay";
        private const string BiomeColorsSection = "Biome Colors";
        private const string HudSection = "HUD";
        private const string KeybindingsSection = "Keybindings";

        private static ConfigFile _configFile = null!;

        public static ConfigEntry<bool> EnableMod { get; private set; } = null!;
        public static ConfigEntry<bool> EnableOverlay { get; private set; } = null!;
        public static ConfigEntry<bool> ShowOverlayWithCultivator { get; private set; } = null!;
        public static ConfigEntry<HudDisplayMode> HudMode { get; private set; } = null!;
        public static ConfigEntry<ModLogLevel> LoggingLevel { get; private set; } = null!;

        public static ConfigEntry<float> OverlayRadius { get; private set; } = null!;
        public static ConfigEntry<float> OverlayHeight { get; private set; } = null!;
        public static ConfigEntry<float> InteriorOpacity { get; private set; } = null!;
        public static ConfigEntry<float> BorderOpacity { get; private set; } = null!;
        public static ConfigEntry<float> BorderWidth { get; private set; } = null!;
        public static ConfigEntry<bool> EnableHatchLines { get; private set; } = null!;
        public static ConfigEntry<float> HatchLineSpacing { get; private set; } = null!;
        public static ConfigEntry<float> HatchLineWidth { get; private set; } = null!;

        public static ConfigEntry<Color> MeadowsColor { get; private set; } = null!;
        public static ConfigEntry<Color> BlackForestColor { get; private set; } = null!;
        public static ConfigEntry<Color> SwampColor { get; private set; } = null!;
        public static ConfigEntry<Color> MountainColor { get; private set; } = null!;
        public static ConfigEntry<Color> PlainsColor { get; private set; } = null!;
        public static ConfigEntry<Color> MistlandsColor { get; private set; } = null!;
        public static ConfigEntry<Color> AshLandsColor { get; private set; } = null!;
        public static ConfigEntry<Color> DeepNorthColor { get; private set; } = null!;
        public static ConfigEntry<Color> OceanColor { get; private set; } = null!;

        public static ConfigEntry<float> HudOffsetX { get; private set; } = null!;
        public static ConfigEntry<float> HudOffsetY { get; private set; } = null!;
        public static ConfigEntry<int> HudFontSize { get; private set; } = null!;
        public static ConfigEntry<Color> HudBackgroundColor { get; private set; } = null!;
        public static ConfigEntry<float> HudUpdateInterval { get; private set; } = null!;
        public static ConfigEntry<float> TargetRayDistance { get; private set; } = null!;

        public static ConfigEntry<KeyboardShortcut> ToggleOverlayKey { get; private set; } = null!;

        public static bool IsInfoLogging => LoggingLevel.Value >= ModLogLevel.Info;
        public static bool IsDebugLogging => LoggingLevel.Value >= ModLogLevel.Debug;

        // handlers go straight onto the config file event, so they hear every setting in this file
        public static event EventHandler<SettingChangedEventArgs> SettingChanged
        {
            add => _configFile.SettingChanged += value;
            remove => _configFile.SettingChanged -= value;
        }

        public static void Initialize(ConfigFile config)
        {
            _configFile = config;

            EnableMod = Bind(config, GeneralSection, "EnableMod", true,
                "Enables or disables PlantBiomeHeeler.");

            EnableOverlay = Bind(config, GeneralSection, "EnableOverlay", true,
                "Draws the plant biome overlay on the terrain around the player.");

            ShowOverlayWithCultivator = Bind(config, GeneralSection, "ShowOverlayWithCultivator", true,
                "Shows the overlay automatically while the cultivator is equipped. The toggle key works with or without the cultivator.");

            HudMode = Bind(config, GeneralSection, "HudMode", HudDisplayMode.WithOverlay,
                "Sets when the biome HUD is visible (Always = at all times, WithOverlay = while the overlay is visible, Disabled = never).");

            LoggingLevel = Bind(config, GeneralSection, "LoggingLevel", ModLogLevel.Info,
                "BepInEx log detail level (None = silent, Info = startup, Debug = overlay rebuild timing).");

            OverlayRadius = Bind(config, OverlaySection, "OverlayRadius", 48.0f,
                "Display radius in meters around the player for the biome overlay.",
                new AcceptableValueRange<float>(16.0f, 96.0f));

            OverlayHeight = Bind(config, OverlaySection, "OverlayHeight", 0.5f,
                "Height in meters of the overlay above the ground, or above the water surface where the ground is under water.",
                new AcceptableValueRange<float>(0.05f, 2.0f));

            InteriorOpacity = Bind(config, OverlaySection, "InteriorOpacity", 1.0f,
                "Multiplier for the opacity of the colored area inside every biome. 0 hides these areas and keeps the borders and hatch lines.",
                new AcceptableValueRange<float>(0.0f, 1.0f));

            BorderOpacity = Bind(config, OverlaySection, "BorderOpacity", 1.0f,
                "Opacity of the solid border strip on each side of a biome border.",
                new AcceptableValueRange<float>(0.0f, 1.0f));

            BorderWidth = Bind(config, OverlaySection, "BorderWidth", 0.35f,
                "Width in meters of the solid border strip on each side of a biome border.",
                new AcceptableValueRange<float>(0.05f, 2.0f));

            EnableHatchLines = Bind(config, OverlaySection, "EnableHatchLines", false,
                "Draws diagonal lines across the area of each biome in the color of that biome. The lines use BorderOpacity.");

            HatchLineSpacing = Bind(config, OverlaySection, "HatchLineSpacing", 2.0f,
                "Distance in meters between the diagonal lines inside each biome area.",
                new AcceptableValueRange<float>(0.5f, 10.0f));

            HatchLineWidth = Bind(config, OverlaySection, "HatchLineWidth", 0.2f,
                "Width in meters of each diagonal line inside each biome area.",
                new AcceptableValueRange<float>(0.05f, 2.0f));

            MeadowsColor = BindBiomeColor(config, "MeadowsColor", new Color32(0x00, 0xC1, 0x00, 0x4F), "Meadows");
            BlackForestColor = BindBiomeColor(config, "BlackForestColor", new Color32(0x00, 0x73, 0xC1, 0x4F), "Black Forest");
            SwampColor = BindBiomeColor(config, "SwampColor", new Color32(0xC1, 0x66, 0x33, 0x4F), "Swamp");
            MountainColor = BindBiomeColor(config, "MountainColor", new Color32(0x88, 0xD2, 0xFF, 0x4F), "Mountain");
            PlainsColor = BindBiomeColor(config, "PlainsColor", new Color32(0xFF, 0xC1, 0x26, 0x4F), "Plains");
            MistlandsColor = BindBiomeColor(config, "MistlandsColor", new Color32(0xA6, 0x59, 0xE6, 0x4F), "Mistlands");
            AshLandsColor = BindBiomeColor(config, "AshLandsColor", new Color32(0xFF, 0x40, 0x26, 0x4F), "Ashlands");
            DeepNorthColor = BindBiomeColor(config, "DeepNorthColor", new Color32(0x00, 0x00, 0x80, 0x87), "Deep North");
            OceanColor = BindBiomeColor(config, "OceanColor", new Color32(0x00, 0xD6, 0x8A, 0x4F), "Ocean");

            HudOffsetX = Bind(config, HudSection, "OffsetX", 610.938f,
                "Horizontal offset in pixels from the top-left corner of the screen for the biome HUD. Positive values move the HUD right.",
                new AcceptableValueRange<float>(-5000.0f, 5000.0f));

            HudOffsetY = Bind(config, HudSection, "OffsetY", -46.0f,
                "Vertical offset in pixels from the top-left corner of the screen for the biome HUD. Negative values move the HUD down.",
                new AcceptableValueRange<float>(-3000.0f, 3000.0f));

            HudFontSize = Bind(config, HudSection, "FontSize", 14,
                "Font size in pixels for the biome HUD.",
                new AcceptableValueRange<int>(8, 32));

            HudBackgroundColor = Bind<Color>(config, HudSection, "BackgroundColor", new Color32(0x14, 0x14, 0x14, 0x80),
                "Background color and opacity for the biome HUD. Set alpha to 0 to remove the background.");

            HudUpdateInterval = Bind(config, HudSection, "UpdateInterval", 0.1f,
                "Seconds between biome HUD updates.",
                new AcceptableValueRange<float>(0.05f, 1.0f));

            TargetRayDistance = Bind(config, HudSection, "TargetRayDistance", 50.0f,
                "Maximum distance in meters from the camera to the ground point used for the Target biome.",
                new AcceptableValueRange<float>(5.0f, 200.0f));

            ToggleOverlayKey = Bind(config, KeybindingsSection, "ToggleOverlayKey", new KeyboardShortcut(KeyCode.P, KeyCode.LeftShift),
                "Shows or hides the biome overlay, with or without the cultivator equipped.");
        }

        public static Color GetBiomeColor(Heightmap.Biome biome) => biome switch
        {
            Heightmap.Biome.Meadows => MeadowsColor.Value,
            Heightmap.Biome.BlackForest => BlackForestColor.Value,
            Heightmap.Biome.Swamp => SwampColor.Value,
            Heightmap.Biome.Mountain => MountainColor.Value,
            Heightmap.Biome.Plains => PlainsColor.Value,
            Heightmap.Biome.Mistlands => MistlandsColor.Value,
            Heightmap.Biome.AshLands => AshLandsColor.Value,
            Heightmap.Biome.DeepNorth => DeepNorthColor.Value,
            Heightmap.Biome.Ocean => OceanColor.Value,
            _ => Color.clear
        };

        private static ConfigEntry<Color> BindBiomeColor(ConfigFile config, string key, Color defaultColor, string biomeName) =>
            Bind(config, BiomeColorsSection, key, defaultColor,
                $"Overlay and HUD color for {biomeName}. Alpha sets the opacity of the colored area inside the biome.");

        // jotunn ordering keeps the config manager in the same order as this file
        private static ConfigEntry<T> Bind<T>(
            ConfigFile config,
            string section,
            string key,
            T defaultValue,
            string description,
            AcceptableValueBase? acceptableValues = null) =>
            config.BindConfigInOrder(
                section,
                key,
                defaultValue,
                description,
                synced: false,
                sectionOrder: true,
                settingOrder: true,
                acceptableValues: acceptableValues);
    }
}
