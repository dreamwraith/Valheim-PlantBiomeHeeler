using System.Diagnostics;
using BepInEx.Configuration;
using PlantBiomeHeeler.Configuration;
using PlantBiomeHeeler.Core.State;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlantBiomeHeeler.Core.Visuals
{
    public sealed class BiomeOverlayRenderer : MonoBehaviour
    {
        // hatch lines draw over the fill and borders over both, sprites default skips depth writes
        private const int FillRenderQueue = 3100;
        private const int HatchRenderQueue = 3101;
        private const int BorderRenderQueue = 3102;

        // unity built in from the game unity_builtin_extra, Shader.Find returns it in game
        private const string OverlayShaderName = "Sprites/Default";

        private static BiomeOverlayRenderer? _instance;

        private readonly BiomeOverlayMeshBuilder _meshBuilder = new BiomeOverlayMeshBuilder();

        private Mesh? _fillMesh;
        private Mesh? _hatchMesh;
        private Mesh? _borderMesh;
        private Material _fillMaterial = null!;
        private Material _hatchMaterial = null!;
        private Material _borderMaterial = null!;
        private Texture2D _hatchTexture = null!;
        private float _hatchLineFraction = -1f;
        private OverlayBuildSpec _builtSpec;
        private bool _hasValidMesh;
        private bool _isTerrainDirty;

        private void Awake()
        {
            _instance = this;

            Shader shader = Shader.Find(OverlayShaderName);
            _fillMaterial = CreateMaterial(shader, FillRenderQueue);
            _hatchMaterial = CreateMaterial(shader, HatchRenderQueue);
            _borderMaterial = CreateMaterial(shader, BorderRenderQueue);

            _hatchTexture = OverlayHatchTexture.Create();
            _hatchMaterial.mainTexture = _hatchTexture;

            ModConfig.SettingChanged += OnConfigSettingChanged;
        }

        private void LateUpdate()
        {
            if (!OverlayVisibility.IsOverlayActive)
            {
                _hasValidMesh = false;
                return;
            }

            Vector3 playerPosition = transform.position;
            OverlayBuildSpec buildSpec = new OverlayBuildSpec(
                Mathf.FloorToInt(playerPosition.x),
                Mathf.FloorToInt(playerPosition.z),
                ModConfig.OverlayRadius.Value,
                ModConfig.OverlayHeight.Value,
                ModConfig.InteriorOpacity.Value,
                ModConfig.BorderOpacity.Value,
                ModConfig.BorderWidth.Value,
                ZoneSystem.instance.m_waterLevel,
                ModConfig.EnableHatchLines.Value,
                ModConfig.HatchLineSpacing.Value,
                ModConfig.HatchLineWidth.Value);

            if (!_hasValidMesh || _isTerrainDirty || buildSpec != _builtSpec)
            {
                RebuildMeshes(in buildSpec);
            }

            // a rebuild just ran or ran earlier, so every mesh exists whenever there is geometry
            if (_meshBuilder.HasFillGeometry)
            {
                Graphics.DrawMesh(_fillMesh!, Matrix4x4.identity, _fillMaterial, 0);
            }

            if (_meshBuilder.HasHatchGeometry)
            {
                Graphics.DrawMesh(_hatchMesh!, Matrix4x4.identity, _hatchMaterial, 0);
            }

            if (_meshBuilder.HasBorderGeometry)
            {
                Graphics.DrawMesh(_borderMesh!, Matrix4x4.identity, _borderMaterial, 0);
            }
        }

        private void OnDestroy()
        {
            ModConfig.SettingChanged -= OnConfigSettingChanged;
            DestroyResource(ref _fillMesh);
            DestroyResource(ref _hatchMesh);
            DestroyResource(ref _borderMesh);
            Destroy(_fillMaterial);
            Destroy(_hatchMaterial);
            Destroy(_borderMaterial);
            Destroy(_hatchTexture);

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        public static void EnsureRenderer(Player player)
        {
            if (!player.TryGetComponent(out BiomeOverlayRenderer _))
            {
                player.gameObject.AddComponent<BiomeOverlayRenderer>();
            }
        }

        // OnDestroy releases the meshes, materials, hatch texture, and the subscription
        public static void Cleanup()
        {
            if (_instance)
            {
                Destroy(_instance);
            }
        }

        // zone loads, height edits, and terrain data from other players all end in a collision rebuild, so the overlay
        // picks up new terrain without polling. the cultivator smooths while it paints, so every swing rebuilds too
        // distant lod tiles arent in the sampled heightmaps, so their regenerations change nothing here
        public static void MarkTerrainDirty(Heightmap heightmap)
        {
            if (!_instance || !_instance._hasValidMesh || heightmap.IsDistantLod)
            {
                return;
            }

            OverlayBuildSpec builtSpec = _instance._builtSpec;
            if (PlantBiomeSampler.IsWithinReach(heightmap.transform.position, builtSpec.FocusCenter, builtSpec.Radius))
            {
                _instance._isTerrainDirty = true;
            }
        }

        // colors and shape both come from settings, so any change rebuilds on the next visible frame
        private void OnConfigSettingChanged(object sender, SettingChangedEventArgs eventArgs) =>
            _hasValidMesh = false;

        private void RebuildMeshes(in OverlayBuildSpec buildSpec)
        {
            if (!_fillMesh)
            {
                _fillMesh = CreateMesh("PlantBiomeHeeler_OverlayFill");
            }

            if (!_hatchMesh)
            {
                _hatchMesh = CreateMesh("PlantBiomeHeeler_OverlayHatch");
            }

            if (!_borderMesh)
            {
                _borderMesh = CreateMesh("PlantBiomeHeeler_OverlayBorder");
            }

            // the texture holds one line per uv period, so only the width to spacing ratio changes it
            float hatchLineFraction = Mathf.Clamp01(buildSpec.HatchLineWidth / buildSpec.HatchLineSpacing);
            if (hatchLineFraction != _hatchLineFraction)
            {
                OverlayHatchTexture.Fill(_hatchTexture, hatchLineFraction);
                _hatchLineFraction = hatchLineFraction;
            }

            long startTimestamp = Stopwatch.GetTimestamp();
            _meshBuilder.Rebuild(_fillMesh!, _hatchMesh!, _borderMesh!, in buildSpec);

            if (ModConfig.IsDebugLogging)
            {
                double elapsedMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
                Plugin.LogDebug($"Overlay rebuilt in {elapsedMilliseconds:F2} ms at radius {buildSpec.Radius:F0} m.");
            }

            _builtSpec = buildSpec;
            _hasValidMesh = true;
            _isTerrainDirty = false;
        }

        private static Mesh CreateMesh(string meshName)
        {
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.MarkDynamic();
            return mesh;
        }

        private static Material CreateMaterial(Shader shader, int renderQueue) =>
            new Material(shader)
            {
                color = Color.white,
                renderQueue = renderQueue,
                hideFlags = HideFlags.HideAndDontSave
            };

        private static void DestroyResource<T>(ref T? resource) where T : Object
        {
            if (resource)
            {
                Destroy(resource);
            }

            resource = null;
        }
    }
}
