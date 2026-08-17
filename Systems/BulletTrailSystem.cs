using System;
using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using WeaponAura.Helpers;
using WeaponAura.Settings;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 날아가는 총알에 등급 색 잔상을 붙이는 시스템.
    ///
    /// 총알(<see cref="Projectile"/>)은 풀에서 재사용되고, 맞는 순간 GameObject가
    /// 통째로 비활성화됩니다. 그래서 잔상을 총알의 자식으로 달면 명중과 동시에 꼬리가
    /// 뚝 끊깁니다. 대신 잔상 오브젝트를 따로 두고 매 프레임 총알 위치만 따라가게 해서,
    /// 총알이 사라진 뒤에도 남은 꼬리가 제자리에서 자연스럽게 사라지도록 합니다.
    ///
    /// 잔상 오브젝트는 풀링합니다 — 연사 무기는 초당 수십 발을 쏘기 때문에
    /// 매번 만들고 버리면 그대로 GC 부담이 됩니다.
    /// </summary>
    public static class BulletTrailSystem
    {
        /// <summary>동시에 살아 있을 수 있는 잔상 수. 넘으면 새 총알은 잔상 없이 나갑니다.</summary>
        private const int MaxLiveTrails = 96;

        private const string HolderName = "WeaponAura_BulletTrails";
        private const string TrailName = "WeaponAura_BulletTrail";

        /// <summary>상태 표시용 폴링 간격(초)</summary>
        private const float StatusInterval = 0.25f;

        private sealed class TrailHandle
        {
            public GameObject Go = null!;
            public TrailRenderer Trail = null!;

            /// <summary>따라가는 총알. null이면 사라지는 중입니다.</summary>
            public Projectile? Follow;

            /// <summary>사라지는 중일 때, 이 시각이 지나면 회수합니다.</summary>
            public float RecycleAt;
        }

        private static GameObject? _holder;
        private static readonly Stack<TrailHandle> _idle = new Stack<TrailHandle>();
        private static readonly List<TrailHandle> _live = new List<TrailHandle>();

        /// <summary>지금 어떤 총알을 어떤 잔상이 따라가는지 (총알 재사용 감지용)</summary>
        private static readonly Dictionary<Projectile, TrailHandle> _followed =
            new Dictionary<Projectile, TrailHandle>();

        private static Material? _additiveMaterial;
        private static Material? _blendMaterial;
        private static Texture2D? _streakTexture;

        private static float _nextStatusTime;

        // ── 상태 조회 (설정 창 표시용) ────────────────────────────
        /// <summary>지금 든 총에 들어 있는 탄환 이름</summary>
        public static string CurrentAmmoName { get; private set; } = "-";
        /// <summary>지금 든 총에 들어 있는 탄환 등급 (없으면 -1)</summary>
        public static int CurrentAmmoQuality { get; private set; } = -1;
        /// <summary>탄약이 바뀔 때마다 올라갑니다 (설정 창이 상태 줄을 다시 그릴지 판단하는 용도).</summary>
        public static int AmmoRevision { get; private set; }
        /// <summary>지금 살아 있는 잔상 수 (진단용)</summary>
        public static int LiveCount => _live.Count;

        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// 총알 하나에 잔상을 붙입니다. 발사 패치에서 총알이 초기화된 직후에 부릅니다.
        /// </summary>
        public static void Apply(Projectile? projectile, int ammoQuality)
        {
            if (projectile == null || !BulletTrailSettings.Enabled)
                return;

            try
            {
                var profile = BulletTrailProfiles.Resolve(ammoQuality);
                if (profile == null || !profile.enabled)
                    return;

                // 같은 총알을 풀에서 곧바로 다시 꺼내 쏜 경우, 이전 잔상은 여기서 끊어 줍니다.
                // (안 그러면 새 총알을 따라가면서 지난 궤적이 이어져 화면을 가로지릅니다)
                if (_followed.TryGetValue(projectile, out var previous))
                    BeginFade(previous);

                if (_live.Count >= MaxLiveTrails)
                    return;

                var handle = Rent();
                if (handle == null)
                    return;

                Configure(handle, profile);

                handle.Go.transform.position = projectile.transform.position;
                handle.Go.transform.rotation = projectile.transform.rotation;
                handle.Go.SetActive(true);

                // 위치를 옮긴 뒤에 지워야 이전 궤적이 남지 않습니다.
                handle.Trail.Clear();
                handle.Trail.emitting = true;

                handle.Follow = projectile;
                handle.RecycleAt = 0f;

                _live.Add(handle);
                _followed[projectile] = handle;
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 탄환 잔상 생성 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>ModBehaviour.Update에서 호출 — 설정 창에 보여 줄 탄약 정보만 읽습니다.</summary>
        public static void Tick()
        {
            TickStatus();
        }

        /// <summary>
        /// ModBehaviour.LateUpdate에서 호출 — 총알 위치를 따라갑니다.
        ///
        /// 반드시 LateUpdate여야 합니다. 총알은 자기 Update에서 움직이는데 스크립트 실행 순서는
        /// 정해져 있지 않아서, Update에서 따라가면 프레임에 따라 꼬리가 총알보다 한 프레임 뒤처집니다.
        /// 총알이 초당 수십 미터로 날아가므로 한 프레임이면 눈에 띄게 어긋납니다.
        /// </summary>
        public static void LateTick()
        {
            TickTrails();
        }

        /// <summary>잔상을 모두 걷어냅니다 (모드 비활성화·설정 끄기).</summary>
        public static void Clear()
        {
            try
            {
                foreach (var handle in _live)
                    Recycle(handle);

                _live.Clear();
                _followed.Clear();
            }
            catch
            {
                // 정리 중 예외는 무시합니다.
            }
        }

        /// <summary>모드가 내려갈 때 만들어 둔 오브젝트까지 전부 정리합니다.</summary>
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

        // ── 매 프레임 갱신 ────────────────────────────────────────

        /// <summary>
        /// 총알을 따라가고, 총알이 사라진 잔상은 남은 꼬리가 다 없어질 때까지 두었다가 회수합니다.
        /// </summary>
        private static void TickTrails()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var handle = _live[i];

                // 씬 정리 등으로 잔상 오브젝트가 먼저 사라진 경우. 그냥 두면 매 프레임
                // 파괴된 transform을 건드리다 예외가 나면서 나머지 잔상도 갱신되지 않습니다.
                if (handle.Go == null || handle.Trail == null)
                {
                    if (!ReferenceEquals(handle.Follow, null))
                    {
                        _followed.Remove(handle.Follow);
                        handle.Follow = null;
                    }

                    _live.RemoveAt(i);
                    continue;
                }

                if (!ReferenceEquals(handle.Follow, null))
                {
                    // Unity의 == 는 파괴된 오브젝트도 null로 봅니다 — 여기서는 그게 맞는 판정입니다.
                    var projectile = handle.Follow;
                    bool alive = projectile != null && projectile.gameObject.activeInHierarchy;

                    if (alive)
                        handle.Go.transform.position = projectile!.transform.position;
                    else
                        BeginFade(handle);
                }

                if (ReferenceEquals(handle.Follow, null) && Time.time >= handle.RecycleAt)
                {
                    Recycle(handle);
                    _live.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 지금 든 총의 탄환 정보를 설정 창에 보여 주기 위해 낮은 빈도로 읽습니다.
        ///
        /// 스케일이 걸리지 않은 시간을 씁니다. 설정 창이 떠 있는 동안 게임은 시간이 멈춘 상태라
        /// <c>Time.time</c>은 아예 흐르지 않고, 그러면 창에 뜬 탄약 정보가 영원히 갱신되지 않습니다.
        /// </summary>
        private static void TickStatus()
        {
            if (Time.unscaledTime < _nextStatusTime)
                return;

            _nextStatusTime = Time.unscaledTime + StatusInterval;

            try
            {
                var player = CharacterMainControl.Main;
                var holder = player != null ? player.agentHolder : null;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;
                var gun = agent != null ? agent.GetComponent<ItemAgent_Gun>() : null;
                Item? ammo = gun != null ? gun.BulletItem : null;

                if (ammo == null)
                {
                    SetAmmo("-", -1);
                    return;
                }

                SetAmmo(WeaponHelper.GetDisplayName(ammo), WeaponHelper.GetQuality(ammo));
            }
            catch
            {
                SetAmmo("-", -1);
            }
        }

        private static void SetAmmo(string name, int quality)
        {
            if (CurrentAmmoQuality == quality && string.Equals(CurrentAmmoName, name, StringComparison.Ordinal))
                return;

            CurrentAmmoName = name;
            CurrentAmmoQuality = quality;
            AmmoRevision++;
        }

        // ── 잔상 풀 ──────────────────────────────────────────────

        private static TrailHandle? Rent()
        {
            EnsureHolder();
            if (_holder == null)
                return null;

            while (_idle.Count > 0)
            {
                var reused = _idle.Pop();
                if (reused.Go != null && reused.Trail != null)
                    return reused;
            }

            var go = new GameObject(TrailName);
            go.transform.SetParent(_holder.transform, false);
            go.SetActive(false);

            var trail = go.AddComponent<TrailRenderer>();
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.autodestruct = false;
            trail.receiveShadows = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            trail.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            trail.minVertexDistance = 0.02f;

            return new TrailHandle { Go = go, Trail = trail };
        }

        private static void Configure(TrailHandle handle, BulletTrailProfile profile)
        {
            var trail = handle.Trail;

            // sharedMaterial로 넣습니다. material로 읽으면 Unity가 사본을 만들어서
            // 총알 한 발마다 머티리얼이 하나씩 쌓입니다.
            trail.sharedMaterial = profile.additive ? GetAdditiveMaterial() : GetBlendMaterial();

            // 꼬리가 0초면 TrailRenderer가 아무것도 그리지 않습니다.
            trail.time = Mathf.Max(0.02f, profile.length);
            trail.startWidth = Mathf.Max(0.001f, profile.startWidth);
            trail.endWidth = Mathf.Max(0f, profile.endWidth);

            // TrailRenderer는 값을 자기 쪽으로 복사하므로 같은 Gradient 인스턴스를 돌려 써도 됩니다.
            trail.colorGradient = GetGradient(profile);
        }

        private static BulletTrailProfile? _gradientProfile;
        private static Gradient? _gradientCache;
        private static int _gradientHash;

        /// <summary>
        /// 그라디언트는 색 네 값과 합성 방식에만 의존합니다. 연사 중에는 같은 프로필이 계속
        /// 들어오므로 마지막 결과 하나만 들고 있어도 매 발 새로 만드는 일이 없어집니다.
        /// </summary>
        private static Gradient GetGradient(BulletTrailProfile profile)
        {
            int hash = profile.colorStart.GetHashCode();
            hash = (hash * 397) ^ profile.colorEnd.GetHashCode();
            hash = (hash * 397) ^ profile.alpha.GetHashCode();
            hash = (hash * 397) ^ profile.intensity.GetHashCode();
            hash = (hash * 397) ^ profile.additive.GetHashCode();

            if (_gradientCache != null && ReferenceEquals(_gradientProfile, profile) && _gradientHash == hash)
                return _gradientCache;

            _gradientCache = BuildGradient(profile);
            _gradientProfile = profile;
            _gradientHash = hash;
            return _gradientCache;
        }

        /// <summary>
        /// 머리에서 꼬리로 가면서 색이 바뀌고 투명해지는 그라디언트.
        ///
        /// 색·알파는 <see cref="BulletTrailShading"/>이 계산합니다 (미리보기와 같은 식).
        /// 여기에 셰이더 보정만 얹습니다.
        /// </summary>
        private static Gradient BuildGradient(BulletTrailProfile profile)
        {
            BulletTrailShading.Resolve(profile.colorStart, profile.intensity, profile.alpha,
                out Color start, out float startAlpha);
            BulletTrailShading.Resolve(profile.colorEnd, profile.intensity, profile.alpha,
                out Color end, out float endAlpha);

            // 두 끝의 알파 보정치가 다를 이유는 없지만(같은 밝기·투명도), 색의 최대 채널이
            // 다르면 값이 갈립니다. 잔상 전체의 세기는 하나여야 하므로 낮은 쪽에 맞춥니다.
            float alpha = Mathf.Min(startAlpha, endAlpha);

            float shaderScale = ShaderColorScale(profile.additive);
            start *= shaderScale;
            end *= shaderScale;
            alpha *= shaderScale;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(alpha, 0f),
                    new GradientAlphaKey(alpha * 0.55f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                });

            return gradient;
        }

        private static float? _additiveColorScale;
        private static float? _blendColorScale;

        /// <summary>
        /// 셰이더에 따른 색 보정.
        ///
        /// 레거시 <c>Particles/Additive</c>는 최종 색을 <c>2.0 × 정점색 × 틴트 × 텍스처</c>로 냅니다.
        /// 그대로 넣으면 채널값이 0.5만 넘어도 화면에서 1에 붙어 흰색이 됩니다 —
        /// 색을 아무리 골라도 잔상이 하얗게 나오던 이유입니다. 미리 절반으로 눌러 두면
        /// 셰이더를 거친 결과가 고른 색과 같아집니다.
        /// </summary>
        private static float ShaderColorScale(bool additive)
        {
            if (additive)
            {
                _additiveColorScale ??= ComputeShaderColorScale(GetAdditiveMaterial());
                return _additiveColorScale.Value;
            }

            _blendColorScale ??= ComputeShaderColorScale(GetBlendMaterial());
            return _blendColorScale.Value;
        }

        private static float ComputeShaderColorScale(Material? material)
        {
            string name = material != null && material.shader != null ? material.shader.name : "";
            return name.IndexOf("Particles/Additive", StringComparison.OrdinalIgnoreCase) >= 0
                ? 0.5f
                : 1f;
        }

        /// <summary>총알에서 떼어내고, 남은 꼬리가 사라질 때까지만 살려 둡니다.</summary>
        private static void BeginFade(TrailHandle handle)
        {
            if (!ReferenceEquals(handle.Follow, null))
            {
                _followed.Remove(handle.Follow);
                handle.Follow = null;
            }

            handle.Trail.emitting = false;

            // 꼬리 수명만큼 두면 마지막 점까지 자연스럽게 사라집니다.
            handle.RecycleAt = Time.time + handle.Trail.time + 0.05f;
        }

        private static void Recycle(TrailHandle handle)
        {
            if (!ReferenceEquals(handle.Follow, null))
            {
                _followed.Remove(handle.Follow);
                handle.Follow = null;
            }

            if (handle.Go == null || handle.Trail == null)
                return;

            handle.Trail.emitting = false;
            handle.Trail.Clear();
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

        // ── 머티리얼 ─────────────────────────────────────────────

        private static Material GetAdditiveMaterial()
        {
            if (_additiveMaterial == null)
                _additiveMaterial = CreateMaterial(additive: true);
            return _additiveMaterial;
        }

        private static Material GetBlendMaterial()
        {
            if (_blendMaterial == null)
                _blendMaterial = CreateMaterial(additive: false);
            return _blendMaterial;
        }

        /// <summary>
        /// 잔상용 머티리얼.
        ///
        /// TrailRenderer는 그라디언트를 정점 색으로 넣기 때문에, 정점 색을 무시하는
        /// 셰이더(URP/Unlit 등)를 쓰면 색이 통째로 단색이 되고 꼬리도 안 사라집니다.
        /// 그래서 파티클/스프라이트 계열만 후보로 둡니다.
        /// </summary>
        private static Material CreateMaterial(bool additive)
        {
            Shader? shader = FindShader(
                "Particles/Additive",
                "Legacy Shaders/Particles/Additive",
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Sprites/Default");

            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = additive ? "WeaponAura_BulletTrail_Add" : "WeaponAura_BulletTrail_Blend",
                mainTexture = StreakTexture,
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);

            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", additive
                    ? (int)UnityEngine.Rendering.BlendMode.One
                    : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);    // Transparent
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", additive ? 1f : 0f);

            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

            return material;
        }

        private static Shader? FindShader(params string[] names)
        {
            foreach (string name in names)
            {
                var shader = Shader.Find(name);
                if (shader != null)
                    return shader;
            }
            return null;
        }

        /// <summary>
        /// 가운데가 밝고 위아래로 부드럽게 사라지는 띠 텍스처.
        /// 흰색 1×1을 쓰면 꼬리 가장자리가 칼같이 잘려서 종잇조각처럼 보입니다.
        /// </summary>
        private static Texture2D StreakTexture
        {
            get
            {
                if (_streakTexture != null)
                    return _streakTexture;

                const int height = 32;

                var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
                {
                    name = "WeaponAura_BulletTrail_Streak",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color[height];
                for (int y = 0; y < height; y++)
                {
                    // 가운데(0.5)에서 1, 위아래 끝에서 0으로 떨어지는 부드러운 감쇠
                    float d = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y] = new Color(1f, 1f, 1f, a * a);
                }

                texture.SetPixels(pixels);
                texture.Apply();

                _streakTexture = texture;
                return _streakTexture;
            }
        }
    }
}
