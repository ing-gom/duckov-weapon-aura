using System;
using System.Collections.Generic;
using UnityEngine;
using WeaponAura.Helpers;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 무기 오라 위에 겹을 더 얹습니다.
    ///
    /// 알갱이는 우리가 직접 만듭니다. 총구 화염이 쓰는 내장 도형 텍스처를 그대로
    /// 재사용하므로 새 에셋이 필요 없고, 게임 에셋에 기대지 않아 <b>어느 무기에서나
    /// 같게 동작합니다</b>. 예전에 점광원(빛)을 빌려 쓰던 방식은 그렇지 않았습니다 —
    /// 무기에 따라 점광원이 있기도 없기도 해서 결과가 갈렸습니다.
    ///
    /// 본체 오라(<see cref="WeaponAuraController"/>)와 시스템을 나눈 이유는 성격이
    /// 다르기 때문입니다. 본체 오라는 무기 메시를 읽어 실루엣을 감싸는 한 벌이고,
    /// 겹은 정해진 자리에서 뿜는 단순한 이미터입니다. 한 컨트롤러에 밀어 넣으면
    /// 메시를 못 읽었을 때 겹까지 같이 무너집니다.
    /// </summary>
    public static class WeaponAuraLayerSystem
    {
        private const string RootName = "WeaponAura_Layer";

        /// <summary>한 겹이 동시에 띄울 수 있는 알갱이 수의 상한.</summary>
        private const int MaxParticlesPerLayer = 256;

        /// <summary>겹 하나에 대응하는 실물</summary>
        private sealed class LiveLayer
        {
            public GameObject Go = null!;
            public WeaponEffectLayer Source = null!;
            public ParticleSystem Particles = null!;

            /// <summary>무기 전체에서 뿜는 겹이 쓰는 바운즈 크기 (만들 때 잰 값)</summary>
            public Vector3 WeaponSize = Vector3.one * 0.2f;
        }

        private static GameObject? _root;
        private static readonly List<LiveLayer> _live = new List<LiveLayer>();

        private static Component? _trackedAgent;
        private static int _trackedTypeId;
        private static string _trackedKey = "";
        private static string _trackedShape = "";

        private static float _nextCheck;
        private const float CheckInterval = 0.25f;

        /// <summary>지금 붙어 있는 겹 수 (진단·설정 창 표시용)</summary>
        public static int LiveLayerCount => _live.Count;

        public static void Tick()
        {
            if (Time.unscaledTime < _nextCheck)
                return;

            _nextCheck = Time.unscaledTime + CheckInterval;
            Evaluate();
        }

        /// <summary>구조가 바뀌었을 때 — 다음 틱에 다시 만들게 합니다.</summary>
        public static void RebuildNow()
        {
            Clear();
            _nextCheck = 0f;
        }

        /// <summary>
        /// 값만 바뀌었을 때 — <b>지금 즉시</b> 덮어씁니다.
        ///
        /// 판단은 0.25초마다 도는데, 슬라이더를 끌 때 그 간격이 그대로 느껴집니다.
        /// 색을 옮기면 네 번에 한 번꼴로 따라오는 것처럼 보여서 "반응이 없다"로 읽힙니다.
        /// </summary>
        public static void ApplyNow()
        {
            if (_live.Count == 0)
                return;

            try
            {
                foreach (var live in _live)
                {
                    if (live.Go != null)
                        Apply(live);
                }
            }
            catch
            {
                // 다음 틱이 어차피 다시 씁니다.
            }
        }

        public static void Clear()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            foreach (var live in _live)
            {
                // 겹은 붙는 자리가 제각각(총구 소켓 등)이라 _root의 자식이 아닙니다.
                if (live.Go != null)
                    UnityEngine.Object.Destroy(live.Go);
            }

            _live.Clear();
            _trackedAgent = null;
            _trackedTypeId = 0;
            _trackedKey = "";
            _trackedShape = "";
        }

        public static void Dispose() => Clear();

        // ──────────────────────────────────────────────────────────

        private static void Evaluate()
        {
            try
            {
                var player = CharacterMainControl.Main;
                var holder = player != null ? player.agentHolder : null;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;
                var item = agent != null ? agent.Item : null;

                // 겹은 오라의 일부입니다. 오라 세기가 0이면(표시 끔) 함께 사라집니다 —
                // "오라를 껐는데 알갱이만 남아 있다"가 되면 안 됩니다.
                if (agent == null || item == null || !WeaponHelper.IsWeapon(item)
                    || WeaponAuraSystem.CurrentIntensity() <= 0f)
                {
                    Clear();
                    return;
                }

                int typeId = SafeTypeId(item);
                int quality = WeaponHelper.GetQuality(item);

                var profile = WeaponAuraSystem.ResolveAuraProfile(typeId, WeaponAuraProfiles.ResolveTier(quality));
                if (profile == null || !profile.enabled || profile.layers.Length == 0)
                {
                    Clear();
                    return;
                }

                string key = WeaponOverrides.ResolveKey(typeId);
                string shape = DescribeShape(profile);

                // 값만 바뀐 것과 구조가 바뀐 것을 가릅니다. 겹의 수·자리·모양이 그대로면
                // 다시 만들지 않고 값만 덮어씁니다 — 매번 새로 만들면 알갱이가 끊깁니다.
                bool sameStructure = ReferenceEquals(agent, _trackedAgent)
                                     && typeId == _trackedTypeId
                                     && key == _trackedKey
                                     && shape == _trackedShape;

                if (sameStructure && _root != null)
                {
                    ApplyNow();
                    return;
                }

                Clear();

                if (!Build(agent, profile))
                    return;

                _trackedAgent = agent;
                _trackedTypeId = typeId;
                _trackedKey = key;
                _trackedShape = shape;
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 오라 겹 갱신 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 다시 만들어야 하는 변화만 골라낸 표식.
        ///
        /// 색·세기·크기·방출량은 살아 있는 채로 바꿀 수 있지만, 겹이 늘거나 붙는 자리나
        /// 알갱이 모양이 바뀌면 오브젝트를 새로 만들어야 합니다.
        /// </summary>
        private static string DescribeShape(WeaponAuraProfile profile)
        {
            var sb = new System.Text.StringBuilder();

            foreach (var layer in profile.layers)
            {
                if (layer == null)
                    continue;

                sb.Append(layer.enabled ? '1' : '0');
                sb.Append((int)layer.anchor);
                sb.Append((int)layer.shape);
                sb.Append((int)layer.direction);
                sb.Append(layer.stretch ? 'S' : 'B');
                sb.Append(layer.textureName);
                sb.Append('|');
            }

            return sb.ToString();
        }

        private static bool Build(Component agent, WeaponAuraProfile profile)
        {
            _root = new GameObject(RootName);
            _root.transform.SetParent(agent.transform, false);

            foreach (var layer in profile.layers)
            {
                if (layer == null || !layer.enabled)
                    continue;

                var live = BuildLayer(agent, layer);
                if (live != null)
                    _live.Add(live);
            }

            if (_live.Count == 0)
            {
                Clear();
                return false;
            }

            ApplyNow();
            LogBuild(profile);
            ScheduleDiagnose();
            return true;
        }

        private static LiveLayer? BuildLayer(Component agent, WeaponEffectLayer layer)
        {
            var anchor = ResolveAnchor(agent, layer.anchor,
                out Vector3 localPosition, out Vector3 weaponSize);

            var go = CreateEmitter(layer, anchor, localPosition, weaponSize, out var particles);

            return new LiveLayer
            {
                Go = go,
                Source = layer,
                Particles = particles,
                WeaponSize = weaponSize,
            };
        }

        /// <summary>
        /// 겹 하나의 이미터를 만듭니다.
        ///
        /// 미리보기도 같은 함수를 씁니다. 미리보기는 자기 무대를 따로 세우기 때문에
        /// 여기서 만든 것이 자동으로 따라가지 않는데, 그렇다고 같은 설정을 두 번 적으면
        /// 한쪽만 고치는 순간 "게임과 미리보기가 다르게 보이는" 문제가 시작됩니다.
        /// (오라 본체도 같은 이유로 컨트롤러를 공유합니다)
        /// </summary>
        /// <param name="localSpace">
        /// 알갱이를 무기에 붙여 따라다니게 할지.
        ///
        /// 게임에서는 <b>false</b>가 맞습니다 — 총을 휘두르면 알갱이가 지나간 자리에
        /// 남아야 "뿜어져 나온다"가 됩니다. 반대로 미리보기 무대는 모델을 제자리에서
        /// 계속 돌리기 때문에, 월드로 두면 알갱이가 그 회전을 따라 고리로 번져서
        /// 실제 게임에서 볼 그림과 전혀 달라집니다. 오라 본체도 같은 이유로
        /// 미리보기에서만 로컬로 돕니다(<c>ForceLocalParticles</c>).
        /// </param>
        public static GameObject CreateEmitter(WeaponEffectLayer layer, Transform anchor,
            Vector3 localPosition, Vector3 weaponSize, out ParticleSystem particles,
            bool localSpace = false)
        {
            var go = new GameObject($"{RootName}_Layer");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = localPosition + layer.offset;
            go.transform.localRotation = Quaternion.identity;

            particles = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.maxParticles = MaxParticlesPerLayer;

            // <b>멈추지 않는 시간을 씁니다.</b>
            //
            // 설정 창은 게임 시간을 멈춥니다(Time.timeScale = 0). 파티클은 기본적으로 그
            // 시간을 따르기 때문에, 창을 열어 둔 채로 겹을 만들면 <b>한 개도 뿜지 못합니다</b>.
            // 실측에서 "재생=True 뿜는중=True 살아있음=0"으로 나온 것이 이것입니다 —
            // 시스템은 정상인데 시간이 흐르지 않아 결과가 0이었습니다.
            //
            // 겹은 창에서 만들고 그 자리에서 확인하는 것이라, 멈춘 동안 안 보이면 기능
            // 자체가 성립하지 않습니다. 대신 일시정지 중에도 알갱이가 계속 흐릅니다 —
            // 그 편이 "아무것도 안 나온다"보다 낫습니다.
            main.useUnscaledTime = true;

            // 기본은 월드입니다. 로컬이면 알갱이가 무기에 붙어 따라다녀서 "뿜어져 나온다"가
            // 성립하지 않습니다 — 총알 자국 잔상에서 같은 이유로 이미 겪은 문제입니다.
            main.simulationSpace = localSpace
                ? ParticleSystemSimulationSpace.Local
                : ParticleSystemSimulationSpace.World;

            var shape = particles.shape;
            shape.enabled = true;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverDistance = 0f;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var texture = MuzzleFlashShapes.Resolve(layer.shape, layer.textureName);
            renderer.sharedMaterial = MuzzleFlashShapes.GetMaterial(texture);

            ApplyTo(particles, layer, weaponSize);
            particles.Play();

            return go;
        }

        private static void Apply(LiveLayer live) => ApplyTo(live.Particles, live.Source, live.WeaponSize);

        /// <summary>값만 덮어씁니다. 미리보기도 같은 함수를 씁니다.</summary>
        public static void ApplyTo(ParticleSystem particles, WeaponEffectLayer layer, Vector3 weaponSize)
        {
            var main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(layer.ResolveColor());
            main.startSize = Mathf.Max(0.005f, layer.size);
            main.startLifetime = Mathf.Max(0.05f, layer.lifetime);
            main.startSpeed = layer.speed;

            // 중력을 음수로 주면 떠오릅니다. 게임 중력이 크므로 배율을 줄여 씁니다.
            main.gravityModifier = -layer.rise * 0.1f;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, layer.rate);

            main.startRotation = 0f;

            ApplyShape(particles, layer, weaponSize);
            ApplyLifetimeCurves(particles, layer);
            ApplyLook(particles, layer);
        }

        /// <summary>
        /// 어디서 어느 쪽으로 뿜을지.
        ///
        /// 방향이 "사방"이면 예전처럼 구(무기 전체는 상자)에서 퍼집니다. 방향이 있으면
        /// <b>원뿔</b>로 바꾸고 그 원뿔을 무기 기준으로 돌립니다 — 총구에서 물이 아래로
        /// 떨어지는 연출이 이것입니다. 구로는 사방으로 튀어서 "물이 폭발"이 됩니다.
        /// </summary>
        private static void ApplyShape(ParticleSystem particles, WeaponEffectLayer layer,
            Vector3 weaponSize)
        {
            var shape = particles.shape;
            shape.enabled = true;

            if (layer.direction == WeaponParticleDirection.Sphere)
            {
                shape.shapeType = layer.anchor == WeaponParticleAnchor.Whole
                    ? ParticleSystemShapeType.Box
                    : ParticleSystemShapeType.Sphere;

                shape.rotation = Vector3.zero;

                if (layer.anchor == WeaponParticleAnchor.Whole)
                {
                    // 무기 바운즈를 퍼짐 값만큼 부풀립니다. 0이면 무기 크기 그대로입니다.
                    shape.scale = weaponSize * (1f + Mathf.Max(0f, layer.spread));
                }
                else
                {
                    shape.scale = Vector3.one;
                    shape.radius = Mathf.Max(0.001f, layer.spread);
                }

                return;
            }

            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = Mathf.Clamp(layer.coneAngle, 0f, 89f);
            shape.radius = Mathf.Max(0.001f, layer.spread);
            shape.scale = Vector3.one;

            // Unity의 원뿔은 로컬 +Z로 뿜습니다. 그 축을 원하는 쪽으로 돌립니다.
            shape.rotation = layer.direction switch
            {
                WeaponParticleDirection.Forward => Vector3.zero,
                WeaponParticleDirection.Backward => new Vector3(0f, 180f, 0f),
                WeaponParticleDirection.Up => new Vector3(-90f, 0f, 0f),
                WeaponParticleDirection.Down => new Vector3(90f, 0f, 0f),
                _ => Vector3.zero,
            };
        }

        /// <summary>수명에 따른 색·크기 변화. 불꽃·연기가 그럴듯해지는 지점입니다.</summary>
        private static void ApplyLifetimeCurves(ParticleSystem particles, WeaponEffectLayer layer)
        {
            var overLifetime = particles.colorOverLifetime;
            overLifetime.enabled = true;

            var gradient = new Gradient();
            var endColor = layer.useColorEnd ? layer.colorEnd : layer.color;

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(layer.color, 0f),
                    new GradientColorKey(endColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(Mathf.Clamp01(layer.alphaStart), 0f),
                    new GradientAlphaKey(Mathf.Clamp01(layer.alphaEnd), 1f),
                });

            overLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = particles.sizeOverLifetime;

            // 시작과 끝이 같으면 곡선을 켜 둘 이유가 없습니다 (계산만 늘어납니다).
            bool sizeChanges = !Mathf.Approximately(layer.sizeStart, layer.sizeEnd);
            sizeOverLifetime.enabled = sizeChanges;

            if (sizeChanges)
            {
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.Linear(0f, Mathf.Max(0.01f, layer.sizeStart),
                        1f, Mathf.Max(0.01f, layer.sizeEnd)));
            }
        }

        /// <summary>늘어짐 · 흔들림 · 회전.</summary>
        private static void ApplyLook(ParticleSystem particles, WeaponEffectLayer layer)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = layer.stretch
                    ? ParticleSystemRenderMode.Stretch
                    : ParticleSystemRenderMode.Billboard;

                if (layer.stretch)
                {
                    renderer.lengthScale = Mathf.Max(0.1f, layer.stretchScale);
                    renderer.velocityScale = 0f;
                }
            }

            var noise = particles.noise;
            noise.enabled = layer.noise > 0.0001f;
            if (noise.enabled)
            {
                noise.strength = layer.noise;
                noise.frequency = 0.6f;
                noise.scrollSpeed = 0.4f;
            }

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = Mathf.Abs(layer.spin) > 0.0001f;
            if (rotation.enabled)
                rotation.z = layer.spin * Mathf.Deg2Rad;
        }

        /// <summary>
        /// 이미터가 <b>실제로</b> 어떤 상태인지 한 줄로. 만들어졌는데 안 보일 때 씁니다.
        ///
        /// "안 만들어졌다"는 생성 로그로 걸러지지만, 만들어진 뒤의 실패(뿜지 않음 ·
        /// 렌더러 꺼짐 · 머티리얼 없음 · 화면 밖)는 값이 아니라 <b>런타임 상태</b>를
        /// 봐야 알 수 있습니다.
        /// </summary>
        public static string DescribeEmitter(ParticleSystem? particles, string label)
        {
            if (particles == null)
                return $"{label}: 파티클 없음";

            try
            {
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                var main = particles.main;

                string material = renderer == null || renderer.sharedMaterial == null
                    ? "없음"
                    : renderer.sharedMaterial.name;

                string texture = renderer?.sharedMaterial != null
                                 && renderer.sharedMaterial.mainTexture != null
                    ? renderer.sharedMaterial.mainTexture.name
                    : "없음";

                return $"{label}: 재생={particles.isPlaying} 뿜는중={particles.isEmitting} " +
                       $"살아있음={particles.particleCount} " +
                       $"렌더러={(renderer != null ? renderer.enabled.ToString() : "null")} " +
                       $"화면안={(renderer != null ? renderer.isVisible.ToString() : "-")} " +
                       $"레이어={particles.gameObject.layer} " +
                       $"머티리얼={material} 텍스처={texture} " +
                       $"시작색={main.startColor.color} 크기={main.startSize.constant:0.000} " +
                       $"수명={main.startLifetime.constant:0.00} 방출={particles.emission.rateOverTime.constant:0} " +
                       $"공간={main.simulationSpace} 위치={particles.transform.position}";
            }
            catch (Exception ex)
            {
                return $"{label}: 상태 읽기 실패 {ex.Message}";
            }
        }

        /// <summary>만든 뒤 잠시 지나서 한 번 실측합니다 (뿜기 시작할 시간을 줍니다).</summary>
        private static float _diagnoseAt;

        private static void ScheduleDiagnose() => _diagnoseAt = Time.unscaledTime + 1f;

        private static void RunDiagnoseIfDue()
        {
            if (_diagnoseAt <= 0f || Time.unscaledTime < _diagnoseAt)
                return;

            _diagnoseAt = 0f;

            var sb = new System.Text.StringBuilder("[WeaponAura] 겹 실측(게임)");
            foreach (var live in _live)
                sb.AppendLine().Append(DescribeEmitter(live.Particles, live.Source.anchor.ToString()));

            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>맥동. LateUpdate에서 매 프레임 부릅니다.</summary>
        public static void LateTick()
        {
            if (_live.Count == 0)
                return;

            RunDiagnoseIfDue();

            try
            {
                foreach (var live in _live)
                {
                    var layer = live.Source;
                    if (layer.pulseAmount <= 0.0001f || live.Go == null)
                        continue;

                    float period = Mathf.Max(0.1f, layer.pulsePeriod);
                    float wave = Mathf.Sin(Time.time / period * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float factor = 1f + (layer.pulseAmount * (wave - 0.5f) * 2f);

                    var baseColor = layer.ResolveColor();
                    var main = live.Particles.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(
                        baseColor.r * factor, baseColor.g * factor, baseColor.b * factor, baseColor.a));
                }
            }
            catch
            {
                // 연출 하나 때문에 프레임이 끊기면 안 됩니다.
            }
        }

        /// <summary>
        /// 무엇이 어디에 얼마나 만들어졌는지 한 줄로 남깁니다.
        ///
        /// "안 보인다"의 원인은 거의 항상 <b>안 만들어졌다</b> 아니면 <b>너무 작다·어둡다</b>
        /// 둘 중 하나인데, 로그가 없으면 그 둘을 구분할 수가 없습니다.
        /// </summary>
        private static void LogBuild(WeaponAuraProfile profile)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[WeaponAura] 오라 겹 생성: ")
              .Append(WeaponAuraSystem.CurrentWeaponName)
              .Append(" - 겹 ").Append(_live.Count).Append('/').Append(profile.layers.Length);

            foreach (var live in _live)
            {
                var layer = live.Source;

                sb.Append(" | ").Append(layer.anchor)
                  .Append(" 모양=")
                  .Append(string.IsNullOrEmpty(layer.textureName) ? layer.shape.ToString() : layer.textureName)
                  .Append(" 색=").Append(layer.ResolveColor())
                  .Append(" 방출=").Append(layer.rate.ToString("0")).Append("/s")
                  .Append(" 크기=").Append(layer.size.ToString("0.000"))
                  .Append(" 수명=").Append(layer.lifetime.ToString("0.00")).Append('s');

                if (layer.anchor == WeaponParticleAnchor.Whole)
                    sb.Append(" 상자=").Append(live.Particles.shape.scale);
                else
                    sb.Append(" 반경=").Append(layer.spread.ToString("0.00"));

                sb.Append(" 위치=").Append(live.Go.transform.position);
            }

            UnityEngine.Debug.Log(sb.ToString());
        }

        // ── 붙일 자리 ───────────────────────────────────────────────

        /// <summary>
        /// 총구는 게임이 이미 소켓으로 들고 있습니다(<c>ItemAgent_Gun.muzzle</c>).
        /// 나머지는 무기 바운즈로 계산합니다 — 근접무기에는 총구가 없기 때문에
        /// 총구를 못 찾으면 조용히 본체로 떨어집니다.
        /// </summary>
        private static Transform ResolveAnchor(Component agent, WeaponParticleAnchor anchor,
            out Vector3 localPosition, out Vector3 weaponSize)
        {
            var root = agent.transform;
            Vector3 center = WeaponLocalCenter(agent, root, out weaponSize);

            if (anchor == WeaponParticleAnchor.Muzzle && agent is ItemAgent_Gun gun)
            {
                var muzzle = gun.muzzle;
                if (muzzle != null)
                {
                    localPosition = Vector3.zero;
                    return muzzle;
                }
            }

            if (anchor == WeaponParticleAnchor.Barrel)
            {
                // 총열 — 본체 가운데에서 진행 방향(로컬 z)으로 절반만큼 더 나갑니다.
                localPosition = center + new Vector3(0f, 0f, weaponSize.z * 0.5f);
                return root;
            }

            // 본체 · 무기 전체 · 총구를 못 찾은 경우
            localPosition = center;
            return root;
        }

        private static Vector3 WeaponLocalCenter(Component agent, Transform root, out Vector3 size)
        {
            size = Vector3.one * 0.2f;

            try
            {
                bool initialized = false;
                Bounds bounds = default;

                foreach (var renderer in agent.GetComponentsInChildren<Renderer>(true))
                {
                    // 오라와 같은 규칙으로 무기 본체만 봅니다 — 점광원·파티클을 섞으면
                    // 자리가 총이 아니라 빛 덩어리 한가운데가 됩니다.
                    if (!WeaponAuraSystem.IsWeaponBodyRenderer(renderer))
                        continue;

                    if (!initialized)
                    {
                        bounds = renderer.bounds;
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                if (!initialized)
                    return Vector3.zero;

                size = bounds.size;
                return root.InverseTransformPoint(bounds.center);
            }
            catch
            {
                return Vector3.zero;
            }
        }

        private static int SafeTypeId(ItemStatsSystem.Item item)
        {
            try
            {
                return item.TypeID;
            }
            catch
            {
                return 0;
            }
        }
    }
}
