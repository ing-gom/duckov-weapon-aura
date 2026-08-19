using System;
using System.Collections.Generic;
using UnityEngine;
using WeaponAura.Settings;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 총을 쏠 때 총구에서 터지는 화염.
    ///
    /// 게임은 총마다 다른 프리팹(<c>ItemSetting_Gun.muzzleFxPfb</c>)을 발사마다 새로 만들어
    /// Muzzle 소켓에 붙입니다. 그 프리팹을 뜯어 색을 바꾸는 건 총마다 파티클 구성이 달라
    /// 깨지기 쉬워서, 우리 화염을 따로 만들어 위에 얹습니다
    /// (원하면 <see cref="MuzzleFlashSettings.HideDefault"/>로 게임 것을 지울 수 있습니다).
    ///
    /// 총구에 자식으로 붙이지 않고 위치만 따라갑니다. 무기를 바꾸면 에이전트가 통째로
    /// 사라지는데, 자식으로 붙여 두면 풀에 넣어 둔 오브젝트까지 같이 파괴됩니다.
    /// </summary>
    public static class MuzzleFlashSystem
    {
        /// <summary>동시에 살아 있을 수 있는 화염 수. 넘으면 이번 발사는 건너뜁니다.</summary>
        private const int MaxLiveFlashes = 24;

        private const string HolderName = "WeaponAura_MuzzleFlashes";
        private const string FlashName = "WeaponAura_MuzzleFlash";

        /// <summary>불꽃은 화염보다 오래 남아야 튀는 것처럼 보입니다.</summary>
        private const float SparkLifetimeScale = 2.5f;

        internal sealed class FlashHandle
        {
            public GameObject Go = null!;
            public ParticleSystem Core = null!;
            public ParticleSystem Sparks = null!;
            public ParticleSystemRenderer CoreRenderer = null!;
            public ParticleSystemRenderer SparkRenderer = null!;

            /// <summary>따라갈 총구. null이면 사그라지는 중입니다.</summary>
            public Transform? Follow;

            public float RecycleAt;
        }

        private static GameObject? _holder;
        private static readonly Stack<FlashHandle> _idle = new Stack<FlashHandle>();
        private static readonly List<FlashHandle> _live = new List<FlashHandle>();

        private static float? _colorScale;
        private static bool _loggedFirstSpawn;
        private static bool _loggedFirstTint;

        /// <summary>지금 살아 있는 화염 수 (진단용)</summary>
        public static int LiveCount => _live.Count;

        // ──────────────────────────────────────────────────────────

        /// <summary>발사 직후 총구에서 화염을 터뜨립니다.</summary>
        /// <param name="weaponTypeId">쏜 총의 TypeID. 전용 설정이 걸려 있으면 등급보다 우선합니다.</param>
        public static void Spawn(Transform? muzzle, int ammoQuality, int weaponTypeId = 0)
        {
            if (muzzle == null || !MuzzleFlashSettings.Enabled)
                return;

            try
            {
                var profile = MuzzleFlashProfiles.Resolve(ammoQuality, weaponTypeId);
                if (profile == null || !profile.enabled)
                    return;

                if (_live.Count >= MaxLiveFlashes)
                    return;

                var handle = Rent();
                if (handle == null)
                    return;

                handle.Go.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
                handle.Go.SetActive(true);

                Configure(handle, profile);

                // Emit만 해서는 안 됩니다. 멈춰 있는 파티클 시스템은 뿜어낸 알갱이를
                // 시뮬레이션하지 않아서, 첫 프레임 모습 그대로 굳어 버립니다.
                handle.Core.Clear();

                // 코어 크기를 0으로 내리면 화염 없이 알갱이만 나갑니다
                // ("화염 대신 하트가 뿅뿅" 같은 연출이 이 조합입니다).
                if (profile.size > 0.005f)
                {
                    handle.Core.Play();
                    handle.Core.Emit(1);
                }

                handle.Sparks.Clear();
                if (profile.sparkCount > 0)
                {
                    handle.Sparks.Play();
                    handle.Sparks.Emit(profile.sparkCount);
                }

                handle.Follow = muzzle;

                // 불꽃이 화염보다 오래 남으므로 긴 쪽에 맞춰 회수합니다.
                float life = profile.duration * (profile.sparkCount > 0 ? SparkLifetimeScale : 1f);
                handle.RecycleAt = Time.time + life + 0.05f;

                _live.Add(handle);

                // 처음 한 번만. "화염이 안 보인다"가 스폰 실패인지 그냥 안 보이는 건지
                // 로그 한 줄로 갈립니다 (Release 빌드에서도 남깁니다).
                if (!_loggedFirstSpawn)
                {
                    _loggedFirstSpawn = true;
                    string shapeName = string.IsNullOrEmpty(profile.textureName)
                        ? profile.shape.ToString()
                        : profile.textureName;

                    // 크기를 눈대중으로 맞추다 세 번 틀렸습니다. 기준이 될 실측값을 같이 남깁니다 —
                    // 무기 바운딩 박스는 오라 시스템이 이미 재고 있고, 그게 곧 월드 단위 기준자입니다.
                    // 셰이더 이름도 찍습니다. 못 찾으면 머티리얼이 비어 아무것도 안 그려지는데,
                    // 그 경우 "작다"와 "안 보인다"가 구분되지 않습니다.
                    var material = handle.CoreRenderer != null
                        ? handle.CoreRenderer.sharedMaterial : null;
                    string shader = material != null && material.shader != null
                        ? material.shader.name : "(없음)";

                    UnityEngine.Debug.Log(
                        $"[WeaponAura] 총구 화염 첫 발생: 등급 {profile.grade}({profile.name}) " +
                        $"모양={shapeName} 셰이더={shader} " +
                        $"코어={profile.size:0.###}m 알갱이={profile.sparkCount}x{profile.sparkSize:0.###}m " +
                        $"거리={profile.sparkDistance:0.##}m 상승={profile.sparkRise:+0.##;-0.##;0}m " +
                        $"지속={profile.duration:0.###}s 위치={handle.Go.transform.position} " +
                        $"| 기준: 무기 실측 크기={WeaponAuraSystem.WeaponBoundsSize.ToString("0.###")}");
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 생성 실패: {ex.Message}");
#endif
            }
        }

        // ── 게임 기본 화염 물들이기 ──────────────────────────────

        /// <summary>
        /// 게임이 방금 만든 총구 화염의 <b>형태는 그대로 두고</b> 색과 크기만 바꿉니다.
        ///
        /// 실제 물들이기는 <see cref="EffectTint"/>가 합니다 — 근접 참격도 같은 문제를
        /// 풀기 때문에 한 곳에 모아 두었습니다. 여기서는 프로필 값을 색으로 풀고,
        /// 첫 발사 때 한 번 실측 로그를 남깁니다.
        /// </summary>
        public static void TintExisting(GameObject? flash, MuzzleFlashProfile profile)
        {
            if (flash == null || profile == null)
                return;

            try
            {
                BulletTrailShading.Resolve(profile.colorInner, profile.intensity, profile.alpha,
                    out Color inner, out float innerAlpha);
                BulletTrailShading.Resolve(profile.colorOuter, profile.intensity, profile.alpha,
                    out Color outer, out float outerAlpha);

                float alpha = Mathf.Clamp01(Mathf.Min(innerAlpha, outerAlpha));
                float scale = Mathf.Max(0.05f, profile.sizeScale);

                EffectTint.Apply(flash, inner, outer, alpha, scale);

                LogFirstFlash(flash, profile, scale);
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 색 변경 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 첫 발사 때 게임 화염이 실제로 어떻게 생겼는지 한 줄 남깁니다.
        /// 크기를 눈대중으로 맞출 수가 없어서(총마다 다릅니다) 실측값이 필요합니다.
        /// </summary>
        private static void LogFirstFlash(GameObject flash, MuzzleFlashProfile profile, float scale)
        {
            if (_loggedFirstTint)
                return;

            _loggedFirstTint = true;

            UnityEngine.Debug.Log(
                $"[WeaponAura] 게임 총구 화염 실측: '{flash.name}' {EffectTint.Describe(flash)} " +
                $"| 총구스케일={flash.transform.lossyScale.ToString("0.###")} " +
                $"적용배율={scale:0.##} (등급 {profile.grade} {profile.name})");
        }

        /// <summary>
        /// ModBehaviour.LateUpdate에서 호출 — 총구를 따라갑니다.
        ///
        /// 화염은 0.05초쯤이라 몇 프레임뿐이지만, 그 사이에도 조준을 돌리면 총구가 움직입니다.
        /// 따라가지 않으면 화염만 허공에 남습니다.
        /// </summary>
        public static void LateTick()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var handle = _live[i];

                if (handle.Go == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                if (!ReferenceEquals(handle.Follow, null))
                {
                    var muzzle = handle.Follow;
                    if (muzzle != null && muzzle.gameObject.activeInHierarchy)
                        handle.Go.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
                    else
                        handle.Follow = null;   // 무기가 사라졌으면 제자리에서 사그라집니다.
                }

                if (Time.time >= handle.RecycleAt)
                {
                    Recycle(handle);
                    _live.RemoveAt(i);
                }
            }
        }

        public static void Clear()
        {
            try
            {
                foreach (var handle in _live)
                    Recycle(handle);

                _live.Clear();
            }
            catch
            {
                // 정리 중 예외는 무시합니다.
            }
        }

        public static void Dispose()
        {
            Clear();
            _idle.Clear();

            if (_holder != null)
            {
                UnityEngine.Object.Destroy(_holder);
                _holder = null;
            }
        }

        // ── 풀 ───────────────────────────────────────────────────

        private static FlashHandle? Rent()
        {
            EnsureHolder();
            if (_holder == null)
                return null;

            while (_idle.Count > 0)
            {
                var reused = _idle.Pop();
                if (reused.Go != null && reused.Core != null && reused.Sparks != null)
                    return reused;
            }

            return CreateFlashObject(_holder.transform, active: false);
        }

        private static FlashHandle CreateFlashObject(Transform parent, bool active)
        {
            var go = new GameObject(FlashName);
            go.transform.SetParent(parent, false);
            go.SetActive(active);

            var core = CreateSystem(go.transform, "Core", ParticleSystemRenderMode.Billboard);
            var sparks = CreateSystem(go.transform, "Sparks", ParticleSystemRenderMode.Stretch);

            return new FlashHandle
            {
                Go = go,
                Core = core,
                Sparks = sparks,
                CoreRenderer = core.GetComponent<ParticleSystemRenderer>(),
                SparkRenderer = sparks.GetComponent<ParticleSystemRenderer>(),
            };
        }

        /// <summary>
        /// 설정 창 미리보기에서 쓰는 이펙트 한 벌.
        ///
        /// 풀에서 빌려 쓰지 않고 따로 만듭니다 — 풀 오브젝트는 게임 화면에 나가는 것이라
        /// 미리보기 전용 레이어로 옮기면 실제 발사 때 안 보이게 됩니다.
        /// 대신 <see cref="ConfigureFor"/>로 런타임과 <b>같은 함수</b>를 태워서 모양이 갈리지 않게 합니다.
        /// </summary>
        public sealed class PreviewEmitter
        {
            internal FlashHandle Handle = null!;

            public GameObject Root => Handle.Go;

            /// <summary>알갱이가 사라지기까지 걸리는 시간(초)</summary>
            public float TotalLife { get; internal set; }
        }

        public static PreviewEmitter CreatePreviewEmitter(Transform parent)
        {
            return new PreviewEmitter { Handle = CreateFlashObject(parent, active: true) };
        }

        /// <summary>미리보기 이펙트를 한 번 터뜨립니다 (런타임 Spawn과 같은 설정을 씁니다).</summary>
        public static void PreviewEmit(PreviewEmitter emitter, MuzzleFlashProfile profile)
        {
            if (emitter == null || profile == null || emitter.Handle.Go == null)
                return;

            var handle = emitter.Handle;
            Configure(handle, profile);

            handle.Core.Clear();
            if (profile.size > 0.005f)
            {
                handle.Core.Play();
                handle.Core.Emit(1);
            }

            handle.Sparks.Clear();
            if (profile.sparkCount > 0)
            {
                handle.Sparks.Play();
                handle.Sparks.Emit(profile.sparkCount);
            }

            emitter.TotalLife = Mathf.Max(0.01f, profile.duration)
                                * (profile.sparkCount > 0 ? SparkLifetimeScale : 1f);
        }

        /// <summary>
        /// 파티클 시스템 한 벌. 방출은 우리가 <c>Emit</c>으로 직접 합니다
        /// (발사 순간에 딱 한 번 터져야 해서 emission rate로는 맞출 수 없습니다).
        /// </summary>
        private static ParticleSystem CreateSystem(Transform parent, string name,
            ParticleSystemRenderMode renderMode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;

            // 설정 창이 떠 있는 동안 게임은 timeScale 0입니다. 스케일된 시간을 쓰면
            // 미리보기 파티클이 첫 프레임에서 얼어붙습니다. 실제 발사 때도 게임이
            // 멈춰 있으면 화염이 멈추는 게 맞으므로, 어느 쪽이든 이 설정이 옳습니다.
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 64;

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = false;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = renderMode;
            renderer.sharedMaterial = WeaponAuraResources.SharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.alignment = ParticleSystemRenderSpace.View;

            if (renderMode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 2.5f;
            }

            return system;
        }

        private static void Configure(FlashHandle handle, MuzzleFlashProfile profile)
        {
            float scale = ColorScale;

            BulletTrailShading.Resolve(profile.colorInner, profile.intensity, profile.alpha,
                out Color inner, out float innerAlpha);
            BulletTrailShading.Resolve(profile.colorOuter, profile.intensity, profile.alpha,
                out Color outer, out float outerAlpha);

            float alpha = Mathf.Min(innerAlpha, outerAlpha) * scale;
            inner *= scale;
            outer *= scale;

            float duration = Mathf.Max(0.01f, profile.duration);

            // 고른 도형(또는 사용자 PNG)으로 갈아 끼웁니다. 텍스처당 머티리얼 하나만 만들어 씁니다.
            var texture = MuzzleFlashShapes.Resolve(profile.shape, profile.textureName);
            var material = MuzzleFlashShapes.GetMaterial(texture);

            handle.CoreRenderer.sharedMaterial = material;
            handle.SparkRenderer.sharedMaterial = material;

            // 불티는 늘어나야 그럴듯하지만, 하트·별은 늘어나면 무엇인지 알아볼 수 없습니다.
            handle.SparkRenderer.renderMode = profile.sparkStretch
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;

            // ── 화염 코어 ──
            var coreMain = handle.Core.main;
            coreMain.startLifetime = duration;
            coreMain.startSize = Mathf.Max(0.01f, profile.size);
            coreMain.startSpeed = 0f;
            coreMain.startRotation = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            coreMain.startColor = WithAlpha(inner, alpha);

            var coreColor = handle.Core.colorOverLifetime;
            coreColor.enabled = true;
            coreColor.color = new ParticleSystem.MinMaxGradient(BuildFlashGradient(inner, outer));

            // 터지는 순간이 가장 크고, 사그라지며 살짝 줄어듭니다.
            var coreSize = handle.Core.sizeOverLifetime;
            coreSize.enabled = true;
            coreSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.75f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0.55f)));

            // ── 불꽃 ──
            var sparkMain = handle.Sparks.main;

            // 알갱이는 월드 공간에서 날아가야 합니다. 총구에 매어 두면 조준을 돌릴 때마다
            // 이미 튀어 나간 것까지 같이 휘둘립니다.
            sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
            float sparkLife = Mathf.Max(0.01f, duration * SparkLifetimeScale);

            sparkMain.startLifetime = sparkLife;
            sparkMain.startSize = Mathf.Max(0.002f, profile.sparkSize);
            sparkMain.startColor = WithAlpha(outer, alpha);

            // 거리 = 속도 × 수명 이므로 속도를 역산합니다.
            sparkMain.startSpeed = Mathf.Max(0f, profile.sparkDistance) / sparkLife;

            // 중력만 받는 물체의 이동량은 Δy = -½·9.81·g·t² 입니다.
            // 도달 높이를 정해 두고 g를 역산하면 수명이 길어져도 그 높이를 넘지 않습니다.
            // (배율을 직접 노출했을 때는 같은 값으로 6m씩 솟구쳤습니다)
            sparkMain.gravityModifier = -profile.sparkRise / (4.905f * sparkLife * sparkLife);

            sparkMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var sparkRotation = handle.Sparks.rotationOverLifetime;
            bool spins = Mathf.Abs(profile.sparkSpin) > 0.01f;
            sparkRotation.enabled = spins;
            if (spins)
                sparkRotation.z = profile.sparkSpin * Mathf.Deg2Rad;

            // 총구 앞쪽으로 흩어집니다.
            var sparkShape = handle.Sparks.shape;
            sparkShape.enabled = true;
            sparkShape.shapeType = ParticleSystemShapeType.Cone;
            sparkShape.angle = Mathf.Clamp(profile.sparkSpread, 0f, 90f);
            sparkShape.radius = 0.01f;

            var sparkColor = handle.Sparks.colorOverLifetime;
            sparkColor.enabled = true;
            sparkColor.color = new ParticleSystem.MinMaxGradient(BuildSparkGradient(inner, outer));

            var sparkSize = handle.Sparks.sizeOverLifetime;
            sparkSize.enabled = true;
            sparkSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        }

        /// <summary>중심의 밝은 색에서 등급 색으로 번지며 사라집니다.</summary>
        private static Gradient BuildFlashGradient(Color inner, Color outer)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(inner, 0f),
                    new GradientColorKey(outer, 0.45f),
                    new GradientColorKey(outer, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.3f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static Gradient BuildSparkGradient(Color inner, Color outer)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(inner, 0f),
                    new GradientColorKey(outer, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.6f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        /// <summary>
        /// 레거시 <c>Particles/Additive</c>는 최종 색을 2배로 냅니다. 그대로 넣으면
        /// 채널값 0.5만 넘어도 화면에서 1에 붙어 화염이 통째로 흰색이 됩니다.
        /// (탄환 잔상에서 겪은 것과 같은 문제입니다)
        /// </summary>
        private static float ColorScale
        {
            get
            {
                if (_colorScale.HasValue)
                    return _colorScale.Value;

                string shader = WeaponAuraResources.ResolvedShaderName;
                _colorScale = shader.IndexOf("Particles/Additive", StringComparison.OrdinalIgnoreCase) >= 0
                    ? 0.5f
                    : 1f;

                return _colorScale.Value;
            }
        }

        private static void Recycle(FlashHandle handle)
        {
            handle.Follow = null;

            if (handle.Go == null)
                return;

            if (handle.Core != null)
            {
                handle.Core.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                handle.Core.Clear();
            }

            if (handle.Sparks != null)
            {
                handle.Sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                handle.Sparks.Clear();
            }

            handle.Go.SetActive(false);

            if (_holder != null)
                handle.Go.transform.SetParent(_holder.transform, false);

            _idle.Push(handle);
        }

        private static void EnsureHolder()
        {
            if (_holder != null)
                return;

            _holder = new GameObject(HolderName);
            UnityEngine.Object.DontDestroyOnLoad(_holder);
        }
    }
}
