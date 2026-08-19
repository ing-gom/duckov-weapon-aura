using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 무기 오라 인스턴스. 들고 있는 무기(DuckovItemAgent)의 렌더러 트랜스폼 아래에 붙어서
    /// 표면 파티클(wrap)과 선택적인 회전 링(ring)을 관리합니다.
    ///
    /// - 무기를 바꾸면 ItemAgent가 통째로 Destroy되므로 이 오브젝트도 같이 사라집니다(정리 불필요).
    /// - CharacterSubVisuals에 등록되어 캐릭터 은폐/표시 상태를 따라갑니다.
    /// </summary>
    public class WeaponAuraController : MonoBehaviour
    {
        /// <summary>링 한 주기(초). 수명과 같게 두어 끊김 없이 이어지게 합니다.</summary>
        private const float RingCycle = 4f;

        private ParticleSystem? _wrap;
        private ParticleSystem? _ring;
        private readonly List<WeaponAuraSheet> _sheets = new List<WeaponAuraSheet>();
        private Transform? _ringTransform;
        private Vector3 _ringBasePosition;
        private float _ringYaw;

        private WeaponAuraProfile? _profile;
        private float _intensity = 1f;

        /// <summary>무기 전체를 감싸는 로컬 바운딩 박스 크기 (메시 Shape를 못 쓸 때 방출 영역으로 사용)</summary>
        private Vector3 _boundsSize = Vector3.one * 0.2f;
        /// <summary>바운딩 박스 중심 (루트 기준 로컬)</summary>
        private Vector3 _shapeCenter = Vector3.zero;

        /// <summary>
        /// 호스트 트랜스폼의 스케일. 루트에서 이 값을 상쇄했기 때문에,
        /// 메시 Shape 방출처럼 원본 공간을 그대로 쓰는 기능은 다시 곱해줘야 맞습니다.
        /// </summary>
        private Vector3 _hostScale = Vector3.one;

        /// <summary>현재 적용 중인 프로필 (읽기 전용 참조)</summary>
        public WeaponAuraProfile? Profile => _profile;

        /// <summary>실제로 무기 메시 표면 방출이 적용됐는지 (읽기 불가 메시 등은 박스로 폴백)</summary>
        public bool MeshShapeActive { get; private set; }

        /// <summary>면(셸)이 무기 메시를 원본으로 쓰고 있는지</summary>
        public bool SheetUsesWeaponMesh { get; private set; }

        /// <summary>면 생성 방식 이름 (진단용)</summary>
        public string SheetModeName { get; private set; } = "-";

        /// <summary>
        /// 실루엣 원본으로 고른 무기 렌더러.
        /// 미리보기 무대가 복제본에서 "같은 부품"을 찾을 때 기준으로 씁니다
        /// (복제본에는 ItemAgent가 없어서 선택 로직을 다시 돌릴 수 없습니다).
        /// </summary>
        public List<Renderer> SilhouetteSources { get; private set; } = new List<Renderer>();

        /// <summary>
        /// 실루엣 원본을 밖에서 지정합니다 (미리보기 무대용).
        ///
        /// 평소에는 <see cref="FindWeaponRenderers"/>가 ItemAgent를 타고 올라가며 본체를 고르지만,
        /// 미리보기 복제본에는 ItemAgent가 없어서 그 로직을 돌릴 수 없습니다.
        /// 복제 전에 골라 둔 렌더러를 여기 넣어 주면 같은 모양이 나옵니다.
        /// </summary>
        public List<Renderer>? SilhouetteOverride { get; set; }

        /// <summary>
        /// 일시정지 중에도 움직이게 합니다 (미리보기 무대용).
        ///
        /// 설정 창은 게임이 멈춰 있을 때 열리므로 Time.time·deltaTime이 진행하지 않습니다.
        /// 이 값을 켜면 셸·링·파티클이 모두 unscaled 시간으로 돕니다.
        /// </summary>
        public bool UseUnscaledTime { get; set; }

        /// <summary>
        /// 알갱이를 무조건 무기 좌표계에서 돌립니다 (프로필의 "지나간 자리에 남기기"를 무시).
        ///
        /// 설정 창 미리보기 전용입니다. 미리보기 무대는 모델을 제자리에서 빙글빙글 돌리는데,
        /// 월드 공간이면 알갱이가 그 회전을 따라 고리 모양으로 번집니다. 실제 게임에서
        /// 보게 될 "지나간 자리에 남는 궤적"과 전혀 다른 그림이라 오해만 부릅니다.
        /// </summary>
        public bool ForceLocalParticles { get; set; }

        /// <summary>이 오라가 어디에 세워진 것인지 (진단 로그 구분용).</summary>
        public string Origin { get; set; } = "게임";

        /// <summary>
        /// 오라를 생성합니다. root(this.gameObject)는 이미 무기 트랜스폼 아래에 부모 설정되어 있어야 합니다.
        /// </summary>
        /// <param name="holder">무기를 들고 있는 캐릭터 (CharacterSubVisuals 등록용, null 허용)</param>
        /// <param name="meshRenderer">메시 Shape로 쓸 렌더러 (null이면 구형 방출로 폴백)</param>
        /// <param name="localCenter">링을 배치할 로컬 좌표 (보통 메시 bounds 중심)</param>
        public bool Build(CharacterMainControl? holder, Renderer? meshRenderer, Vector3 localCenter,
            Vector3 boundsSize, Vector3 hostScale, WeaponAuraProfile profile, float intensity)
        {
            _profile = profile.Clone();
            _intensity = Mathf.Max(0f, intensity);
            _ringBasePosition = localCenter;
            _boundsSize = boundsSize;
            _shapeCenter = localCenter;
            _hostScale = hostScale;

            try
            {
                _wrap = CreateWrapParticles(meshRenderer);
                if (_wrap == null)
                    return false;

                if (_profile.ringEnabled && _profile.ringCount > 0)
                    _ring = CreateRingParticles();

                // 캐릭터 은폐/표시(레이어) 동기화 — 파티클 생성 후에 등록해야 SetRenderers가 잡습니다.
                //
                // ★ 면(셸)을 만들기 "전에" 등록하는 것이 중요합니다.
                //   CharacterSubVisuals.SetRenderers()는 파티클과 일반 MeshRenderer를 나눠 담고,
                //   CharacterModel.AddSubVisuals()가 일반 렌더러 쪽만 hurtVisual에 넘깁니다.
                //   hurtVisual은 피격 플래시를 위해 그 렌더러들의 MaterialPropertyBlock을 덮어쓰므로,
                //   셸이 거기 등록되면 우리가 넣는 색·알파가 지워져 보이지 않게 됩니다.
                RegisterSubVisuals(holder);

                if (_profile.renderStyle == WeaponAuraRenderStyle.Sheet)
                    CreateSheets(meshRenderer);

                ApplyLive(_profile, _intensity);
#if DEBUG
                LogScale();
#endif
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] WeaponAuraController.Build 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 파티클 재생성 없이 수치만 갱신합니다 (디버그 패널 슬라이더용).
        /// Shape 종류·링 유무처럼 구조가 바뀌는 값은 반영되지 않으므로 재생성이 필요합니다.
        /// </summary>
        public void ApplyLive(WeaponAuraProfile profile, float intensity)
        {
            if (profile == null)
                return;

            if (_profile == null)
                _profile = profile.Clone();
            else
                _profile.CopyFrom(profile);

            _intensity = Mathf.Max(0f, intensity);

            try
            {
                ApplyToWrap();
                ApplyToRing();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] WeaponAuraController.ApplyLive 오류: {ex.Message}");
            }
        }

#if DEBUG
        /// <summary>
        /// 알갱이 크기가 실제로 몇 미터로 해석되는지 한 번 남깁니다.
        ///
        /// 미리보기와 인게임이 다르게 보인다면 둘 중 하나입니다 — 알갱이 자체가 다른 크기이거나,
        /// 뿜는 영역이 달라서 뭉치는 정도가 다르거나. 두 줄을 나란히 놓고 비교하면 갈립니다.
        /// startSize는 계층 스케일이 곱해지므로 루트의 lossyScale이 1이어야 미터로 읽힙니다.
        /// </summary>
        private void LogScale()
        {
            if (_wrap == null || _profile == null)
                return;

            var shape = _wrap.shape;
            var lossy = transform.lossyScale;

            UnityEngine.Debug.Log(
                $"[WeaponAura] 알갱이 실측({Origin}): 설정크기={_profile.startSize:0.####} " +
                $"실제크기={_wrap.main.startSize.constantMax:0.####} " +
                $"루트스케일={lossy.ToString("0.###")} 스케일모드={_wrap.main.scalingMode} " +
                $"| 방출={shape.shapeType} 셰이프스케일={shape.scale.ToString("0.###")} " +
                $"위치={shape.position.ToString("0.###")} " +
                $"| 무기바운즈={_boundsSize.ToString("0.###")} 호스트스케일={_hostScale.ToString("0.###")} " +
                $"메시Shape={MeshShapeActive} " +
                // 흰색으로 포화되는지는 "몇 개가 얼마나 진하게 겹치는가"로 갈립니다.
                $"| 세기={_intensity:0.##} 방출={_wrap.emission.rateOverTime.constantMax:0.#}/s " +
                $"최대개수={_wrap.main.maxParticles} 수명={_profile.lifetime:0.##}s " +
                $"알파={_profile.alpha:0.##} 밝기={_profile.colorIntensity:0.##} " +
                $"| 방식={_profile.renderStyle} 겹={_sheets.Count} 월드잔상={_profile.worldTrail}");
        }
#endif

        /// <summary>퍼져나가는 면(셸)을 겹 수만큼 만듭니다.</summary>
        private void CreateSheets(Renderer? weaponRenderer)
        {
            if (_profile == null)
                return;

            int layers = Mathf.Clamp(_profile.sheetLayers, 1, 8);

            // 면은 파티클용 원형 글로우 텍스처를 쓰면 안 됩니다.
            // 구면 UV 전체에 점 무늬가 늘어나 얼룩덜룩한 덩어리가 됩니다.
            // 흰 텍스처를 써서 정점 색(그라디언트 + 림 + 동심원 밴드)만으로 그립니다.
            var material = WeaponAuraResources.GetSheetMaterial(_profile.textureName);
            Vector3 half = _boundsSize * 0.5f;

            // 무기 메시를 셸 원본으로 쓸 수 있으면 그 메시 로컬 → 셸 로컬 변환을 준비합니다.
            // 실루엣 복제가 기본 경로라 정점 복사 경로는 현재 쓰이지 않지만,
            // Build(source, ...) 폴백 시그니처를 유지하기 위해 남겨 둡니다.
            Mesh? source = null;
            Matrix4x4 sourceToLocal = Matrix4x4.identity;

            string reason;
            List<Renderer> silhouetteParts = new List<Renderer>();

            if (!_profile.sheetUseWeaponMesh)
            {
                reason = "박스(메시 사용 끔)";
            }
            else
            {
                // 보이는 본체 렌더러 중 가장 큰 것 = 총기 본체.
                // 부착물까지 감싸면 형태가 지저분해져서 본체 하나만 씁니다.
                silhouetteParts = SilhouetteOverride != null && SilhouetteOverride.Count > 0
                    ? new List<Renderer>(SilhouetteOverride)
                    : FindWeaponRenderers();

                if (silhouetteParts.Count == 0)
                {
                    reason = "박스(렌더러 못 찾음)";
                    // 이 경우 바운즈도 기본값으로 떨어져 손 위치에 작은 덩어리만 뜹니다.
                    // 조용히 넘어가면 원인을 못 찾으니 사유를 그대로 남깁니다.
                    UnityEngine.Debug.LogWarning(
                        $"[WeaponAura] 무기 렌더러를 찾지 못했습니다 — 손 위치에 표시될 수 있습니다. {FormatRejections()}");
                }
                else
                {
                    reason = $"실루엣 복제 ({silhouetteParts[0].gameObject.name})";

                    var first = silhouetteParts[0];
                    var firstMesh = GetSharedMesh(first);
                    UnityEngine.Debug.Log(
                        $"[WeaponAura] {DescribeParts(silhouetteParts)}\n" +
                        $"  월드바운즈={first.bounds.size} " +
                        $"메시바운즈={(firstMesh != null ? firstMesh.bounds.size.ToString() : "?")}");
                }
            }

            SheetModeName = reason;
            SilhouetteSources = silhouetteParts;
            UnityEngine.Debug.Log($"[WeaponAura] 면 생성 방식: {reason}");

            for (int i = 0; i < layers; i++)
            {
                var go = new GameObject($"Sheet{i}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = _shapeCenter;
                go.transform.localRotation = Quaternion.identity;

                var sheet = go.AddComponent<WeaponAuraSheet>();
                sheet.SetPhase(i / (float)layers);

                bool built = false;

                if (silhouetteParts.Count > 0)
                {
                    // 메시를 못 읽어도 "그리기"는 되므로, 무기 메시를 한 벌 더 렌더링해
                    // 총 모양 그대로 부풀어 오르게 합니다 (박스 폴백보다 훨씬 정확).
                    built = sheet.BuildSilhouette(silhouetteParts, transform, material);

                    if (!built && i == 0)
                    {
                        // 스킨드 메시처럼 복제가 불가능한 무기는 여기로 옵니다.
                        // 아무것도 안 그리는 것보다 박스 셸이라도 무기 위에 띄우는 게 낫습니다.
                        SheetModeName = "박스(실루엣 불가 · 스킨드 메시)";
                        UnityEngine.Debug.LogWarning(
                            $"[WeaponAura] 실루엣 복제 불가 → 박스 셸로 폴백. {DescribeParts(silhouetteParts)}");
                    }
                }

                if (!built)
                {
                    // 셸 오브젝트가 실루엣 시도로 위치가 바뀌었을 수 있어 원위치로 되돌립니다.
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = _shapeCenter;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;

                    built = sheet.Build(source, sourceToLocal, half, _profile.sheetBoxiness, i / (float)layers, material);
                }

                if (!built)
                {
                    Destroy(go);
                    continue;
                }

                if (i == 0)
                    SheetUsesWeaponMesh = sheet.UsesWeaponMesh;

                _sheets.Add(sheet);
            }
        }

        /// <summary>
        /// 무기 본체 렌더러를 직접 찾습니다.
        /// 시스템이 넘겨주는 렌더러는 "파티클 메시 Shape로 쓸 수 있는" 것만이라,
        /// 읽기 불가 메시인 경우 null로 옵니다. 실루엣 복제는 읽기 여부와 무관하므로 여기서 다시 찾습니다.
        /// </summary>
        private List<Renderer> FindWeaponRenderers()
        {
            try
            {
                // GetComponentInParent는 기본적으로 "활성" 오브젝트만 훑습니다.
                // 오라 루트는 설정이 끝날 때까지 비활성이라 여기서 항상 null이 나왔습니다.
                // 그래서 부모 체인을 직접 걸어 올라갑니다.
                DuckovItemAgent? agent = null;
                for (var t = transform.parent; t != null; t = t.parent)
                {
                    agent = t.GetComponent<DuckovItemAgent>();
                    if (agent != null)
                        break;
                }

                if (agent == null)
                    return new List<Renderer>();

                // 화면에 실제로 보이는 것만 씁니다.
                // 총기는 부착물/스킨 변형 메시를 숨겨둔 채 들고 다니는 경우가 많아서,
                // 비활성 렌더러를 고르면 안 보이는 메시로 실루엣을 만들게 됩니다.
                // 부착물은 무기 에이전트의 "소켓" 트랜스폼 아래에 장착됩니다 (Muzzle, Tec 등).
                // 게임이 실제로 등록해 둔 소켓 목록을 읽어 그 하위를 통째로 제외하면,
                // 이름을 추측할 필요 없이 총기 본체만 남습니다.
                var sockets = GetSocketTransforms(agent);

                var visible = new List<Renderer>();
                var hidden = new List<Renderer>();
                // 필터에 다 걸러졌을 때 쓸 예비 목록 (부착물이라도 감싸는 게 손에 덩어리가 뜨는 것보다 낫습니다)
                var relaxed = new List<Renderer>();
                _rejectionLog.Clear();

                foreach (var renderer in agent.GetComponentsInChildren<Renderer>(true))
                {
                    if (!IsBodyRenderer(renderer))
                    {
                        Reject(renderer, "렌더러 종류");
                        continue;
                    }

                    var mesh = GetSharedMesh(renderer);
                    if (mesh == null || mesh.vertexCount <= 0)
                    {
                        Reject(renderer, "메시 없음");
                        continue;
                    }

                    bool isVisible = renderer.gameObject.activeInHierarchy && renderer.enabled;
                    if (isVisible)
                        relaxed.Add(renderer);

                    if (IsUnderAnySocket(renderer.transform, sockets))
                    {
                        Reject(renderer, "소켓 하위(부착물)");
                        continue;
                    }

                    // 소켓 이름에 기대지 않는 일반 규칙:
                    // 부착물은 "자기 Item/ItemAgent를 가진 별개 아이템"입니다.
                    // 렌더러에서 무기 에이전트까지 올라가는 길에 다른 Item이 끼어 있으면 부착물입니다.
                    if (BelongsToAttachedItem(renderer.transform, agent))
                    {
                        Reject(renderer, "다른 Item 하위(부착물)");
                        continue;
                    }

                    if (isVisible)
                        visible.Add(renderer);
                    else
                        hidden.Add(renderer);
                }

                // 보이는 게 하나도 없으면(무기를 아직 꺼내지 않은 상태 등) 숨은 것이라도 씁니다.
                var chosen = visible.Count > 0 ? visible : hidden;

                // 필터가 전부 걸러냈다면 필터를 풀어서라도 무언가를 감쌉니다.
                // (아무것도 없으면 바운즈가 기본값으로 떨어져 "손에 작은 덩어리"가 됩니다)
                if (chosen.Count == 0 && relaxed.Count > 0)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[WeaponAura] 본체 렌더러가 전부 걸러져 필터를 완화합니다. {FormatRejections()}");
                    chosen = relaxed;
                }

                // 총기 본체는 "가장 긴" 부품입니다.
                // 부피로 고르면 드럼탄창·대형 조준경처럼 뭉툭한 부착물이 본체를 이깁니다.
                // 총열+개머리판이 이어진 본체는 길이에서 압도적이라 훨씬 안정적인 기준입니다.
                chosen.Sort((a, b) =>
                {
                    int byLength = LongestSideOf(b).CompareTo(LongestSideOf(a));
                    if (byLength != 0)
                        return byLength;

                    // 길이가 비슷하면 형상이 더 복잡한 쪽(정점 수)이 본체일 가능성이 높습니다.
                    return VertexCountOf(b).CompareTo(VertexCountOf(a));
                });

                LogCandidates(chosen);
                if (chosen.Count > MaxSilhouetteParts)
                    chosen.RemoveRange(MaxSilhouetteParts, chosen.Count - MaxSilhouetteParts);

                return chosen;
            }
            catch
            {
                return new List<Renderer>();
            }
        }

        /// <summary>
        /// 실루엣으로 복제할 부품 수.
        /// 1이면 총기 본체(가장 큰 보이는 메시)만 써서 형태가 깔끔하게 떨어집니다.
        /// 부착물까지 전부 감싸면 오라가 지저분해지고 드로우콜도 겹 수만큼 곱해집니다.
        /// </summary>
        private const int MaxSilhouetteParts = 1;

        /// <summary>
        /// 무기 에이전트가 등록한 소켓 트랜스폼 목록을 읽습니다 (private 필드라 리플렉션).
        /// 실패하면 게임 코드에 나오는 소켓 이름으로 폴백합니다.
        /// </summary>
        private static List<Transform> GetSocketTransforms(DuckovItemAgent agent)
        {
            var result = new List<Transform>();

            try
            {
                var field = typeof(DuckovItemAgent).GetField("socketsList",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public);

                if (field != null && field.GetValue(agent) is List<Transform> list)
                {
                    foreach (var socket in list)
                    {
                        if (socket != null)
                            result.Add(socket);
                    }
                }
            }
            catch
            {
                // 아래 이름 폴백으로 진행
            }

            if (result.Count == 0)
            {
                // ItemAgent_Gun이 만드는 기본 소켓 이름들
                foreach (string name in SocketNames)
                {
                    var socket = agent.transform.Find(name);
                    if (socket != null)
                        result.Add(socket);
                }
            }

            return result;
        }

        /// <summary>ItemAgent_Gun이 생성하는 소켓 이름 (부착물이 붙는 자리)</summary>
        private static readonly string[] SocketNames = { "Muzzle", "Muzzle2", "Tec" };

        /// <summary>
        /// 이 렌더러가 "다른 아이템"(부착물)에 속하는지 판별합니다.
        /// 부착물은 슬롯에 장착된 별개 Item이고 자기 ItemAgent를 갖습니다.
        /// 렌더러에서 무기 에이전트까지 올라가는 도중에 그런 컴포넌트를 만나면 부착물입니다.
        /// </summary>
        private static bool BelongsToAttachedItem(Transform target, DuckovItemAgent agent)
        {
            var agentTransform = agent.transform;

            for (var t = target; t != null && t != agentTransform; t = t.parent)
            {
                if (t.GetComponent<ItemStatsSystem.ItemAgent>() != null)
                    return true;

                if (t.GetComponent<ItemStatsSystem.Item>() != null)
                    return true;
            }

            return false;
        }

        private static bool IsUnderAnySocket(Transform target, List<Transform> sockets)
        {
            if (sockets.Count == 0)
                return false;

            for (var t = target; t != null; t = t.parent)
            {
                for (int i = 0; i < sockets.Count; i++)
                {
                    if (t == sockets[i])
                        return true;
                }
            }

            return false;
        }

        private static bool IsBodyRenderer(Renderer? renderer)
        {
            if (renderer == null
                || renderer is ParticleSystemRenderer
                || renderer is TrailRenderer
                || renderer is LineRenderer
                || renderer is SpriteRenderer
                || renderer is BillboardRenderer)
                return false;

            // 점광원의 빛 덩어리는 무기 실루엣이 아닙니다.
            //
            // 이게 없으면 불꽃 AK-47 같은 속성 무기에서 오라가 총이 아니라 빛을 감쌉니다.
            // 본체 후보는 "길이 순"으로 고르는데, 빛 덩어리는 구(2×2×2 메시)를 크게 키워
            // 놓은 것이라 실측에서 6.77m로 잡혔습니다. 총열(WPN_AK47)이 0.79m니까 경쟁이
            // 안 됩니다. 그래서 오라가 6.8m짜리 공을 감싸고, 빛이 이상하게 번져 보입니다.
            //
            // 이름이 아니라 컴포넌트로 거릅니다 — 게임이 오브젝트 이름을 바꿔도 따라갑니다.
            // (일반 총에도 SodaPointLight가 0.75m로 붙어 있어서 총열 0.79m와 간발의 차입니다.
            //  즉 이건 속성 무기만의 문제가 아니라 원래 아슬아슬했던 것입니다)
            if (renderer.GetComponent<SodaPointLight>() != null
                || renderer.GetComponentInParent<SodaPointLight>() != null)
                return false;

            string name = renderer.gameObject.name;
            return !name.StartsWith("WeaponAura_") && name != "Silhouette";
        }

        /// <summary>월드 기준 가장 긴 변 (m). 총기 본체를 고르는 1순위 기준입니다.</summary>
        private static float LongestSideOf(Renderer renderer)
        {
            var mesh = GetSharedMesh(renderer);
            if (mesh == null)
                return 0f;

            Vector3 size = Vector3.Scale(mesh.bounds.size, renderer.transform.lossyScale);
            return Mathf.Max(Mathf.Abs(size.x), Mathf.Max(Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }

        private static int VertexCountOf(Renderer renderer)
        {
            var mesh = GetSharedMesh(renderer);
            return mesh != null ? mesh.vertexCount : 0;
        }

        /// <summary>선택된 부품의 실제 구성을 설명합니다 (무기별 차이 추적용).</summary>
        private static string DescribeParts(List<Renderer> parts)
        {
            var sb = new System.Text.StringBuilder("선택 부품 — ");

            for (int i = 0; i < parts.Count; i++)
            {
                var r = parts[i];
                if (r == null)
                    continue;

                var mesh = GetSharedMesh(r);
                sb.Append($"{r.gameObject.name}[{r.GetType().Name}] " +
                          $"메시={(mesh != null ? mesh.name : "없음")} " +
                          $"서브메시={(mesh != null ? mesh.subMeshCount : 0)} " +
                          $"정점={(mesh != null ? mesh.vertexCount : 0)} ");
            }

            return sb.ToString();
        }

        /// <summary>렌더러가 왜 후보에서 빠졌는지 기록 (무기별 이슈 추적용)</summary>
        private readonly List<string> _rejectionLog = new List<string>();

        private void Reject(Renderer? renderer, string reason)
        {
            if (renderer == null || _rejectionLog.Count >= 20)
                return;

            _rejectionLog.Add($"{renderer.gameObject.name}({renderer.GetType().Name}): {reason}");
        }

        private string FormatRejections()
        {
            if (_rejectionLog.Count == 0)
                return "제외된 렌더러 없음";

            return "제외 목록 — " + string.Join(", ", _rejectionLog.ToArray());
        }

        /// <summary>어떤 후보들 중에서 무엇을 골랐는지 남깁니다 (엉뚱한 부품이 뽑힐 때 추적용).</summary>
        private static void LogCandidates(List<Renderer> candidates)
        {
            if (candidates.Count <= 1)
                return;

            var sb = new System.Text.StringBuilder("[WeaponAura] 본체 후보 (길이 순):");
            for (int i = 0; i < candidates.Count; i++)
            {
                var r = candidates[i];
                sb.Append($"\n  {(i == 0 ? "→" : " ")} {r.gameObject.name}  " +
                          $"길이 {LongestSideOf(r):0.000}m  정점 {VertexCountOf(r)}");
            }

            UnityEngine.Debug.Log(sb.ToString());
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

        private void Update()
        {
            if (_profile == null)
                return;


            if (_sheets.Count > 0)
            {
                float t = UseUnscaledTime ? Time.unscaledTime : Time.time;
                for (int i = _sheets.Count - 1; i >= 0; i--)
                {
                    if (_sheets[i] == null) { _sheets.RemoveAt(i); continue; }
                    _sheets[i].Tick(t, _profile, _intensity);
                }
            }

            if (_ringTransform == null)
                return;

            _ringYaw += _profile.ringSpeed * (UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
            if (_ringYaw > 360f)
                _ringYaw -= 360f;

            float bob = _profile.ringBob > 0f
                ? Mathf.Sin(Time.time * _profile.ringBobSpeed) * _profile.ringBob
                : 0f;

            // 링은 <b>바닥에 눕는 고리</b>가 기본입니다.
            //
            // ParticleSystemShapeType.Circle은 이미터의 XY 평면에 뿌립니다. 그대로 두면 고리가
            // 세로로 서고, 덕코프는 탑다운이라 위에서 보면 선 하나로 보입니다("링이 잘 안 보인다").
            // X를 90도 먼저 눕혀 두고, 기울기는 그 수평면에서 얼마나 기울일지로 씁니다.
            _ringTransform.localRotation =
                Quaternion.Euler(90f + _profile.ringTilt, _ringYaw, _profile.ringRoll);
            _ringTransform.localPosition = _ringBasePosition + Vector3.up * bob;
        }

        // ──────────────────────────────────────────────────────────
        // 생성
        // ──────────────────────────────────────────────────────────

        private ParticleSystem? CreateWrapParticles(Renderer? meshRenderer)
        {
            var ps = gameObject.AddComponent<ParticleSystem>();
            if (ps == null)
                return null;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;

            // 기본은 로컬 — 무기를 감싼 오라라서 무기와 함께 움직여야 합니다.
            // "지나간 자리에 남기기"를 켜면 월드로 바뀝니다(ApplyToWrap이 갱신).
            main.simulationSpace = WantedSimulationSpace();

            // 루트에서 호스트 스케일을 상쇄했으므로, 파티클 크기도 계층 스케일(=월드 1)을 따르게 해야
            // startSize가 실제 미터로 해석됩니다. Local(기본값)이면 보정된 로컬 스케일이 그대로 곱해집니다.
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.loop = true;
            // 설정 창은 게임이 멈춘 상태에서 열리므로 미리보기는 unscaled 시간이 필요합니다.
            main.useUnscaledTime = UseUnscaledTime;
            // 무기를 넣었다 뺄 때(GameObject 비활성/활성) 자동으로 다시 재생되도록 playOnAwake 사용.
            // 루트를 비활성 상태로 만들어 두고 설정을 마친 뒤 활성화하므로 첫 프레임 튐도 없습니다.
            main.playOnAwake = true;
            main.startRotation3D = false;

            var emission = ps.emission;
            emission.enabled = true;

            ConfigureShape(ps, meshRenderer);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;

            var textureSheet = ps.textureSheetAnimation;
            textureSheet.enabled = false;

            var noise = ps.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.damping = true;

            ConfigureRenderer(ps.GetComponent<ParticleSystemRenderer>(), _profile);
            return ps;
        }

        private ParticleSystem? CreateRingParticles()
        {
            if (_profile == null)
                return null;

            var ringObject = new GameObject("Ring");
            _ringTransform = ringObject.transform;
            _ringTransform.SetParent(transform, false);
            _ringTransform.localPosition = _ringBasePosition;
            _ringTransform.localRotation = Quaternion.identity;

            var ps = ringObject.AddComponent<ParticleSystem>();
            if (ps == null)
                return null;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            // 수명을 무한으로 두면 안 됩니다.
            //
            // 유니티는 파티클 수명으로 렌더 바운즈를 잡는데, 무한이 섞이면 그 계산이
            // 무한/NaN이 되어 렌더러가 통째로 컬링됩니다 — 알갱이는 살아 있는데 화면에
            // 아무것도 안 그려집니다. 링이 안 보이던 원인입니다.
            //
            // 대신 한 주기와 수명을 같게 잡습니다. 주기가 끝나는 순간 옛 알갱이가 죽고
            // 같은 자리에 새로 나므로 끊김 없이 이어집니다(색 곡선은 꺼 두었습니다).
            main.duration = RingCycle;
            main.startLifetime = RingCycle;
            main.startSpeed = 0f;
            main.loop = true;
            // 설정 창은 게임이 멈춘 상태에서 열리므로 미리보기는 unscaled 시간이 필요합니다.
            main.useUnscaledTime = UseUnscaledTime;
            // 무기를 넣었다 뺄 때(GameObject 비활성/활성) 자동으로 다시 재생되도록 playOnAwake 사용.
            // 루트를 비활성 상태로 만들어 두고 설정을 마친 뒤 활성화하므로 첫 프레임 튐도 없습니다.
            main.playOnAwake = true;
            main.maxParticles = Mathf.Max(1, _profile.ringCount);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, _profile.ringCount)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.arc = 360f;
            // BurstSpread는 <b>한 번의 버스트</b>를 호 전체에 고르게 흩뿌립니다.
            // Loop는 시간이 지나며 방출 지점이 도는 방식이라, 한 번에 터뜨리는 우리 방식과
            // 맞지 않아 광점이 한자리에 겹칠 수 있습니다.
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            shape.arcSpread = 0f;
            shape.radiusThickness = 0f;
            shape.alignToDirection = false;

            // 링은 수명이 무한이라 ColorOverLifetime을 쓰면 첫 프레임 색으로 고정됩니다. 비활성 유지.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = false;

            ConfigureRenderer(ps.GetComponent<ParticleSystemRenderer>(), _profile);
            return ps;
        }

        private void ConfigureShape(ParticleSystem ps, Renderer? meshRenderer)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.alignToDirection = false;

            bool meshOk = _profile != null && _profile.useMeshShape && IsUsableMeshRenderer(meshRenderer);
            MeshShapeActive = meshOk;

            if (meshOk && meshRenderer != null)
            {
                if (meshRenderer is SkinnedMeshRenderer skinned)
                {
                    shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                    shape.skinnedMeshRenderer = skinned;
                }
                else
                {
                    shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                    shape.meshRenderer = meshRenderer as MeshRenderer;
                }

                shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                shape.useMeshMaterialIndex = false;
                shape.normalOffset = _profile != null ? _profile.normalOffset : 0.01f;
                // 루트에서 호스트 스케일을 상쇄했으므로 메시 방출은 다시 곱해 원래 크기로 맞춥니다.
                float meshScale = _profile != null ? Mathf.Max(0.01f, _profile.shapeScale) : 1f;
                shape.scale = new Vector3(_hostScale.x, _hostScale.y, _hostScale.z) * meshScale;
            }
            else
            {
                // 메시를 직접 못 쓸 때(읽기 불가 메시 등)는 무기 바운딩 박스 표면에서 방출합니다.
                // 원점에 작은 구를 놓으면 총이 아니라 "손에서" 나오는 것처럼 보이기 때문에,
                // 총 전체를 감싸는 박스 껍데기를 써서 무기 형태를 따라 퍼지게 합니다.
                float scale = _profile != null ? Mathf.Max(0.01f, _profile.shapeScale) : 1f;
                Vector3 size = _boundsSize * scale;

                // 너무 납작한 축은 최소 두께를 줘서 한 면에 몰리지 않게
                float minThickness = Mathf.Max(0.02f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.12f);
                size = new Vector3(
                    Mathf.Max(size.x, minThickness),
                    Mathf.Max(size.y, minThickness),
                    Mathf.Max(size.z, minThickness));

                shape.shapeType = ParticleSystemShapeType.BoxShell;
                shape.scale = size;
                shape.position = _shapeCenter;
                shape.randomDirectionAmount = 1f;
            }
        }

        /// <summary>
        /// 메시 Shape로 쓸 수 있는 렌더러인지 확인합니다.
        /// - 표면적이 0이면 Unity가 매 프레임 "zero surface area" 경고를 뿌립니다.
        /// - isReadable이 false인 메시는 파티클 Shape로 못 쓰고, triangles 접근 자체가 예외입니다.
        /// </summary>
        private static bool IsUsableMeshRenderer(Renderer? renderer)
        {
            if (renderer == null)
                return false;

            try
            {
                Mesh? mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null)
                        mesh = filter.sharedMesh;
                }

                if (mesh == null || mesh.vertexCount <= 0)
                    return false;

                // 읽기 불가 메시는 triangles 접근이 예외를 던지므로 먼저 차단
                if (!mesh.isReadable)
                    return false;

                return mesh.triangles.Length >= 3;
            }
            catch
            {
                return false;
            }
        }

        private void RegisterSubVisuals(CharacterMainControl? holder)
        {
            if (holder == null)
                return;

            try
            {
                var subVisuals = gameObject.AddComponent<CharacterSubVisuals>();
                if (subVisuals == null)
                    return;

                subVisuals.SetRenderers();
                // SetCharacter가 AddSubVisuals까지 호출하고, OnDestroy에서 RemoveVisual도 스스로 처리합니다.
                subVisuals.SetCharacter(holder);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] WeaponAuraController: CharacterSubVisuals 등록 실패: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────
        // 수치 적용
        // ──────────────────────────────────────────────────────────

        /// <summary>알갱이를 무기에 맬지(Local), 생긴 자리에 남길지(World).</summary>
        private ParticleSystemSimulationSpace WantedSimulationSpace()
        {
            return !ForceLocalParticles && _profile != null && _profile.worldTrail
                ? ParticleSystemSimulationSpace.World
                : ParticleSystemSimulationSpace.Local;
        }

        private void ApplyToWrap()
        {
            if (_wrap == null || _profile == null)
                return;

            float alpha = Mathf.Clamp01(_profile.alpha * _intensity);
            float rate = Mathf.Max(0f, _profile.emissionRate * _intensity);
            float lifetime = Mathf.Max(0.05f, _profile.lifetime);

            var main = _wrap.main;

            // 알갱이를 무기에 맬지, 생긴 자리에 남길지.
            //
            // 바꾸는 순간 이미 살아 있는 알갱이는 좌표가 다르게 해석되어 순간이동합니다.
            // 슬라이더를 만지는 중에 그게 보이면 버그로 읽히므로, 바뀔 때만 한 번 지웁니다.
            var wanted = WantedSimulationSpace();

            if (main.simulationSpace != wanted)
            {
                main.simulationSpace = wanted;
                _wrap.Clear();
            }

            main.startLifetime = lifetime;
            main.startSpeed = _profile.speed;
            main.startSize = Mathf.Max(0.001f, _profile.startSize);
            main.gravityModifier = _profile.gravity;
            main.startColor = MakeStartColor(alpha);
            main.maxParticles = Mathf.Clamp(Mathf.CeilToInt(rate * lifetime) + 8, 8, 300);
            main.startRotation = _profile.startRotationRandom > 0.01f
                ? new ParticleSystem.MinMaxCurve(
                    -_profile.startRotationRandom * Mathf.Deg2Rad,
                    _profile.startRotationRandom * Mathf.Deg2Rad)
                : new ParticleSystem.MinMaxCurve(0f);

            var emission = _wrap.emission;
            emission.rateOverTime = rate;

            var colorOverLifetime = _wrap.colorOverLifetime;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                CreateFadeGradient(_profile.fadeIn, _profile.fadeOut));

            var sizeOverLifetime = _wrap.sizeOverLifetime;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                CreateSizeCurve(_profile.sizeStart, _profile.sizePeak, _profile.sizeEnd));

            var rotationOverLifetime = _wrap.rotationOverLifetime;
            rotationOverLifetime.enabled = Mathf.Abs(_profile.rotationSpeed) > 0.01f;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(_profile.rotationSpeed * Mathf.Deg2Rad);

            ApplyTrails(_wrap);
            ApplyDrag(_wrap);

            var noise = _wrap.noise;
            noise.enabled = _profile.noiseStrength > 0.001f;
            noise.strength = _profile.noiseStrength;
            noise.frequency = Mathf.Max(0.0001f, _profile.noiseFrequency);
            noise.scrollSpeed = _profile.noiseScroll;

            var shape = _wrap.shape;
            if (MeshShapeActive)
            {
                shape.normalOffset = _profile.normalOffset;
                shape.scale = _hostScale * Mathf.Max(0.01f, _profile.shapeScale);
            }
            else
            {
                // 박스 껍데기: 무기 바운딩 박스를 배율만큼 키움
                float scale = Mathf.Max(0.01f, _profile.shapeScale);
                Vector3 size = _boundsSize * scale;
                float minThickness = Mathf.Max(0.02f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.12f);
                shape.scale = new Vector3(
                    Mathf.Max(size.x, minThickness),
                    Mathf.Max(size.y, minThickness),
                    Mathf.Max(size.z, minThickness));
                shape.position = _shapeCenter;
            }

            // 메시 방출은 법선 방향이 있지만, 박스 방출은 방향이 없으므로 중심에서 바깥으로 밀어 줍니다.
            var velocity = _wrap.velocityOverLifetime;
            if (MeshShapeActive)
            {
                velocity.enabled = false;
                main.startSpeed = _profile.speed;
            }
            else
            {
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.radial = new ParticleSystem.MinMaxCurve(_profile.speed);
                main.startSpeed = 0f;
            }

            ConfigureRenderer(_wrap.GetComponent<ParticleSystemRenderer>(), _profile);
        }

        /// <summary>
        /// 트레일(꼬리 / 오로라 띠) 설정.
        /// Ribbon 모드에서는 파티클을 나이순으로 이어 붙여 연속된 띠를 만듭니다 — 오로라 형태의 핵심.
        /// </summary>
        private void ApplyTrails(ParticleSystem ps)
        {
            if (_profile == null)
                return;

            bool ribbon = _profile.renderStyle == WeaponAuraRenderStyle.Ribbon;
            var trails = ps.trails;
            trails.enabled = ribbon || _profile.trailEnabled;

            if (!trails.enabled)
                return;

            if (ribbon)
            {
                trails.mode = ParticleSystemTrailMode.Ribbon;
                trails.ribbonCount = Mathf.Max(1, _profile.ribbonCount);
                trails.splitSubEmitterRibbons = false;
                // 리본은 파티클 전체를 이어야 하므로 비율은 항상 1
                trails.ratio = 1f;
            }
            else
            {
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.ratio = Mathf.Clamp01(_profile.trailRatio);
                trails.lifetime = new ParticleSystem.MinMaxCurve(Mathf.Clamp(_profile.trailLifetime, 0.01f, 1f));
                trails.dieWithParticles = true;
            }

            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, _profile.trailWidth));
            trails.inheritParticleColor = true;
            trails.sizeAffectsWidth = true;
        }

        /// <summary>
        /// 감속(drag) 적용. 무기 표면에서 번져 나가다가 서서히 멈춰 주변에 머무는 느낌을 만듭니다.
        /// </summary>
        private void ApplyDrag(ParticleSystem ps)
        {
            if (_profile == null)
                return;

            var limit = ps.limitVelocityOverLifetime;
            bool useDrag = _profile.drag > 0.001f;
            limit.enabled = useDrag;
            if (!useDrag)
                return;

            limit.separateAxes = false;
            limit.dampen = Mathf.Clamp01(_profile.drag);
            limit.limit = new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, _profile.speed * 0.5f));
        }

        private ParticleSystem.MinMaxGradient MakeStartColor(float alpha)
        {
            if (_profile == null)
                return new ParticleSystem.MinMaxGradient(Color.white);

            float intensity = Mathf.Max(0f, _profile.colorIntensity);
            return new ParticleSystem.MinMaxGradient(
                WithAlpha(_profile.colorA * intensity, alpha),
                WithAlpha(_profile.colorB * intensity, alpha));
        }

        private void ApplyToRing()
        {
            if (_ring == null || _profile == null)
                return;

            float alpha = Mathf.Clamp01(_profile.alpha * _intensity);

            var main = _ring.main;
            main.startSize = Mathf.Max(0.001f, _profile.ringSize);
            main.startColor = MakeStartColor(alpha);

            var shape = _ring.shape;
            shape.radius = Mathf.Max(0.01f, _profile.ringRadius);

            ConfigureRenderer(_ring.GetComponent<ParticleSystemRenderer>(), _profile, _profile.ringTexture);
        }

        // ──────────────────────────────────────────────────────────
        // 리소스
        // ──────────────────────────────────────────────────────────

        /// <param name="textureOverride">
        /// 비어 있지 않으면 이 그림을 씁니다. 링은 오라와 다른 문양을 돌릴 수 있어야 합니다.
        /// </param>
        private static void ConfigureRenderer(ParticleSystemRenderer? renderer, WeaponAuraProfile? profile,
            string? textureOverride = null)
        {
            if (renderer == null)
                return;

            var style = profile?.renderStyle ?? WeaponAuraRenderStyle.Billboard;

            if (style == WeaponAuraRenderStyle.Stretched && profile != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = Mathf.Max(0.1f, profile.stretchLength);
                renderer.velocityScale = 0f;
            }
            else if (style == WeaponAuraRenderStyle.Ribbon && profile != null && !profile.ribbonShowHeads)
            {
                // 띠만 남기고 파티클 알갱이는 숨김 (None이면 트레일만 그려집니다)
                renderer.renderMode = ParticleSystemRenderMode.None;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
            }

            string texture = !string.IsNullOrEmpty(textureOverride)
                ? textureOverride!
                : profile?.textureName ?? "";

            var material = WeaponAuraResources.GetMaterial(texture);
            renderer.material = material;
            renderer.trailMaterial = material;

            renderer.sortingFudge = -1f;
            renderer.minParticleSize = 0f;

            // 뷰포트 높이 대비 상한. 기본 0.5는 화면 절반까지 허용해서, 크기 계산이 어긋나면
            // 파티클 하나가 화면을 덮어버립니다. 그래서 상한을 두긴 하는데, 0.06은 너무
            // 좁았습니다.
            //
            // 이건 <b>화면 비율</b> 기준이라 보는 거리에 따라 걸리는 시점이 달라집니다.
            // 미리보기는 무기를 2m 시야로 당겨 보기 때문에 1m짜리 알갱이가 곧바로 이 상한에
            // 걸리는데, 인게임 탑다운 화면에서는 같은 알갱이가 화면의 몇 %라 안 걸립니다.
            // 그래서 같은 설정인데 미리보기만 작게 보였습니다. 크기 슬라이더도 상한이 걸린
            // 구간에서는 올려도 아무 변화가 없습니다.
            renderer.maxParticleSize = 0.25f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>페이드 인/아웃 구간을 반영한 알파 그라디언트</summary>
        private static Gradient CreateFadeGradient(float fadeIn, float fadeOut)
        {
            float inEnd = Mathf.Clamp(fadeIn, 0.001f, 0.98f);
            float outStart = Mathf.Clamp(1f - fadeOut, inEnd + 0.001f, 0.999f);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, inEnd),
                    new GradientAlphaKey(1f, outStart),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static AnimationCurve CreateSizeCurve(float start, float peak, float end)
        {
            return new AnimationCurve(
                new Keyframe(0f, Mathf.Max(0f, start)),
                new Keyframe(0.35f, Mathf.Max(0f, peak)),
                new Keyframe(1f, Mathf.Max(0f, end)));
        }
    }

    /// <summary>
    /// 오라가 공유하는 머티리얼/텍스처. 무기를 바꿀 때마다 새로 만들면 그대로 누수되므로 반드시 캐시합니다.
    ///
    /// 외부 텍스처는 <c>assets/vfx_textures/</c> 안의 PNG/JPG를 파일로 읽어서 씁니다.
    /// (프리팹과 달리 이미지는 AssetBundle 없이 Texture2D.LoadImage로 바로 로드할 수 있습니다.)
    /// </summary>
    public static class WeaponAuraResources
    {
        /// <summary>모드 폴더 기준 텍스처 폴더</summary>
        public const string TextureFolder = "vfx_textures";

        private static Material? _sharedMaterial;
        private static Material? _sheetMaterial;
        private static Texture2D? _sharedTexture;
        private static Texture2D? _whiteTexture;
        private static readonly Dictionary<string, Material> _materialCache =
            new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D?> _textureCache =
            new Dictionary<string, Texture2D?>(StringComparer.OrdinalIgnoreCase);
        private static string[]? _textureNames;

        public static Material SharedMaterial
        {
            get
            {
                if (_sharedMaterial == null)
                    _sharedMaterial = CreateMaterial();
                return _sharedMaterial;
            }
        }

        /// <summary>텍스처 이름에 해당하는 머티리얼 (이름이 비었거나 못 찾으면 내장 글로우)</summary>
        public static Material GetMaterial(string? textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return SharedMaterial;

            string key = textureName!;
            if (_materialCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            // 내장 도형 · 직접 그린 도형 · PNG를 한 곳에서 찾습니다. 예전에는 여기서
            // 파일만 봐서, 설정 창에서 그린 도형을 오라에 고르면 아무 일도 없었습니다.
            var texture = MuzzleFlashShapes.ResolveByName(key);
            if (texture == null)
                return SharedMaterial;

            var material = CreateMaterial();
            material.name = $"WeaponAura_Shell_{key}";
            material.mainTexture = texture;
            _materialCache[key] = material;
            return material;
        }

        /// <summary>
        /// 면(셸) 전용 머티리얼. 파티클용 점 텍스처 대신 흰 텍스처를 써서
        /// 정점 색만으로 그려지게 합니다. 사용자가 텍스처를 고른 경우에는 그것을 씁니다.
        /// </summary>
        public static Material GetSheetMaterial(string? textureName)
        {
            if (!string.IsNullOrEmpty(textureName))
                return GetMaterial(textureName);

            if (_sheetMaterial == null)
                _sheetMaterial = CreateSheetMaterialInternal();

            return _sheetMaterial;
        }

        /// <summary>
        /// 면(셸) 전용 머티리얼.
        ///
        /// 파티클용 셰이더를 쓰면 안 됩니다. 파티클 셰이더는 최종 색을 "정점 색 × 기본 색"으로
        /// 계산하는데, 실루엣은 무기 메시를 그대로 복제한 것이라 무기의 정점 색이 곱해집니다.
        /// 정점 색이 어두운 무기는 결과가 0에 가까워져 아예 보이지 않습니다.
        /// 그래서 정점 색을 쓰지 않는 Unlit 계열을 우선으로 고르고, 알파는 색에 미리 곱해
        /// (premultiplied) One+One 가산으로 그립니다.
        /// </summary>
        private static Material CreateSheetMaterialInternal()
        {
            Shader? shader = FindShader(
                "Universal Render Pipeline/Unlit",   // URP · 정점 색 미사용 (게임이 URP입니다)
                "Unlit/Transparent",
                "Sprites/Default",
                "Particles/Additive");

            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = "WeaponAura_Sheet",
                mainTexture = WhiteTexture,
                hideFlags = HideFlags.HideAndDontSave,
            };

            // 알파를 색에 미리 곱해서 넣으므로 One+One 순수 가산이면 충분합니다.
            // (셰이더가 알파를 어떻게 다루든 결과가 같아집니다)
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);   // Transparent

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

            return material;
        }

        /// <summary>면 전용 흰색 1×1 텍스처</summary>
        public static Texture2D WhiteTexture
        {
            get
            {
                if (_whiteTexture == null)
                {
                    _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                    {
                        name = "WeaponAura_White",
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    _whiteTexture.SetPixel(0, 0, Color.white);
                    _whiteTexture.Apply();
                }

                return _whiteTexture;
            }
        }

        /// <summary>면(셸)에 실제로 쓰인 셰이더 이름 (진단용)</summary>
        public static string ResolvedSheetShaderName
        {
            get
            {
                var material = GetSheetMaterial(null);
                var shader = material != null ? material.shader : null;
                return shader != null ? shader.name : "(없음)";
            }
        }

        /// <summary>현재 해결된 셰이더 이름 (진단용)</summary>
        public static string ResolvedShaderName
        {
            get
            {
                var shader = SharedMaterial != null ? SharedMaterial.shader : null;
                return shader != null ? shader.name : "(없음)";
            }
        }

        /// <summary>assets/vfx_textures 에서 텍스처를 읽습니다 (결과는 실패 포함 캐시).</summary>
        public static Texture2D? LoadTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
                return null;

            if (_textureCache.TryGetValue(textureName, out var cached))
                return cached;

            Texture2D? result = null;
            try
            {
                foreach (string folder in GetTextureFolders())
                {
                    if (result != null)
                        break;

                    foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".tga" })
                    {
                        string path = System.IO.Path.Combine(folder, textureName + extension);
                        if (!System.IO.File.Exists(path))
                            continue;

                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                        {
                            name = textureName,
                            wrapMode = TextureWrapMode.Clamp,
                            filterMode = FilterMode.Bilinear,
                            hideFlags = HideFlags.HideAndDontSave,
                        };

                        if (texture.LoadImage(System.IO.File.ReadAllBytes(path)))
                        {
                            result = texture;
                        }
                        else
                        {
                            UnityEngine.Object.Destroy(texture);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 오라 텍스처 로드 실패({textureName}): {ex.Message}");
            }

            _textureCache[textureName] = result;
            return result;
        }

        /// <summary>선택 가능한 텍스처 이름 목록. 첫 항목은 항상 내장 글로우("")입니다.</summary>
        public static string[] GetTextureNames(bool refresh = false)
        {
            if (_textureNames != null && !refresh)
                return _textureNames;

            var names = new List<string> { "" };
            try
            {
                foreach (string folder in GetTextureFolders())
                {
                    if (!System.IO.Directory.Exists(folder))
                        continue;

                    foreach (string file in System.IO.Directory.GetFiles(folder))
                    {
                        string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".tga")
                            continue;

                        string name = System.IO.Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                            names.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 오라 텍스처 목록 조회 실패: {ex.Message}");
            }

            if (refresh)
            {
                _textureCache.Clear();
                _materialCache.Clear();
            }

            _textureNames = names.ToArray();
            return _textureNames;
        }

        /// <summary>텍스처 폴더 경로 (모드 루트/assets/vfx_textures)</summary>
        /// <summary>
        /// 사용자 이미지를 둘 수 있는 폴더 — <b>여러 곳</b>입니다.
        ///
        /// 모드는 두 자리에 있을 수 있습니다. 창작마당으로 받으면 workshop 폴더에서 돌고,
        /// 직접 넣으면 게임의 Mods 폴더에서 돕니다. 어느 쪽에서 돌든 <b>다른 쪽에 넣어 둔
        /// 이미지도 보여야</b> 합니다 — 개발용으로 로컬에 넣어 둔 것을 창작마당 판으로
        /// 바꿔 켰다고 못 쓰게 되면 곤란합니다.
        ///
        /// 그리고 모드 폴더는 재설치·갱신 때 <b>통째로 지워집니다</b>(설정 파일에서 이미
        /// 겪은 문제입니다). 그래서 사용자 데이터 폴더를 맨 앞에 둡니다 — 거기 넣은 것은
        /// 업데이트에도 남습니다.
        /// </summary>
        public static List<string> GetTextureFolders()
        {
            var folders = new List<string>();

            void Add(string? path)
            {
                if (string.IsNullOrEmpty(path))
                    return;

                if (!folders.Contains(path!))
                    folders.Add(path!);
            }

            // 1순위 — 사용자 데이터 폴더. 업데이트에도 남습니다.
            try
            {
                string user = System.IO.Path.Combine(
                    System.IO.Path.Combine(Application.persistentDataPath, "WeaponAura"), TextureFolder);

                System.IO.Directory.CreateDirectory(user);
                Add(user);
            }
            catch
            {
                // 만들지 못해도 아래 폴더들은 계속 봅니다.
            }

            // 2순위 — 지금 돌고 있는 모드 폴더 (창작마당이든 로컬이든 여기로 잡힙니다)
            AddModFolder(GetModRoot());

            // 3순위 — 게임 Mods 폴더의 같은 이름 모드. 창작마당 판으로 돌 때 로컬에
            // 넣어 둔 이미지를 찾기 위한 길입니다.
            try
            {
                string? dataPath = Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    AddModFolder(System.IO.Path.Combine(
                        System.IO.Path.Combine(dataPath, "Mods"), "WeaponAura"));

                    string? gameRoot = System.IO.Path.GetDirectoryName(dataPath);
                    if (!string.IsNullOrEmpty(gameRoot))
                    {
                        AddModFolder(System.IO.Path.Combine(
                            System.IO.Path.Combine(gameRoot!, "Mods"), "WeaponAura"));
                    }
                }
            }
            catch
            {
                // 경로를 못 만들어도 나머지는 씁니다.
            }

            return folders;

            void AddModFolder(string? root)
            {
                foreach (var path in TexturePathsOf(root))
                    Add(path);
            }
        }

        /// <summary>
        /// 모드 폴더 하나에서 이미지가 있을 수 있는 자리 — <b>두 군데</b>입니다.
        ///
        /// 빌드 방식에 따라 짐이 놓이는 모양이 다릅니다. SDK가 게임에 바로 설치할 때는
        /// <c>assets/</c> 안을 <b>모드 루트로 펼쳐서</b> 복사하므로 <c>&lt;모드&gt;/vfx_textures</c>가
        /// 되고, 창작마당에 올리는 묶음(release)은 <c>assets/</c>를 그대로 들고 가므로
        /// <c>&lt;모드&gt;/assets/vfx_textures</c>가 됩니다.
        ///
        /// 한쪽만 보면 <b>같이 넣어 보낸 PNG를 못 찾습니다</b> — 개발 중 설치판에서
        /// 내장 도형만 나오던 원인이 이것입니다. 둘 다 봅니다.
        /// </summary>
        private static IEnumerable<string> TexturePathsOf(string? root)
        {
            if (string.IsNullOrEmpty(root))
                yield break;

            yield return System.IO.Path.Combine(root!, TextureFolder);
            yield return System.IO.Path.Combine(System.IO.Path.Combine(root!, "assets"), TextureFolder);
        }

        /// <summary>이미지를 넣을 곳으로 안내할 폴더 (사용자 데이터 폴더).</summary>
        public static string? GetUserTextureFolder()
        {
            var folders = GetTextureFolders();
            return folders.Count > 0 ? folders[0] : null;
        }

        /// <summary>모드 루트 폴더 (ModInfo 우선, 실패 시 어셈블리 위치에서 상위 탐색)</summary>
        private static string? GetModRoot()
        {
            try
            {
                string? fromModInfo = Settings.AuraSettings.GetModRootPath();
                if (!string.IsNullOrEmpty(fromModInfo) && System.IO.Directory.Exists(fromModInfo))
                    return fromModInfo;

                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(assemblyPath))
                    return null;

                var dir = new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(assemblyPath) ?? "");
                while (dir != null)
                {
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "assets")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch
            {
                // 무시
            }

            return null;
        }

        private static Material CreateMaterial()
        {
            Shader? shader = FindShader(
                "Particles/Additive",
                "Legacy Shaders/Particles/Additive",
                "Particles/Additive (Soft)",
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Unlit/Transparent",
                "Sprites/Default");

            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = "WeaponAura_Shell",
                mainTexture = SharedTexture,
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);

            // 어떤 셰이더로 폴백하더라도 "가산 · 양면 · 깊이쓰기 없음 · 투명 큐"가 되도록 강제합니다.
            // (면 메시는 파티클과 달리 이 설정이 안 맞으면 불투명 덩어리로 그려집니다.)
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            // URP 계열 셰이더는 키워드/서피스 타입까지 맞춰야 투명으로 그려집니다.
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);   // 1 = Transparent
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 1f);     // 1 = Additive
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

        /// <summary>가운데가 밝고 가장자리로 부드럽게 사라지는 원형 글로우 텍스처</summary>
        public static Texture2D SharedTexture
        {
            get
            {
                if (_sharedTexture == null)
                    _sharedTexture = CreateGlowTexture(64);
                return _sharedTexture;
            }
        }

        private static Texture2D CreateGlowTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WeaponAura_Shell_Glow",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // 중심 1 → 가장자리 0, 제곱 감쇠로 글로우 느낌
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
