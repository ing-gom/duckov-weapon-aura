using System;
using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using WeaponAura.Helpers;
using WeaponAura.Settings;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 근접무기를 휘두를 때 나가는 참격 이펙트.
    ///
    /// 게임은 무기마다 다른 프리팹(<c>ItemAgent_MeleeWeapon.slashFx</c>)을 휘두를 때마다
    /// 하나 만들어 캐릭터에 붙입니다. 실측해 보니 그 정체는 <b>파티클 시스템 하나</b>였습니다
    /// (<c>MeleeSlashFx(Clone)</c> · 파티클 1 · 렌더러 1 · URP/Particles/Unlit).
    /// 호는 그 알갱이 한 장에 그려진 그림이고, 커지면서 사라집니다.
    ///
    /// 그래서 흩뿌림은 그 알갱이를 <b>직접 읽어서</b> 얹습니다 — 매 프레임 위치와 현재 크기를
    /// 가져와, 그 크기가 만드는 고리 위에 알갱이를 하나씩 놓습니다. 호가 커지면 우리 알갱이가
    /// 놓이는 자리도 같이 커지므로 어긋날 수가 없습니다.
    ///
    /// 앞서 시도한 두 가지는 이 참격에 성립하지 않았습니다.
    /// - 메시를 파티클 셰이프에 물려 주기 → 물려 줄 메시가 없습니다.
    /// - 렌더러 바운즈를 셰이프로 쓰기 → 파티클 렌더러의 바운즈는 시뮬레이션 전체 범위라
    ///   실측 40m가 나왔습니다. 그 안에 뿌리면 화면 전체에 흩뿌리는 것과 같습니다.
    ///
    /// 알갱이는 참격의 자식으로 달지 않습니다. 참격은 짧게 살고 사라지는데, 자식으로
    /// 달아 두면 이미 흩날린 알갱이까지 그 순간 같이 사라집니다.
    /// </summary>
    public static class MeleeSlashSystem
    {
        /// <summary>동시에 살아 있을 수 있는 흩뿌림 수. 넘으면 이번 참격은 건너뜁니다.</summary>
        private const int MaxLiveBursts = 16;

        private const string HolderName = "WeaponAura_MeleeSlashes";
        private const string BurstName = "WeaponAura_MeleeSlash";

        /// <summary>따라갈 참격이 없을 때 쓰는 부채꼴을 세로로 얼마나 눌러 놓을지.</summary>
        private const float FanFlatten = 0.22f;

        /// <summary>
        /// 고리의 안쪽 경계. 1이면 호의 바깥 테두리에만 정확히 붙고, 낮출수록 안쪽까지
        /// 퍼집니다. 테두리에만 붙이면 선처럼 보여서 조금 두께를 줍니다.
        /// </summary>
        private const float RingInner = 0.72f;

        /// <summary>상태 표시용 폴링 간격(초)</summary>
        private const float StatusInterval = 0.25f;

        internal sealed class BurstHandle
        {
            public GameObject Go = null!;
            public ParticleSystem Sparks = null!;
            public ParticleSystemRenderer SparkRenderer = null!;

            /// <summary>따라갈 참격. null이면 제자리에서 사그라지는 중입니다.</summary>
            public Transform? Follow;

            /// <summary>얹어 갈 게임 참격의 파티클 시스템. null이면 부채꼴로 한 번 터뜨린 것입니다.</summary>
            public ParticleSystem? Source;

            /// <summary>지금 이 흩뿌림이 쓰는 설정. 설정 창에서 값을 바꾸면 바로 반영됩니다.</summary>
            public MeleeSlashProfile? Profile;

            /// <summary>아직 내보내야 할 알갱이 수</summary>
            public int Remaining;

            /// <summary>초당 몇 개를 내보낼지 (개수 ÷ 뿌리는 시간)</summary>
            public float Rate;

            /// <summary>1개 미만의 몫을 다음 프레임으로 넘기는 누적값</summary>
            public float Pending;

            public float RecycleAt;
        }

        private static GameObject? _holder;
        private static readonly Stack<BurstHandle> _idle = new Stack<BurstHandle>();
        private static readonly List<BurstHandle> _live = new List<BurstHandle>();

        /// <summary>게임 참격의 알갱이를 읽어 올 버퍼. 매 프레임 할당하지 않도록 재사용합니다.</summary>
        private static ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[32];

        private static float? _colorScale;
        private static float _nextStatusTime;
        private static bool _loggedFirstSlash;
        private static bool _loggedFirstRing;
        private static bool _loggedFirstTint;

        // ── 상태 조회 (설정 창 표시용) ────────────────────────────
        /// <summary>지금 든 근접무기 이름</summary>
        public static string CurrentWeaponName { get; private set; } = "-";
        /// <summary>지금 든 근접무기 등급 (근접무기가 아니면 -1)</summary>
        public static int CurrentWeaponQuality { get; private set; } = -1;
        /// <summary>무기가 바뀔 때마다 올라갑니다 (설정 창이 상태 줄을 다시 그릴지 판단하는 용도).</summary>
        public static int WeaponRevision { get; private set; }
        /// <summary>지금 살아 있는 흩뿌림 수 (진단용)</summary>
        public static int LiveCount => _live.Count;

        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// 게임 참격에 흩뿌림을 얹습니다. 실제 배치는 <see cref="TickRing"/>이 매 프레임 합니다 —
        /// 호가 커지는 것을 따라가야 하므로 한 번에 정할 수가 없습니다.
        /// </summary>
        public static void Attach(GameObject? slash, MeleeSlashProfile? profile)
        {
            if (slash == null || profile == null || !profile.enabled || !MeleeSlashSettings.Enabled)
                return;

            if (profile.sparkCount <= 0)
                return;

            try
            {
                var source = FindSourceSystem(slash);

                // 얹어 갈 알갱이가 없으면 위치를 알 방법이 없습니다.
                // 그 자리에서 부채꼴로 뿌리는 예전 방식으로 물러납니다.
                if (source == null)
                {
                    SpawnAt(slash.transform.position, slash.transform.rotation, profile);
                    return;
                }

                if (_live.Count >= MaxLiveBursts)
                    return;

                var handle = Rent();
                if (handle == null)
                    return;

                var origin = source.transform;
                handle.Go.transform.SetPositionAndRotation(origin.position, origin.rotation);
                handle.Go.SetActive(true);

                Configure(handle, profile, ring: true);

                handle.Follow = origin;
                handle.Source = source;
                handle.Profile = profile;

                // 뿌리는 시간은 참격이 살아 있는 시간을 넘을 수 없습니다.
                // 넘기면 호가 사라진 뒤에도 남은 몫을 못 내보내고 그냥 끝납니다.
                float window = Mathf.Max(0.01f, profile.sparkEmitWindow);
                float sourceLife = SourceLifetime(source);
                if (sourceLife > 0.01f)
                    window = Mathf.Min(window, sourceLife);

                handle.Remaining = profile.sparkCount;
                handle.Rate = profile.sparkCount / window;
                handle.Pending = 0f;
                handle.RecycleAt = Time.time + window + Mathf.Max(0.05f, profile.sparkDuration) + 0.1f;

                handle.Sparks.Clear();
                handle.Sparks.Play();

                _live.Add(handle);
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 근접 흩뿌림 부착 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 참격 없이 그 자리에서 부채꼴로 한 번 뿌립니다.
        /// 게임 참격을 지우는 <c>흩뿌림만</c> 모드와, 얹어 갈 알갱이를 못 찾았을 때의 길입니다.
        /// </summary>
        public static void SpawnAt(Vector3 position, Quaternion rotation, MeleeSlashProfile? profile)
        {
            if (profile == null || !profile.enabled || !MeleeSlashSettings.Enabled)
                return;

            if (profile.sparkCount <= 0)
                return;

            try
            {
                if (_live.Count >= MaxLiveBursts)
                    return;

                var handle = Rent();
                if (handle == null)
                    return;

                handle.Go.transform.SetPositionAndRotation(position, rotation);
                handle.Go.SetActive(true);

                Configure(handle, profile, ring: false);

                handle.Follow = null;
                handle.Source = null;
                handle.Profile = profile;
                handle.Remaining = 0;

                handle.Sparks.Clear();
                handle.Sparks.Play();
                handle.Sparks.Emit(profile.sparkCount);

                handle.RecycleAt = Time.time + Mathf.Max(0.05f, profile.sparkDuration) + 0.1f;

                _live.Add(handle);
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 근접 흩뿌림 생성 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 게임 참격을 그리는 파티클 시스템. 여러 개면 알갱이가 가장 큰 쪽이 호 본체입니다.
        /// </summary>
        private static ParticleSystem? FindSourceSystem(GameObject slash)
        {
            ParticleSystem? best = null;
            float bestSize = -1f;

            foreach (var system in slash.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system == null)
                    continue;

                float size = system.main.startSize.constantMax;
                if (size <= bestSize)
                    continue;

                best = system;
                bestSize = size;
            }

            return best;
        }

        private static float SourceLifetime(ParticleSystem source)
        {
            try
            {
                return source.main.startLifetime.constantMax;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// 참격 호의 <b>모양</b>을 바꿔 끼웁니다.
        ///
        /// 게임 참격은 파티클 하나를 메시로 그리는 물건이라, 그 메시가 곧 호의 모양입니다.
        /// 다른 메시를 꽂으면 휘두를 때 나오는 모양 자체가 바뀝니다.
        /// 원본이 있던 평면·크기에 맞춰 만들기 때문에 무기가 달라도 자리를 벗어나지 않습니다.
        /// </summary>
        public static void ApplyShape(GameObject? slash, MeleeSlashProfile? profile)
        {
            if (slash == null || profile == null || string.IsNullOrEmpty(profile.slashTexture))
                return;

            try
            {
                var texture = ResolveSlashTexture(profile);
                if (texture == null)
                    return;

                foreach (var renderer in slash.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || renderer.sharedMaterial == null)
                        continue;

                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);

                    bool touched = false;
                    foreach (string property in TextureProperties)
                    {
                        if (!renderer.sharedMaterial.HasProperty(property))
                            continue;

                        block.SetTexture(property, texture);
                        touched = true;
                    }

                    if (touched)
                        renderer.SetPropertyBlock(block);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 참격 모양 교체 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>셰이더마다 주 텍스처 이름이 다릅니다. URP는 _BaseMap, 레거시는 _MainTex.</summary>
        private static readonly string[] TextureProperties = { "_BaseMap", "_MainTex" };

        /// <summary>
        /// 참격에 씌울 텍스처. 총구 화염과 같은 목록을 씁니다 —
        /// 내장 도형 · 직접 그린 도형 · assets/vfx_textures의 PNG.
        /// </summary>
        public static Texture2D? ResolveSlashTexture(MeleeSlashProfile? profile)
        {
            return profile != null ? MuzzleFlashShapes.ResolveByName(profile.slashTexture) : null;
        }

        /// <summary>
        /// 게임이 방금 만든 참격의 <b>형태는 그대로 두고</b> 색과 크기만 바꿉니다.
        /// 실제 물들이기는 총구 화염과 같은 <see cref="EffectTint"/>가 합니다.
        /// </summary>
        public static void TintExisting(GameObject? slash, MeleeSlashProfile? profile)
        {
            if (slash == null || profile == null)
                return;

            try
            {
                BulletTrailShading.Resolve(profile.slashColor, profile.slashIntensity, profile.slashAlpha,
                    out Color color, out float alpha);

                // 참격은 알갱이가 한 장뿐이라 두 색을 섞을 수가 없습니다. 한 색으로 확실하게.
                //
                // 크기는 건드리지 않습니다. 참격 호의 크기는 그 무기의 공격 사거리를 그대로
                // 보여 주는 정보라, 색 때문에 늘리거나 줄이면 실제 닿는 거리와 화면이
                // 어긋나서 플레이어를 속이게 됩니다.
                EffectTint.ApplySolid(slash, color, alpha);

                LogFirstTint(slash, color, alpha);
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 근접 참격 색 변경 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// 참격에 실제로 어떤 색을 밀어 넣었는지 한 번 남깁니다.
        /// 화면이 그대로면 "안 걸렸다"인지 "걸렸는데 흰색에 가깝다"인지 여기서 갈립니다.
        /// </summary>
        private static void LogFirstTint(GameObject slash, Color color, float alpha)
        {
            if (_loggedFirstTint)
                return;

            _loggedFirstTint = true;

            var detail = new System.Text.StringBuilder();
            foreach (var renderer in slash.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);

                detail.Append($" | '{renderer.gameObject.name}' 적용후_BaseColor=" +
                              (renderer.sharedMaterial.HasProperty("_BaseColor")
                                  ? block.GetColor("_BaseColor").ToString()
                                  : "없음"));
            }

            UnityEngine.Debug.Log(
                $"[WeaponAura] 근접 참격 색 적용: 색={color.ToString()} 알파={alpha:0.###}" + detail);
        }

        /// <summary>
        /// 첫 참격 때 게임 참격이 실제로 어떻게 생겼는지 한 줄 남깁니다.
        ///
        /// 이 로그가 "메시가 아니라 파티클 하나"라는 사실을 알려 줘서 방식을 바꿀 수 있었습니다.
        /// 무기마다 다른 물건이라 앞으로도 같은 판단이 필요합니다.
        /// 방식과 무관하게 남겨야 하므로 패치가 직접 부릅니다.
        /// </summary>
        public static void LogFirstSlash(GameObject? slash, MeleeSlashProfile? profile)
        {
            if (_loggedFirstSlash || slash == null || profile == null)
                return;

            _loggedFirstSlash = true;

            string shapeName = string.IsNullOrEmpty(profile.textureName)
                ? profile.shape.ToString()
                : profile.textureName;

            var source = FindSourceSystem(slash);
            string sourceInfo = source != null
                ? $"'{source.gameObject.name}' 시작크기={source.main.startSize.constantMax:0.###} " +
                  $"수명={SourceLifetime(source):0.###}s 공간={source.main.simulationSpace} " +
                  $"렌더={source.GetComponent<ParticleSystemRenderer>()?.renderMode.ToString() ?? "?"}"
                : "없음(부채꼴로 대체)";

            UnityEngine.Debug.Log(
                $"[WeaponAura] 게임 근접 참격 실측: '{slash.name}' {EffectTint.Describe(slash)} " +
                $"| 얹어 갈 파티클={sourceInfo} " +
                $"| 방식={MeleeSlashSettings.Mode} 등급 {profile.grade}({profile.name}) 모양={shapeName} " +
                $"알갱이={profile.sparkCount}x{profile.sparkSize:0.###}m " +
                $"뿌리는시간={profile.sparkEmitWindow:0.###}s 지속={profile.sparkDuration:0.###}s");
        }

        /// <summary>ModBehaviour.Update에서 호출 — 설정 창에 보여 줄 무기 정보만 읽습니다.</summary>
        public static void Tick()
        {
            if (Time.unscaledTime < _nextStatusTime)
                return;

            _nextStatusTime = Time.unscaledTime + StatusInterval;

            try
            {
                var player = CharacterMainControl.Main;
                var melee = player != null ? player.GetMeleeWeapon() : null;
                Item? item = melee != null ? melee.Item : null;

                if (item == null)
                {
                    SetWeapon("-", -1);
                    return;
                }

                SetWeapon(WeaponHelper.GetDisplayName(item), WeaponHelper.GetQuality(item));
            }
            catch
            {
                SetWeapon("-", -1);
            }
        }

        private static void SetWeapon(string name, int quality)
        {
            if (CurrentWeaponQuality == quality &&
                string.Equals(CurrentWeaponName, name, StringComparison.Ordinal))
                return;

            CurrentWeaponName = name;
            CurrentWeaponQuality = quality;
            WeaponRevision++;
        }

        /// <summary>
        /// ModBehaviour.LateUpdate에서 호출 — 호가 커지는 것을 따라가며 알갱이를 얹습니다.
        ///
        /// LateUpdate여야 합니다. 참격은 캐릭터에 붙어 있어서 캐릭터가 움직이는 만큼 같이
        /// 움직이는데, Update에서 읽으면 프레임에 따라 한 박자 뒤처집니다.
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
                    // Unity의 == 는 파괴된 오브젝트도 null로 봅니다 — 여기서는 그게 맞는 판정입니다.
                    var slash = handle.Follow;
                    if (slash != null && slash.gameObject.activeInHierarchy)
                    {
                        handle.Go.transform.SetPositionAndRotation(slash.position, slash.rotation);
                    }
                    else
                    {
                        // 참격이 먼저 사라졌습니다. 얹어 갈 호가 없으니 여기서 멈춥니다.
                        handle.Follow = null;
                        handle.Source = null;
                        handle.Remaining = 0;
                    }
                }

                TickRing(handle);

                if (Time.time >= handle.RecycleAt)
                {
                    Recycle(handle);
                    _live.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 이번 프레임 몫의 알갱이를 게임 참격의 호 위에 놓습니다.
        ///
        /// 위치는 게임 알갱이에서 그대로 읽습니다 — 우리가 캐릭터 위치와 각도로 다시 계산하면
        /// 게임이 쓰는 오프셋과 미묘하게 어긋나서, 결국 따로 노는 것으로 보입니다.
        /// </summary>
        private static void TickRing(BurstHandle handle)
        {
            if (handle.Remaining <= 0 || handle.Source == null || handle.Profile == null)
                return;

            var source = handle.Source;
            var profile = handle.Profile;

            int count = source.particleCount;
            if (count <= 0)
                return;     // 아직 안 나왔습니다. 다음 프레임에 다시 봅니다.

            if (_particles.Length < count)
                _particles = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];

            int read = source.GetParticles(_particles);
            if (read <= 0)
                return;

            // 가장 큰 알갱이가 호 본체입니다.
            int best = 0;
            float bestSize = -1f;
            for (int i = 0; i < read; i++)
            {
                float size = _particles[i].GetCurrentSize(source);
                if (size <= bestSize)
                    continue;

                best = i;
                bestSize = size;
            }

            if (bestSize <= 0.0001f)
                return;

            var toWorld = ParticleMatrix(source, _particles[best], bestSize);
            Vector3 center = toWorld.MultiplyPoint3x4(Vector3.zero);

            // 이번 프레임 몫. 1개가 안 되면 다음 프레임으로 넘깁니다.
            handle.Pending += handle.Rate * Time.unscaledDeltaTime;

            int emit = Mathf.FloorToInt(handle.Pending);
            if (emit <= 0)
                return;

            handle.Pending -= emit;
            emit = Mathf.Min(emit, handle.Remaining);
            handle.Remaining -= emit;

            float life = Mathf.Max(0.05f, profile.sparkDuration);
            float speed = Mathf.Max(0f, profile.sparkDistance) / life;
            float scatter = Mathf.Clamp01(profile.sparkScatter);
            float ring = Mathf.Max(0f, profile.sparkRing);

            // 참격은 1×1 쿼드 한 장이고 호는 그 위의 <b>텍스처</b>에 그려져 있습니다.
            // 그래서 메시 정점(모서리 4개)은 얹을 자리가 못 됩니다 — 모서리에만 찍힙니다.
            // 쿼드가 누운 평면 안에서 호를 그리며 뿌립니다. 쿼드 로컬 좌표라 프리팹이
            // 어느 방향으로 눕든 상관없습니다.
            float halfArc = Mathf.Clamp(profile.sparkArc * 0.5f, 0f, 180f) * Mathf.Deg2Rad;
            float facing = profile.slashFacing * Mathf.Deg2Rad;

            LogFirstRing(handle, center, bestSize, toWorld, ring);

            for (int i = 0; i < emit; i++)
            {
                float angle = facing + UnityEngine.Random.Range(-halfArc, halfArc);

                // 쿼드는 -0.5~0.5 범위라 반지름 0.5가 테두리입니다.
                float radius = 0.5f * ring * UnityEngine.Random.Range(RingInner, 1f);

                var localPoint = new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, 0f);
                var position = toWorld.MultiplyPoint3x4(localPoint);

                // 호 바깥으로 밀려나면서, 흩어짐만큼 방향이 흐트러집니다.
                var outward = position - center;
                outward = outward.sqrMagnitude > 0.000001f ? outward.normalized : UnityEngine.Random.onUnitSphere;

                var velocity = outward * speed
                               + UnityEngine.Random.insideUnitSphere * (speed * scatter);

                var parameters = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity,
                    applyShapeToPosition = false,
                };

                handle.Sparks.Emit(parameters, 1);
            }
        }

        /// <summary>
        /// 알갱이 한 장이 실제로 그려지는 변환(월드 기준).
        ///
        /// 크기를 알갱이 크기만으로 잡으면 안 됩니다 — 그건 시스템 좌표계 기준이고,
        /// 로컬 시뮬레이션이면 트랜스폼 스케일이 한 번 더 곱해집니다. 그걸 빼먹어서
        /// 알갱이가 캐릭터 발밑에 몰렸습니다. 트랜스폼 행렬로 한 번에 합성합니다.
        /// </summary>
        private static Matrix4x4 ParticleMatrix(ParticleSystem source,
            ParticleSystem.Particle particle, float size)
        {
            var localMatrix = Matrix4x4.TRS(
                particle.position, Quaternion.Euler(particle.rotation3D), Vector3.one * size);

            if (source.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                return source.transform.localToWorldMatrix * localMatrix;

            // 월드 시뮬레이션이면 위치·회전은 이미 월드이고, 크기만 스케일링 모드를 탑니다.
            var scale = source.main.scalingMode == ParticleSystemScalingMode.Hierarchy
                ? source.transform.lossyScale
                : Vector3.one;

            return Matrix4x4.TRS(particle.position, Quaternion.Euler(particle.rotation3D),
                new Vector3(scale.x * size, scale.y * size, scale.z * size));
        }

        /// <summary>
        /// 지금 그려지고 있는 참격 판의 자리와 방향.
        /// 미리보기 카메라가 그 판을 정면으로 보기 위해 씁니다 — 판이 바닥에 누워 있으면
        /// 옆에서 찍을 때 선 하나로 보여서 "안 보인다"가 됩니다.
        /// </summary>
        public static bool TryGetSlashFrame(GameObject? slash, out Vector3 center, out Quaternion rotation,
            out float radius)
        {
            center = Vector3.zero;
            rotation = Quaternion.identity;
            radius = 0f;

            if (slash == null)
                return false;

            try
            {
                var source = FindSourceSystem(slash);
                if (source == null || source.particleCount <= 0)
                    return false;

                if (_particles.Length < source.particleCount)
                    _particles = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(source.particleCount)];

                int read = source.GetParticles(_particles);
                if (read <= 0)
                    return false;

                var matrix = ParticleMatrix(source, _particles[0], _particles[0].GetCurrentSize(source));

                center = matrix.MultiplyPoint3x4(Vector3.zero);
                rotation = Quaternion.LookRotation(
                    matrix.MultiplyVector(Vector3.forward), matrix.MultiplyVector(Vector3.up));

                // 쿼드는 -0.5~0.5 범위입니다. 그 반폭을 월드 단위로 재면 곧 호의 크기입니다 —
                // 미리보기 화면을 여기에 맞춰야 합니다. 상수로 짐작하면 실제 참격이 그보다
                // 크거나 작을 때 화면 밖으로 나가거나 점처럼 보입니다.
                radius = matrix.MultiplyVector(new Vector3(0.5f, 0f, 0f)).magnitude;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 첫 흩뿌림에서 실제로 어느 자리에 얼마만 한 고리로 얹었는지 한 번 남깁니다.
        /// 크기가 터무니없으면(예전 바운즈 폴백의 40m처럼) 이 줄에서 바로 드러납니다.
        /// </summary>
        private static void LogFirstRing(BurstHandle handle, Vector3 center, float rawSize,
            Matrix4x4 toWorld, float ring)
        {
            if (_loggedFirstRing)
                return;

            _loggedFirstRing = true;

            // 쿼드 테두리까지의 실제 월드 거리. 이 값이 참격 크기와 안 맞으면 바로 드러납니다.
            float edge = toWorld.MultiplyVector(new Vector3(0.5f * ring, 0f, 0f)).magnitude;
            var normal = toWorld.MultiplyVector(Vector3.forward).normalized;

            UnityEngine.Debug.Log(
                $"[WeaponAura] 근접 흩뿌림 자리: 중심={center.ToString("0.##")} " +
                $"알갱이크기={rawSize:0.###} 호반지름={edge:0.###}m " +
                $"판법선={normal.ToString("0.##")} 초당={handle.Rate:0.#}개");
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

        private static BurstHandle? Rent()
        {
            EnsureHolder();
            if (_holder == null)
                return null;

            while (_idle.Count > 0)
            {
                var reused = _idle.Pop();
                if (reused.Go != null && reused.Sparks != null)
                    return reused;
            }

            return CreateBurstObject(_holder.transform, active: false);
        }

        private static BurstHandle CreateBurstObject(Transform parent, bool active)
        {
            var go = new GameObject(BurstName);
            go.transform.SetParent(parent, false);
            go.SetActive(active);

            var sparks = CreateSystem(go.transform, "Sparks");

            return new BurstHandle
            {
                Go = go,
                Sparks = sparks,
                SparkRenderer = sparks.GetComponent<ParticleSystemRenderer>(),
            };
        }

        /// <summary>
        /// 설정 창 미리보기에서 쓰는 이펙트 한 벌.
        ///
        /// 풀에서 빌려 쓰지 않고 따로 만듭니다 — 풀 오브젝트는 게임 화면에 나가는 것이라
        /// 미리보기 전용 레이어로 옮기면 실제 전투에서 안 보이게 됩니다.
        /// 대신 <see cref="Configure"/>·<see cref="TickRing"/>으로 런타임과 <b>같은 함수</b>를 태웁니다.
        /// </summary>
        public sealed class PreviewEmitter
        {
            internal BurstHandle Handle = null!;

            public GameObject Root => Handle.Go;
        }

        public static PreviewEmitter CreatePreviewEmitter(Transform parent)
        {
            return new PreviewEmitter { Handle = CreateBurstObject(parent, active: true) };
        }

        /// <summary>
        /// 미리보기 이펙트를 한 번 터뜨립니다 (런타임과 같은 설정·같은 배치를 씁니다).
        /// </summary>
        /// <param name="slash">무대에 세워 둔 게임 참격. 있으면 그 호 위에 얹습니다.</param>
        public static void PreviewEmit(PreviewEmitter? emitter, MeleeSlashProfile? profile, GameObject? slash)
        {
            if (emitter == null || profile == null || emitter.Handle.Go == null)
                return;

            var handle = emitter.Handle;
            var source = slash != null ? FindSourceSystem(slash) : null;

            Configure(handle, profile, ring: source != null);

            handle.Sparks.Clear();

            if (profile.sparkCount <= 0)
            {
                handle.Sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            handle.Sparks.Play();

            if (source == null)
            {
                // 참격이 없는 미리보기(흩뿌림만 모드)는 부채꼴로 한 번 터뜨립니다.
                handle.Source = null;
                handle.Follow = null;
                handle.Remaining = 0;
                handle.Sparks.Emit(profile.sparkCount);
                return;
            }

            var origin = source.transform;
            handle.Go.transform.SetPositionAndRotation(origin.position, origin.rotation);

            handle.Follow = origin;
            handle.Source = source;
            handle.Profile = profile;

            float window = Mathf.Max(0.01f, profile.sparkEmitWindow);
            float sourceLife = SourceLifetime(source);
            if (sourceLife > 0.01f)
                window = Mathf.Min(window, sourceLife);

            handle.Remaining = profile.sparkCount;
            handle.Rate = profile.sparkCount / window;
            handle.Pending = 0f;
        }

        /// <summary>
        /// 미리보기 무대가 매 프레임 부릅니다 — 무대의 참격도 호가 커지므로,
        /// 실제와 똑같이 프레임마다 얹어 줘야 합니다.
        /// </summary>
        public static void PreviewTick(PreviewEmitter? emitter)
        {
            if (emitter == null || emitter.Handle.Go == null)
                return;

            TickRing(emitter.Handle);
        }

        /// <summary>
        /// 미리보기 무대의 참격을 치우기 <b>전에</b> 부릅니다.
        /// 파괴된 파티클 시스템을 계속 읽으면 다음 프레임부터 예외가 납니다.
        /// </summary>
        public static void PreviewDetach(PreviewEmitter? emitter)
        {
            if (emitter == null)
                return;

            emitter.Handle.Source = null;
            emitter.Handle.Follow = null;
            emitter.Handle.Remaining = 0;
        }

        /// <summary>
        /// 파티클 시스템 한 벌. 방출은 우리가 <c>Emit</c>으로 직접 합니다
        /// (자리를 하나하나 지정해야 하므로 emission rate로는 맞출 수 없습니다).
        /// </summary>
        private static ParticleSystem CreateSystem(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;

            // 설정 창이 떠 있는 동안 게임은 timeScale 0입니다. 스케일된 시간을 쓰면
            // 미리보기 파티클이 첫 프레임에서 얼어붙습니다.
            main.useUnscaledTime = true;

            // 알갱이는 월드 공간에서 날아가야 합니다. 참격에 매어 두면 이미 흩날린 것까지
            // 호가 커질 때마다 같이 끌려다녀서, 잔상이 아니라 덩어리로 보입니다.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 256;

            // 자리를 EmitParams로 직접 주므로 셰이프는 쓰지 않습니다.
            var emission = system.emission;
            emission.enabled = false;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = WeaponAuraResources.SharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2.2f;

            return system;
        }

        /// <param name="ring">참격 호 위에 얹는 중인지. 아니면 부채꼴 셰이프를 씁니다.</param>
        private static void Configure(BurstHandle handle, MeleeSlashProfile profile, bool ring)
        {
            float scale = ColorScale;

            BulletTrailShading.Resolve(profile.colorInner, profile.intensity, profile.alpha,
                out Color inner, out float innerAlpha);
            BulletTrailShading.Resolve(profile.colorOuter, profile.intensity, profile.alpha,
                out Color outer, out float outerAlpha);

            float alpha = Mathf.Min(innerAlpha, outerAlpha) * scale;
            inner *= scale;
            outer *= scale;

            float life = Mathf.Max(0.05f, profile.sparkDuration);

            // 고른 도형(또는 사용자 PNG)으로 갈아 끼웁니다. 총구 화염과 같은 목록을 씁니다.
            var texture = MuzzleFlashShapes.Resolve(profile.shape, profile.textureName);
            handle.SparkRenderer.sharedMaterial = MuzzleFlashShapes.GetMaterial(texture);

            // 칼날 파편은 늘어나야 그럴듯하지만, 하트·별은 늘어나면 알아볼 수 없습니다.
            handle.SparkRenderer.renderMode = profile.sparkStretch
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;

            var main = handle.Sparks.main;
            main.startLifetime = life;
            main.startSize = Mathf.Max(0.005f, profile.sparkSize);
            main.startColor = EffectTint.WithAlpha(inner, alpha);

            // 고리에 얹을 때는 속도를 알갱이마다 직접 줍니다(고리 바깥 방향).
            // 부채꼴로 터뜨릴 때만 시작 속도가 쓰입니다.
            main.startSpeed = ring ? 0f : Mathf.Max(0f, profile.sparkDistance) / life;

            // 중력만 받는 물체의 이동량은 Δy = -½·9.81·g·t² 입니다.
            // 도달 높이를 정해 두고 g를 역산하면 수명이 길어져도 그 높이를 넘지 않습니다.
            main.gravityModifier = -profile.sparkRise / (4.905f * life * life);

            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var rotation = handle.Sparks.rotationOverLifetime;
            bool spins = Mathf.Abs(profile.sparkSpin) > 0.01f;
            rotation.enabled = spins;
            if (spins)
                rotation.z = profile.sparkSpin * Mathf.Deg2Rad;

            var shape = handle.Sparks.shape;
            if (ring)
            {
                // 자리를 우리가 직접 주므로 셰이프가 끼어들면 안 됩니다.
                shape.enabled = false;
            }
            else
            {
                // 휘두른 방향으로 납작한 부채꼴. 원뿔이면 폭발처럼 보입니다.
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = Mathf.Clamp(profile.sparkArc * 0.5f, 0f, 90f);
                shape.radius = 0.05f;
                shape.position = Vector3.zero;
                shape.rotation = Vector3.zero;
                shape.scale = new Vector3(1f, FanFlatten, 1f);
                shape.randomDirectionAmount = Mathf.Clamp01(profile.sparkScatter);
            }

            var color = handle.Sparks.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(BuildSparkGradient(inner, outer));

            var size = handle.Sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        }

        /// <summary>칼날 쪽의 밝은 색에서 등급 색으로 번지며 사라집니다.</summary>
        private static Gradient BuildSparkGradient(Color inner, Color outer)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(inner, 0f),
                    new GradientColorKey(outer, 0.5f),
                    new GradientColorKey(outer, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.7f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        /// <summary>
        /// 레거시 <c>Particles/Additive</c>는 최종 색을 2배로 냅니다. 그대로 넣으면
        /// 채널값 0.5만 넘어도 화면에서 1에 붙어 알갱이가 통째로 흰색이 됩니다.
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

        private static void Recycle(BurstHandle handle)
        {
            handle.Follow = null;
            handle.Source = null;
            handle.Profile = null;
            handle.Remaining = 0;

            if (handle.Go == null)
                return;

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
