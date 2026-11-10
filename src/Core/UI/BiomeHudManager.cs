using System.Collections;
using BepInEx.Configuration;
using PlantBiomeHeeler.Configuration;
using PlantBiomeHeeler.Core.State;
using UnityEngine;

namespace PlantBiomeHeeler.Core.UI
{
    public sealed class BiomeHudManager : MonoBehaviour
    {
        private static BiomeHudManager? _instance;

        private BiomeHudPanel _panel = null!;
        private WaitForSeconds _cachedWait = null!;

        private void Awake()
        {
            _instance = this;
            ModConfig.SettingChanged += OnConfigSettingChanged;
        }

        private void OnDestroy()
        {
            ModConfig.SettingChanged -= OnConfigSettingChanged;
            _panel.Destroy();

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        public static void AttachToHud(Hud hud)
        {
            if (!hud.TryGetComponent(out BiomeHudManager _))
            {
                hud.gameObject.AddComponent<BiomeHudManager>().Initialize(hud);
            }
        }

        // OnDestroy releases the panel and the subscription
        public static void Cleanup()
        {
            if (_instance)
            {
                Destroy(_instance);
            }
        }

        private void Initialize(Hud hud)
        {
            // the game hides its hud by moving m_rootObject offscreen, so parenting here hides this with it
            _panel = new BiomeHudPanel(hud.m_rootObject.transform);
            ApplySettings();
            StartCoroutine(PollLoop());
        }

        private IEnumerator PollLoop()
        {
            while (true)
            {
                yield return _cachedWait;
                Refresh();
            }
        }

        private void Refresh()
        {
            Player localPlayer = Player.m_localPlayer;
            if (!localPlayer || !OverlayVisibility.IsHudVisible)
            {
                _panel.SetActive(false);
                return;
            }

            Heightmap.Biome standingBiome = Heightmap.FindBiome(localPlayer.transform.position);
            Heightmap.Biome targetBiome = TryGetTargetPoint(localPlayer, out Vector3 targetPoint)
                ? Heightmap.FindBiome(targetPoint)
                : Heightmap.Biome.None;

            _panel.SetBiomes(targetBiome, standingBiome);
            _panel.SetActive(true);
        }

        // the placement ghost is where a plant actually spawns, so it wins over the crosshair ray when its out
        private static bool TryGetTargetPoint(Player player, out Vector3 targetPoint)
        {
            GameObject placementGhost = player.m_placementGhost;
            if (placementGhost && placementGhost.activeSelf)
            {
                targetPoint = placementGhost.transform.position;
                return true;
            }

            Transform cameraTransform = Utils.GetMainCamera().transform;
            if (Physics.Raycast(
                    cameraTransform.position,
                    cameraTransform.forward,
                    out RaycastHit hitInfo,
                    ModConfig.TargetRayDistance.Value,
                    ZoneSystem.instance.m_terrainRayMask))
            {
                targetPoint = hitInfo.point;
                return true;
            }

            targetPoint = Vector3.zero;
            return false;
        }

        private void OnConfigSettingChanged(object sender, SettingChangedEventArgs eventArgs) =>
            ApplySettings();

        private void ApplySettings()
        {
            _cachedWait = new WaitForSeconds(ModConfig.HudUpdateInterval.Value);
            _panel.ApplySettings();
        }
    }
}
