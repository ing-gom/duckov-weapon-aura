using System;
using UnityEngine;
using ItemStatsSystem;
using WeaponAura.Helpers;
using WeaponAura.Settings;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 들고 있는 무기의 숙련도 레벨에 맞춰 오라 이펙트를 붙였다 떼는 시스템.
    ///
    /// 이벤트 구독 대신 저빈도 폴링(<see cref="CheckInterval"/>)을 씁니다.
    /// - 무기 교체·레벨업·설정 변경을 한 곳에서 처리할 수 있고,
    /// - ItemAgentHolder 이벤트 구독/해제 수명 문제에 얽히지 않습니다.
    /// </summary>
    public static class WeaponAuraSystem
    {
        /// <summary>상태 확인 간격(초)</summary>
        private const float CheckInterval = 0.25f;

        private const string AuraObjectName = "WeaponAura_Shell";

        /// <summary>무기 한 변이 이보다 크면 계산이 잘못된 것으로 보고 잘라냅니다 (m).</summary>
        private const float MaxWeaponSize = 2f;

        private static float _nextCheckTime;
        private static DuckovItemAgent? _trackedAgent;
        private static int _trackedTier = -2;
        private static WeaponAuraMode _trackedMode = (WeaponAuraMode)(-1);
        private static WeaponAuraController? _controller;
        private static DuckovItemAgent? _buildFailedAgent;

        // ── 상태 조회 (디버그 패널 표시용) ──────────────────────
        public static int CurrentLevel { get; private set; } = -1;
        public static int CurrentTier => _trackedTier;
        public static string CurrentWeaponName { get; private set; } = "-";
        public static bool HasAura => _controller != null;

        /// <summary>지금 오라의 원본으로 쓰이는 무기 렌더러 (미리보기 무대가 참조합니다).</summary>
        public static IReadOnlyList<Renderer> SilhouetteSources =>
            _controller != null ? _controller.SilhouetteSources : System.Array.Empty<Renderer>();
        public static bool MeshShapeInUse { get; private set; }
        /// <summary>계산된 무기 바운딩 박스 크기 (디버그 표시용)</summary>
        public static Vector3 WeaponBoundsSize { get; private set; }
        /// <summary>현재 방출 Shape와 그 이유 (디버그 표시용)</summary>
        public static string ShapeReason { get; private set; } = "-";
        /// <summary>면(셸)이 무기 메시를 원본으로 쓰고 있는지</summary>
        public static bool SheetUsesWeaponMesh { get; private set; }
        /// <summary>무기 트랜스폼의 스케일 (진단용 — 1이 아니면 보정이 동작 중)</summary>
        public static Vector3 HostScale { get; private set; } = Vector3.one;
        /// <summary>면(셸)이 어떤 방식으로 만들어졌는지 (진단용)</summary>
        public static string SheetMode { get; private set; } = "-";
        /// <summary>인스턴스 표시 등급 (게임 UI 색과 같은 값)</summary>
        public static int CurrentDisplayQuality { get; private set; }
        /// <summary>프리팹 기본 등급 (메타데이터)</summary>
        public static int CurrentMetaQuality { get; private set; }

        // ── 디버그 오버라이드 ───────────────────────────────────
        /// <summary>0 이상이면 실제 숙련도 대신 이 레벨을 사용합니다. -1이면 사용 안 함.</summary>
        public static int DebugLevelOverride = -1;
        /// <summary>-2면 자동, -1이면 강제로 오라 없음, 0 이상이면 해당 티어 강제.</summary>
        public static int DebugTierOverride = -2;
        /// <summary>true면 설정이 꺼져 있어도 오라를 표시합니다.</summary>
        public static bool DebugIgnoreSetting;

        /// <summary>ModBehaviour.Update에서 매 프레임 호출 (내부에서 스로틀링)</summary>
        public static void Tick()
        {
            if (Time.time < _nextCheckTime)
                return;

            _nextCheckTime = Time.time + CheckInterval;
            Evaluate(false);
        }

        /// <summary>지금 즉시 오라를 파괴하고 다시 만듭니다 (구조가 바뀌는 값 변경 시).</summary>
        public static void RebuildNow()
        {
            Evaluate(true);
        }

        /// <summary>
        /// 스로틀링을 건너뛰고 상태만 다시 판정합니다.
        /// 실제로 티어·무기·설정이 바뀌었을 때만 재생성하므로 슬라이더를 끌 때 깜빡이지 않습니다.
        /// </summary>
        public static void Reevaluate()
        {
            Evaluate(false);
        }

        /// <summary>파티클 재생성 없이 현재 티어 프로필의 수치만 반영합니다.</summary>
        public static void ApplyLive()
        {
            if (_controller == null)
                return;

            var profile = WeaponAuraProfiles.Get(_trackedTier);
            if (profile == null)
                return;

            _controller.ApplyLive(profile, CurrentIntensity());
        }

        /// <summary>오라를 제거합니다 (모드 비활성화·씬 전환 등).</summary>
        public static void Clear()
        {
            DestroyAura();
            _trackedAgent = null;
            _buildFailedAgent = null;
            _trackedTier = -2;
            _trackedMode = (WeaponAuraMode)(-1);
            CurrentLevel = -1;
            CurrentWeaponName = "-";
            MeshShapeInUse = false;
        }

        /// <summary>현재 설정 기준 오라 세기 배율</summary>
        public static float CurrentIntensity()
        {
            switch (AuraSettings.AuraMode)
            {
                case WeaponAuraMode.Subtle:
                    return 0.6f;
                case WeaponAuraMode.Intense:
                    return 1.6f;
                case WeaponAuraMode.Normal:
                    return 1f;
                default:
                    // 설정이 꺼져 있는데 디버그로 강제 표시 중이면 기본 세기
                    return DebugIgnoreSetting ? 1f : 0f;
            }
        }

        // ──────────────────────────────────────────────────────────

        private static void Evaluate(bool force)
        {
            try
            {
                var player = CharacterMainControl.Main;
                var holder = player != null ? player.agentHolder : null;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;
                var item = agent != null ? agent.Item : null;

                if (agent == null || item == null || !WeaponHelper.IsWeapon(item))
                {
                    if (_trackedAgent != null || _controller != null || CurrentLevel >= 0)
                        Clear();
                    return;
                }

                int level = ResolveLevel(item);
                int tier = ResolveTier(level);
                var mode = AuraSettings.AuraMode;

                CurrentLevel = level;
                CurrentWeaponName = SafeItemName(item);
                CurrentDisplayQuality = WeaponHelper.GetDisplayQuality(item);
                CurrentMetaQuality = WeaponHelper.GetMetaQuality(item);

                bool enabled = mode != WeaponAuraMode.Disabled || DebugIgnoreSetting;

                // 티어 단위로 꺼 둔 등급은 오라를 만들지 않습니다.
                // (낮은 등급 무기에는 오라를 원치 않는 경우가 많습니다)
                if (enabled && tier >= 0)
                {
                    var tierProfile = WeaponAuraProfiles.Get(tier);
                    if (tierProfile != null && !tierProfile.enabled && !DebugIgnoreSetting)
                        enabled = false;
                }

                if (!enabled)
                    tier = -1;

                bool sameState = agent == _trackedAgent && tier == _trackedTier && mode == _trackedMode;

                // 생성에 실패한 무기는 같은 상태가 유지되는 동안 다시 시도하지 않습니다 (매 틱 재시도 방지).
                if (!force && sameState && (_controller != null || _buildFailedAgent == agent))
                    return;

                if (!force && sameState && _controller == null && tier < 0)
                    return;

                DestroyAura();

                _trackedAgent = agent;
                _trackedTier = tier;
                _trackedMode = mode;

                // 무기별로 등급이 실제로 갈리는지 한 줄로 남깁니다.
                // (전부 같은 티어로 뜨는 문제를 눈으로 확인할 수 있게)
                var chosen = WeaponAuraProfiles.Get(tier);
                UnityEngine.Debug.Log(
                    $"[WeaponAura] 등급 판정: {CurrentWeaponName} — " +
                    $"표시={CurrentDisplayQuality} 메타={CurrentMetaQuality} 사용={level} " +
                    $"→ 티어 {tier}{(chosen != null ? $" {chosen.name}" : "")} " +
                    $"(기준={AuraSettings.TierSource})");

                if (tier < 0)
                    return;

                if (!CreateAura(agent, tier))
                    _buildFailedAgent = agent;
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] WeaponAuraSystem.Evaluate 오류: {ex.Message}");
#endif
            }
        }

        private static bool CreateAura(DuckovItemAgent agent, int tier)
        {
            var profile = WeaponAuraProfiles.Get(tier);
            if (profile == null)
                return false;

            float intensity = CurrentIntensity();
            if (intensity <= 0f)
                return false;

            Renderer? meshRenderer = FindMeshShapeRenderer(agent, profile, out string shapeReason);
            bool useMesh = meshRenderer != null;

            // 메시 Shape는 파티클 시스템과 메시 렌더러의 로컬 공간이 일치할 때만 무기 실루엣과 맞습니다.
            // 그래서 메시를 쓸 때는 오라 루트를 렌더러 트랜스폼에 그대로 붙이고,
            // 못 쓸 때는 에이전트 기준으로 무기 바운딩 박스 중심에 붙입니다.
            // (에이전트 원점은 손 소켓이라, 거기 그대로 두면 "손에서 나오는" 것처럼 보입니다.)
            Transform host = useMesh ? meshRenderer!.transform : agent.transform;

            // 설정이 끝나기 전에 파티클이 한 프레임 튀는 것을 막기 위해 비활성 상태로 만들고 마지막에 켭니다.
            var root = new GameObject(AuraObjectName);
            root.SetActive(false);
            root.transform.SetParent(host, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // ★ 호스트의 스케일을 상쇄해 오라 루트의 월드 스케일을 정확히 1로 만듭니다.
            //   이걸 안 하면 무기 모델의 스케일(덕코프는 1이 아닙니다)이 그대로 곱해져서
            //   0.035m로 지정한 값이 몇 미터짜리 덩어리가 됩니다.
            Vector3 hostScale = host.lossyScale;
            root.transform.localScale = new Vector3(
                Mathf.Approximately(hostScale.x, 0f) ? 1f : 1f / hostScale.x,
                Mathf.Approximately(hostScale.y, 0f) ? 1f : 1f / hostScale.y,
                Mathf.Approximately(hostScale.z, 0f) ? 1f : 1f / hostScale.z);

            // 루트 기준(= 월드 스케일 1, 단위 m)으로 바운딩 박스를 다시 계산합니다.
            Bounds localBounds = CalculateLocalBounds(agent, root.transform);

            // 무기가 이 정도로 클 수는 없습니다 — 값이 이상하면 사람이 볼 수 있게 남기고 잘라냅니다.
            Vector3 boundsSize = localBounds.size;
            if (boundsSize.x > MaxWeaponSize || boundsSize.y > MaxWeaponSize || boundsSize.z > MaxWeaponSize)
            {
                UnityEngine.Debug.LogWarning(
                    $"[WeaponAura] 무기 바운딩 박스가 비정상적으로 큽니다: {boundsSize} (호스트 스케일 {hostScale}). 잘라냅니다.");
                boundsSize = Vector3.Min(boundsSize, Vector3.one * MaxWeaponSize);
            }

            Vector3 shapeCenter = localBounds.center;

            var controller = root.AddComponent<WeaponAuraController>();

            if (!controller.Build(agent.Holder, meshRenderer, shapeCenter, boundsSize, hostScale, profile, intensity))
            {
                UnityEngine.Object.Destroy(root);
                return false;
            }

            root.SetActive(true);

            _controller = controller;
            _buildFailedAgent = null;
            MeshShapeInUse = controller.MeshShapeActive;
            SheetUsesWeaponMesh = controller.SheetUsesWeaponMesh;
            SheetMode = controller.SheetModeName;
            WeaponBoundsSize = boundsSize;
            HostScale = hostScale;
            ShapeReason = MeshShapeInUse ? "메시 표면" : shapeReason;

#if DEBUG
            UnityEngine.Debug.Log(
                $"[WeaponAura] 무기 오라 생성: {CurrentWeaponName}, Lv.{CurrentLevel}, 티어 {tier}({profile.name}), " +
                $"메시Shape={MeshShapeInUse}({ShapeReason}), 크기={boundsSize}, 세기={intensity:0.00}");
#endif
            return true;
        }

        /// <summary>
        /// 메시 Shape로 쓸 렌더러를 고릅니다. 쓸 수 없으면 null과 사유를 돌려줍니다.
        /// </summary>
        private static Renderer? FindMeshShapeRenderer(DuckovItemAgent agent, WeaponAuraProfile profile, out string reason)
        {
            if (!profile.useMeshShape)
            {
                reason = "박스(메시Shape 끔)";
                return null;
            }

            var renderer = FindLargestRenderer(agent);
            if (renderer == null)
            {
                reason = "박스(렌더러 없음)";
                return null;
            }

            var mesh = GetSharedMesh(renderer);
            if (mesh == null)
            {
                reason = "박스(메시 없음)";
                return null;
            }

            if (!mesh.isReadable)
            {
                reason = "박스(메시 읽기불가)";
                return null;
            }

            reason = "메시 표면";
            return renderer;
        }

        /// <summary>
        /// 무기(및 부착물) 전체를 감싸는 바운딩 박스를 host 로컬 공간에서 계산합니다.
        /// Mesh.bounds는 isReadable이 false여도 접근할 수 있어서, 읽기 불가 메시에서도 크기를 알 수 있습니다.
        /// </summary>
        private static Bounds CalculateLocalBounds(DuckovItemAgent agent, Transform host)
        {
            // 1순위: 렌더러의 월드 바운즈. 스케일·부모 체인과 무관하게 항상 실제 미터라서
            //        로컬 단위 계산에서 생기던 배율 혼동이 원천적으로 없습니다.
            //        (host의 월드 스케일은 1로 보정해 두었으므로 월드 크기 = 로컬 크기)
            var world = CalculateWorldBounds(agent);
            if (world.HasValue)
            {
                var w = world.Value;
                return new Bounds(host.InverseTransformPoint(w.center), w.size);
            }

            // 2순위: 렌더러가 아직 비활성이라 월드 바운즈를 못 믿을 때 메시 로컬 값으로 계산
            var result = new Bounds(Vector3.zero, Vector3.one * 0.15f);
            bool initialized = false;

            try
            {
                foreach (var renderer in agent.GetComponentsInChildren<Renderer>(true))
                {
                    if (!IsWeaponBodyRenderer(renderer))
                        continue;

                    var mesh = GetSharedMesh(renderer);
                    if (mesh == null || mesh.vertexCount <= 0)
                        continue;

                    Bounds meshBounds = mesh.bounds;
                    Vector3 center = meshBounds.center;
                    Vector3 extents = meshBounds.extents;

                    // 메시 로컬 → 월드 → host 로컬 로 8개 코너를 옮겨서 감싸기
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3(
                            center.x + ((i & 1) == 0 ? -extents.x : extents.x),
                            center.y + ((i & 2) == 0 ? -extents.y : extents.y),
                            center.z + ((i & 4) == 0 ? -extents.z : extents.z));

                        Vector3 local = host.InverseTransformPoint(renderer.transform.TransformPoint(corner));

                        if (!initialized)
                        {
                            result = new Bounds(local, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            result.Encapsulate(local);
                        }
                    }
                }
            }
            catch
            {
                // 계산 실패 시 기본값 사용
            }

            if (!initialized)
                return new Bounds(Vector3.zero, Vector3.one * 0.15f);

            return result;
        }

        private static void DestroyAura()
        {
            if (_controller != null)
            {
                UnityEngine.Object.Destroy(_controller.gameObject);
                _controller = null;
            }
        }

        /// <summary>
        /// 무기 모델에서 가장 큰 렌더러(= 본체 메시)를 찾습니다.
        /// 무기를 꺼내기 전에는 GameObject가 비활성이라 Renderer.bounds가 0이므로,
        /// 활성 상태와 무관한 sharedMesh.bounds를 기준으로 판단합니다.
        /// </summary>
        private static Renderer? FindLargestRenderer(DuckovItemAgent agent)
        {
            try
            {
                var renderers = agent.GetComponentsInChildren<Renderer>(true);
                Renderer? best = null;
                float bestVolume = 0f;

                foreach (var renderer in renderers)
                {
                    if (!IsWeaponBodyRenderer(renderer))
                        continue;

                    Mesh? mesh = GetSharedMesh(renderer);
                    if (mesh == null || mesh.vertexCount <= 0)
                        continue;

                    Vector3 size = Vector3.Scale(mesh.bounds.size, renderer.transform.lossyScale);
                    float volume = Mathf.Abs(size.x * size.y * size.z);
                    if (volume > bestVolume)
                    {
                        bestVolume = volume;
                        best = renderer;
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 무기 렌더러들의 월드 바운즈 합집합. 활성 상태인 렌더러만 유효한 값을 주므로,
        /// 하나도 없으면 null을 돌려 로컬 계산으로 넘깁니다.
        /// </summary>
        private static Bounds? CalculateWorldBounds(DuckovItemAgent agent)
        {
            try
            {
                Bounds result = default;
                bool initialized = false;

                foreach (var renderer in agent.GetComponentsInChildren<Renderer>(false))
                {
                    if (!IsWeaponBodyRenderer(renderer))
                        continue;

                    var b = renderer.bounds;
                    if (b.size.sqrMagnitude <= 1e-8f)
                        continue;

                    // 레이저·트레일처럼 조준 지점까지 뻗는 렌더러가 섞이면 바운즈가 수십 m가 됩니다.
                    // 무기 본체는 이보다 클 수 없으므로 걸러냅니다.
                    if (b.size.x > MaxWeaponSize || b.size.y > MaxWeaponSize || b.size.z > MaxWeaponSize)
                        continue;

                    if (!initialized)
                    {
                        result = b;
                        initialized = true;
                    }
                    else
                    {
                        result.Encapsulate(b);
                    }
                }

                return initialized ? result : (Bounds?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 무기 "본체" 렌더러인지. 레이저 사이트(LineRenderer)·궤적·파티클·UI 스프라이트는 제외합니다.
        /// 레이저는 월드 바운즈가 조준 지점까지 뻗어서(13~30m) 오라 크기를 통째로 망가뜨립니다.
        /// </summary>
        private static bool IsWeaponBodyRenderer(Renderer? renderer)
        {
            if (renderer == null)
                return false;

            if (renderer is ParticleSystemRenderer
                || renderer is TrailRenderer
                || renderer is LineRenderer
                || renderer is SpriteRenderer
                || renderer is BillboardRenderer)
                return false;

            if (renderer.gameObject.name.StartsWith("WeaponAura_"))
                return false;

            return true;
        }

        private static Mesh? GetSharedMesh(Renderer renderer)
        {
            try
            {
                if (renderer is SkinnedMeshRenderer skinned)
                    return skinned.sharedMesh;

                var filter = renderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// 이 무기의 "등급 점수". 설정한 티어 소스에 따라 달라집니다.
        /// (숙련도 레벨이 아니라 이 모드 단독으로 판단할 수 있는 값만 씁니다.)
        /// </summary>
        private static int ResolveLevel(Item item)
        {
            if (DebugLevelOverride >= 0)
                return DebugLevelOverride;

            try
            {
                switch (AuraSettings.TierSource)
                {
                    case AuraTierSource.WeaponClass:
                        return ResolveWeaponClassScore(item);

                    case AuraTierSource.Fixed:
                        return int.MaxValue;

                    case AuraTierSource.Quality:
                    default:
                        return WeaponHelper.GetQuality(item);
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>무기 종류로 점수를 매깁니다 (근접 &lt; 소형 &lt; 소총 &lt; 중화기).</summary>
        private static int ResolveWeaponClassScore(Item item)
        {
            if (WeaponHelper.IsRocketWeapon(item))
                return 4;
            if (WeaponHelper.IsMeleeWeapon(item))
                return 1;

            // 총기는 일단 3점, 등급이 높으면 4점으로 올림
            int quality = WeaponHelper.GetQuality(item);
            return quality >= 4 ? 4 : 3;
        }

        private static int ResolveTier(int level)
        {
            if (DebugTierOverride >= -1)
                return DebugTierOverride;

            if (AuraSettings.TierSource == AuraTierSource.Fixed)
            {
                int max = WeaponAuraProfiles.TierCount - 1;
                return Mathf.Clamp(AuraSettings.FixedTier, 0, max);
            }

            return WeaponAuraProfiles.ResolveTier(level);
        }

        private static string SafeItemName(Item item)
        {
            try
            {
                string displayName = item.DisplayName ?? "";
                return string.IsNullOrEmpty(displayName) ? item.name : displayName;
            }
            catch
            {
                return "-";
            }
        }
    }
}
