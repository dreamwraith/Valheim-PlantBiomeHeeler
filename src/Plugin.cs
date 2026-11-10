using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using PlantBiomeHeeler.Configuration;
using PlantBiomeHeeler.Core.Input;
using PlantBiomeHeeler.Core.UI;
using PlantBiomeHeeler.Core.Visuals;

namespace PlantBiomeHeeler
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, BepInDependency.DependencyFlags.HardDependency)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "dreamwraith.PlantBiomeHeeler";
        public const string ModName = "PlantBiomeHeeler";
        public const string ModVersion = VersionInfo.Version;

        private Harmony? _harmony;

        internal static ManualLogSource Log { get; private set; } = null!;

        private void Awake()
        {
            Log = Logger;

            ModConfig.Initialize(Config);
            new ConfigFileWatcher(Config);

            // client only, a dedicated server has nothing to draw
            if (GUIManager.IsHeadless())
            {
                return;
            }

            ModInputManager.Initialize();

            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), ModGUID);
            LogInfo($"{ModName} v{ModVersion} initialized.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            BiomeOverlayRenderer.Cleanup();
            BiomeHudManager.Cleanup();
        }

        internal static void LogInfo(string message)
        {
            if (ModConfig.IsInfoLogging)
            {
                Log.LogInfo(message);
            }
        }

        internal static void LogDebug(string message)
        {
            if (ModConfig.IsDebugLogging)
            {
                Log.LogInfo($"[DEBUG] {message}");
            }
        }
    }
}
