using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 파티클을 화면에 그리는 방식
    /// </summary>
    public enum WeaponAuraRenderStyle
    {
        /// <summary>기본 빌보드 (점 형태)</summary>
        Billboard = 0,
        /// <summary>진행 방향으로 늘린 빌보드 (짧은 선)</summary>
        Stretched = 1,
        /// <summary>파티클을 이어 붙인 연속된 띠 — 오로라 형태</summary>
        Ribbon = 2,
        /// <summary>무기를 감싼 면(셸)이 바깥으로 퍼져나가는 형태 — 파티클이 아닌 커튼</summary>
        Sheet = 3,
    }

    /// <summary>
    /// 속성별 프리셋 종류. 형태·색·움직임을 한 번에 바꿉니다.
    /// </summary>
    public enum WeaponAuraPresetKind
    {
        Aurora = 0,
        Fire = 1,
        Frost = 2,
        Toxic = 3,
        Void = 4,
        Shock = 5,
        Holy = 6,
        Blood = 7,
        Arcane = 8,
        Plasma = 9,
        Nature = 10,
        Shadow = 11,
    }

    /// <summary>
    /// 무기 오라 1티어분의 시각 파라미터.
    /// JsonUtility로 직렬화되므로 public 필드만 사용합니다.
    /// </summary>
    [Serializable]
    public class WeaponAuraProfile
    {
        /// <summary>티어 표시 이름 (디버그 패널용)</summary>
        public string name = "";

        /// <summary>
        /// 이 티어에 오라를 적용할지.
        ///
        /// 낮은 등급 무기에는 오라를 원치 않는 경우가 많아서 티어 단위로 끕니다.
        /// 끄면 그 등급 무기를 들었을 때 오라 자체가 만들어지지 않습니다.
        /// (모드 전체를 껐다 켜는 스위치가 아닙니다)
        /// </summary>
        public bool enabled = true;

        /// <summary>이 티어가 적용되기 시작하는 등급 점수 (티어 소스에 따라 아이템 등급 또는 무기 종류 점수)</summary>
        public int minLevel = 1;

        /// <summary>
        /// 설정 창에서 사용자가 + 로 추가한 티어인지.
        /// 기본 7단계는 지울 수 없고, 추가한 것만 지울 수 있습니다.
        /// </summary>
        public bool custom = false;

        // ── 색상 ─────────────────────────────────────────────
        /// <summary>파티클 색상 A (A~B 사이에서 랜덤 선택 → 오로라 그라디언트)</summary>
        public Color colorA = new Color(0.35f, 0.85f, 1f, 1f);
        /// <summary>파티클 색상 B</summary>
        public Color colorB = new Color(0.55f, 0.45f, 1f, 1f);
        /// <summary>전체 불투명도 배율</summary>
        public float alpha = 0.7f;

        // ── 무기 표면 파티클 ─────────────────────────────────
        /// <summary>초당 방출 개수</summary>
        public float emissionRate = 14f;
        /// <summary>파티클 크기</summary>
        public float startSize = 0.05f;
        /// <summary>파티클 수명(초)</summary>
        public float lifetime = 0.7f;
        /// <summary>표면에서 퍼져나가는 속도</summary>
        public float speed = 0.1f;
        /// <summary>중력 배율 (음수면 위로 떠오름)</summary>
        public float gravity = -0.02f;

        /// <summary>
        /// 뿌려진 알갱이를 무기에 매지 않고 <b>월드에 남길지</b>.
        ///
        /// 끄면 알갱이가 무기 좌표계에서 살기 때문에, 움직이면 이미 뿌려진 것까지 통째로
        /// 따라옵니다. 무기를 감싼 오라만 원할 때 그렇게 씁니다.
        ///
        /// 켜면(기본) 알갱이가 생긴 자리에 그대로 박혀서, 지나간 궤적에 문양이 깔립니다.
        /// 다만 화면에 남는 개수가 <b>이동 속도에 비례</b>합니다 — 달리면 길게 깔리므로
        /// 수명과 방출량을 같이 낮춰 잡아야 지저분해지지 않습니다.
        /// </summary>
        public bool worldTrail = true;

        // ── 노이즈 (오로라 흔들림) ────────────────────────────
        public float noiseStrength = 0.2f;
        public float noiseFrequency = 0.4f;
        public float noiseScroll = 0.3f;

        // ── 텍스처 / 렌더 ────────────────────────────────────
        /// <summary>assets/vfx_textures 안의 파일 이름(확장자 제외). 비어 있으면 내장 글로우 사용</summary>
        public string textureName = "";
        /// <summary>플립북 가로 칸 수 (1이면 단일 이미지)</summary>
        public int tilesX = 1;
        /// <summary>플립북 세로 칸 수</summary>
        public int tilesY = 1;
        /// <summary>플립북 재생 속도. 0이면 파티클 수명에 맞춰 1회 재생</summary>
        public float flipbookFps = 0f;
        /// <summary>색 밝기 배율 (가산 합성이라 1을 넘기면 더 발광함)</summary>
        public float colorIntensity = 1f;
        /// <summary>그리기 방식 (빌보드 / 늘림 / 리본)</summary>
        public WeaponAuraRenderStyle renderStyle = WeaponAuraRenderStyle.Billboard;
        /// <summary>Stretched일 때 늘어나는 길이</summary>
        public float stretchLength = 2f;
        /// <summary>Ribbon일 때 몇 가닥으로 나눌지</summary>
        public int ribbonCount = 4;
        /// <summary>Ribbon일 때 파티클 알갱이도 같이 그릴지 (false면 띠만 보임)</summary>
        public bool ribbonShowHeads = false;
        /// <summary>퍼져나가다 감속하는 정도 (0=감속 없음, 1=금방 멈춤) — 무기에서 번져 퍼지는 느낌</summary>
        public float drag = 0.3f;

        // ── 셸(면) — renderStyle이 Sheet일 때 ────────────────
        /// <summary>동시에 퍼지는 면의 겹 수</summary>
        public int sheetLayers = 3;
        /// <summary>
        /// 표면이 법선 방향으로 밀려나는 거리(m). 배율이 아니라 거리라서
        /// 총열처럼 얇은 부분도 같은 두께로 뻗어나갑니다.
        /// </summary>
        public float sheetSpread = 0.12f;
        /// <summary>true면 무기 메시 자체를 셸로 씁니다 (읽기 불가 메시면 자동으로 박스 형태로 폴백)</summary>
        public bool sheetUseWeaponMesh = true;
        /// <summary>폴백 셸의 각짐 정도 (0 = 타원체, 1 = 박스에 가까움)</summary>
        public float sheetBoxiness = 0.65f;
        /// <summary>
        /// 뻗어나가는 방향을 중심에서 바깥(반경) 쪽으로 굽히는 정도.
        /// 0 = 무기 표면 법선 그대로(형태 유지), 1 = 완전한 동심원(퍼질수록 둥글어짐)
        /// </summary>
        public float sheetRoundness = 1f;
        /// <summary>동심원 파동의 개수. 0이면 파동 없이 매끈하게 퍼집니다.</summary>
        public float sheetRings = 3f;
        /// <summary>동심원이 바깥으로 흘러가는 속도</summary>
        public float sheetRingSpeed = 0.6f;
        /// <summary>한 겹이 퍼졌다 사라지는 주기(초)</summary>
        public float sheetPeriod = 2.2f;
        /// <summary>면 표면의 일렁임 크기</summary>
        public float sheetWobble = 0.12f;
        /// <summary>일렁임 속도</summary>
        public float sheetWobbleSpeed = 1.2f;
        /// <summary>파티클 회전 속도(도/초)</summary>
        public float rotationSpeed = 0f;
        /// <summary>초기 회전 랜덤 범위(도)</summary>
        public float startRotationRandom = 180f;

        // ── 수명 곡선 ────────────────────────────────────────
        /// <summary>페이드 인 구간 (수명 대비 0~1)</summary>
        public float fadeIn = 0.25f;
        /// <summary>페이드 아웃 구간 (수명 대비 0~1)</summary>
        public float fadeOut = 0.4f;
        /// <summary>크기 곡선: 시작 / 중간 / 끝 배율</summary>
        public float sizeStart = 0.35f;
        public float sizePeak = 1f;
        public float sizeEnd = 0.2f;

        // ── 트레일 (꼬리) ────────────────────────────────────
        public bool trailEnabled = true;
        /// <summary>트레일이 붙는 파티클 비율 (0~1)</summary>
        public float trailRatio = 0.5f;
        /// <summary>파티클 크기 대비 트레일 폭</summary>
        public float trailWidth = 0.4f;
        /// <summary>파티클 수명 대비 트레일 수명</summary>
        public float trailLifetime = 0.4f;

        // ── Shape ────────────────────────────────────────────
        /// <summary>무기 메시 표면에서 방출할지 여부 (false면 구형 방출)</summary>
        public bool useMeshShape = true;
        /// <summary>메시 Shape 배율</summary>
        public float shapeScale = 1f;
        /// <summary>메시를 못 찾았을 때 사용하는 구 반지름</summary>
        public float fallbackRadius = 0.18f;
        /// <summary>표면에서 살짝 띄우는 거리</summary>
        public float normalOffset = 0.01f;

        // ── 링 (무기 주위를 도는 광점) ────────────────────────
        public bool ringEnabled = false;
        public int ringCount = 6;
        public float ringRadius = 0.3f;
        public float ringSize = 0.05f;
        /// <summary>초당 회전 각도</summary>
        public float ringSpeed = 80f;
        /// <summary>링 기울기(도)</summary>
        public float ringTilt = 20f;
        /// <summary>상하 흔들림 폭</summary>
        public float ringBob = 0.03f;
        public float ringBobSpeed = 2f;

        /// <summary>깊은 복사</summary>
        public WeaponAuraProfile Clone()
        {
            var clone = new WeaponAuraProfile();
            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>다른 프로필의 값을 그대로 덮어씁니다.</summary>
        public void CopyFrom(WeaponAuraProfile other)
        {
            if (other == null)
                return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(other), this);
        }

        /// <summary>구조(Shape/링 구성)가 바뀌었는지 — 바뀌면 파티클 재생성이 필요합니다.</summary>
        public bool StructureDiffers(WeaponAuraProfile other)
        {
            if (other == null)
                return true;
            return useMeshShape != other.useMeshShape
                || ringEnabled != other.ringEnabled
                || ringCount != other.ringCount
                || renderStyle != other.renderStyle
                || ribbonCount != other.ribbonCount
                || ribbonShowHeads != other.ribbonShowHeads
                || sheetLayers != other.sheetLayers
                || sheetUseWeaponMesh != other.sheetUseWeaponMesh
                || !Mathf.Approximately(sheetBoxiness, other.sheetBoxiness);
        }
    }

    /// <summary>
    /// 티어별 프로필 묶음. 런타임 값(<see cref="Runtime"/>)은 디버그 패널에서 실시간 수정되고,
    /// JSON으로 저장/복원할 수 있습니다.
    /// </summary>
    public static class WeaponAuraProfiles
    {
        /// <summary>튜닝 결과 저장 파일명</summary>
        public const string TuningFileName = "weapon_aura_tuning.json";

        [Serializable]
        private class ProfileSetData
        {
            public WeaponAuraProfile[] tiers = Array.Empty<WeaponAuraProfile>();
        }

        private static WeaponAuraProfile[]? _runtime;

        /// <summary>현재 사용 중인 티어 프로필 배열 (수정 가능)</summary>
        public static WeaponAuraProfile[] Runtime
        {
            get
            {
                if (_runtime == null)
                    _runtime = CreateDefaults();
                return _runtime;
            }
        }

        public static int TierCount => Runtime.Length;

        /// <summary>기본 티어 구성</summary>
        /// <summary>
        /// 희귀도 1~7에 1:1로 대응하는 기본 프로필.
        /// 위로 갈수록 색이 강해지고(밝기·채도), 면 겹 수·뻗는 거리·동심원이 함께 늘어납니다.
        /// </summary>
        public static WeaponAuraProfile[] CreateDefaults()
        {
            return new[]
            {
                // 1 — 낡음: 거의 보이지 않는 회청색 숨결
                Rarity("Worn", 1, 0,
                    new Color(0.72f, 0.85f, 0.95f), new Color(0.92f, 0.97f, 1f),
                    alpha: 0.24f, intensity: 1.25f, layers: 1, spread: 0.2f,
                    period: 4f, rings: 1f, ringSpeed: 0.2f, emission: 4f, ring: false),

                // 2 — 일반: 은은한 초록
                Rarity("Common", 2, 1,
                    new Color(0.35f, 1f, 0.45f), new Color(0.7f, 1f, 0.55f),
                    alpha: 0.28f, intensity: 1.35f, layers: 2, spread: 0.2f,
                    period: 3.6f, rings: 2f, ringSpeed: 0.28f, emission: 8f, ring: false),

                // 3 — 고급: 맑은 청색
                Rarity("Fine", 3, 2,
                    new Color(0.25f, 0.6f, 1f), new Color(0.45f, 0.95f, 1f),
                    alpha: 0.32f, intensity: 1.45f, layers: 2, spread: 0.2f,
                    period: 3.2f, rings: 3f, ringSpeed: 0.4f, emission: 14f, ring: false),

                // 4 — 희귀: 보라, 여기서부터 링이 붙습니다
                Rarity("Rare", 4, 3,
                    new Color(0.65f, 0.3f, 1f), new Color(0.9f, 0.55f, 1f),
                    alpha: 0.36f, intensity: 1.55f, layers: 3, spread: 0.2f,
                    period: 2.8f, rings: 4f, ringSpeed: 0.55f, emission: 22f, ring: true),

                // 5 — 영웅: 금색
                Rarity("Epic", 5, 4,
                    new Color(1f, 0.84f, 0.3f), new Color(1f, 0.62f, 0.15f),
                    alpha: 0.40f, intensity: 1.65f, layers: 3, spread: 0.2f,
                    period: 2.4f, rings: 5f, ringSpeed: 0.75f, emission: 30f, ring: true),

                // 6 — 전설: 진홍, 강하게 타오름
                Rarity("Legendary", 6, 5,
                    new Color(1f, 0.55f, 0.15f), new Color(1f, 0.3f, 0.08f),
                    alpha: 0.44f, intensity: 1.75f, layers: 4, spread: 0.2f,
                    period: 1.9f, rings: 6f, ringSpeed: 1f, emission: 38f, ring: true),

                // 7 — 신화: 최대 강도. 기본 제공은 여기까지입니다.
                //     9·999 같은 특수/제작 등급을 따로 꾸미고 싶으면 설정 창에서 + 로 추가합니다.
                Rarity("Mythic", 7, 6,
                    new Color(1f, 0.18f, 0.18f), new Color(1f, 0.45f, 0.3f),
                    alpha: 0.50f, intensity: 1.9f, layers: 5, spread: 0.2f,
                    period: 1.6f, rings: 7f, ringSpeed: 1.3f, emission: 46f, ring: true),
            };
        }

        /// <summary>희귀도 한 단계의 프로필을 만듭니다 (면 모드 · 둥글기 1 고정).</summary>
        /// <param name="minLevel">이 티어가 적용되는 최소 등급(quality) — 임계값</param>
        /// <param name="step">시각 강도 단계 0~9 — 크기·수명·링 개수 등 파생값 계산에 사용.
        /// 임계값과 분리해야 quality 999 같은 값이 링 1003개를 만드는 사고가 없습니다.</param>
        private static WeaponAuraProfile Rarity(string name, int minLevel, int step, Color a, Color b,
            float alpha, float intensity, int layers, float spread,
            float period, float rings, float ringSpeed, float emission, bool ring)
        {
            return new WeaponAuraProfile
            {
                name = name,
                minLevel = minLevel,

                colorA = a,
                colorB = b,
                alpha = alpha,
                colorIntensity = intensity,

                emissionRate = emission,
                startSize = Mathf.Lerp(0.03f, 0.055f, step / 9f),
                lifetime = Mathf.Lerp(1f, 1.8f, step / 9f),
                speed = Mathf.Lerp(0.05f, 0.12f, step / 9f),
                gravity = -0.02f,
                drag = 0.55f,

                noiseStrength = Mathf.Lerp(0.15f, 0.55f, step / 9f),
                noiseFrequency = 0.3f,
                noiseScroll = Mathf.Lerp(0.15f, 0.5f, step / 9f),

                renderStyle = WeaponAuraRenderStyle.Sheet,
                sheetLayers = layers,
                sheetSpread = spread,
                sheetPeriod = period,
                sheetRoundness = 1f,
                sheetRings = rings,
                sheetRingSpeed = ringSpeed,
                sheetWobble = Mathf.Lerp(0.08f, 0.28f, step / 9f),
                sheetWobbleSpeed = Mathf.Lerp(0.6f, 1.8f, step / 9f),
                sheetBoxiness = 0.45f,
                sheetUseWeaponMesh = true,

                fadeIn = 0.25f,
                fadeOut = 0.45f,
                sizeStart = 0.3f,
                sizePeak = 1f,
                sizeEnd = 0.5f,
                normalOffset = 0.012f,

                ringEnabled = ring,
                ringCount = 4 + step,
                ringRadius = 0.3f,
                ringSize = Mathf.Lerp(0.028f, 0.045f, step / 9f),
                ringSpeed = 40f + step * 10f,
                ringTilt = 15f,
                ringBob = 0.02f,
                ringBobSpeed = 1.4f,
            };
        }

        /// <summary>
        /// 무기 표면에서 띠가 번져 나가는 오로라 프리셋.
        /// 파티클을 Ribbon으로 이어 붙이고, 방출 속도는 낮추되 수명을 길게 가져가서
        /// 무기 실루엣을 감싸며 흐르는 형태를 만듭니다.
        /// </summary>
        public static WeaponAuraProfile CreateAuroraProfile(string name = "Aurora", int minLevel = 4)
        {
            return new WeaponAuraProfile
            {
                name = name,
                minLevel = minLevel,

                colorA = new Color(0.7f, 0.3f, 1f, 1f),
                colorB = new Color(0.25f, 0.95f, 0.9f, 1f),
                alpha = 0.75f,
                colorIntensity = 1.6f,

                // 띠가 끊기지 않게 방출은 꾸준히, 수명은 길게
                emissionRate = 45f,
                startSize = 0.05f,
                lifetime = 1.8f,
                speed = 0.09f,          // 표면 법선 방향으로 천천히 번져 나감
                gravity = -0.015f,
                drag = 0.45f,           // 번지다가 감속 → 무기 주변에 머무름

                // 느리고 큰 노이즈가 오로라 특유의 일렁임을 만듭니다
                noiseStrength = 0.55f,
                noiseFrequency = 0.22f,
                noiseScroll = 0.35f,

                useMeshShape = true,
                shapeScale = 1f,
                fallbackRadius = 0.2f,
                normalOffset = 0.02f,

                renderStyle = WeaponAuraRenderStyle.Ribbon,
                ribbonCount = 4,
                trailEnabled = true,
                trailRatio = 1f,
                trailWidth = 0.7f,
                trailLifetime = 1f,

                fadeIn = 0.2f,
                fadeOut = 0.45f,
                sizeStart = 0.3f,
                sizePeak = 1f,
                sizeEnd = 0.55f,

                ringEnabled = false,
            };
        }

        /// <summary>
        /// 속성별 프리셋. 형태(파티클/리본/면)와 움직임까지 함께 바뀝니다.
        /// </summary>
        public static WeaponAuraProfile CreatePreset(WeaponAuraPresetKind kind, string name, int minLevel)
        {
            switch (kind)
            {
                case WeaponAuraPresetKind.Aurora:
                    return CreateAuroraProfile(name, minLevel);

                // 화염 — 위로 솟는 면 + 불티. 짧은 수명, 강한 난류.
                case WeaponAuraPresetKind.Fire:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(1f, 0.75f, 0.2f, 1f),
                        colorB = new Color(1f, 0.25f, 0.05f, 1f),
                        alpha = 0.55f, colorIntensity = 1.9f,
                        emissionRate = 34f, startSize = 0.045f, lifetime = 0.8f,
                        speed = 0.12f, gravity = -0.16f, drag = 0.25f,   // 음수 중력 = 위로 솟음
                        noiseStrength = 0.7f, noiseFrequency = 0.7f, noiseScroll = 0.9f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 3, sheetSpread = 0.1f, sheetPeriod = 1.1f, sheetBoxiness = 0.6f,
                        sheetRoundness = 1f, sheetRings = 5f, sheetRingSpeed = 1.4f,
                        sheetWobble = 0.22f, sheetWobbleSpeed = 3.2f,
                        fadeIn = 0.12f, fadeOut = 0.55f,
                        sizeStart = 0.5f, sizePeak = 1f, sizeEnd = 0.15f,
                        normalOffset = 0.015f,
                    });

                // 냉기 — 느리게 퍼지는 얇은 면 + 반짝이는 링.
                case WeaponAuraPresetKind.Frost:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.6f, 0.92f, 1f, 1f),
                        colorB = new Color(0.85f, 0.97f, 1f, 1f),
                        alpha = 0.4f, colorIntensity = 1.5f,
                        emissionRate = 12f, startSize = 0.03f, lifetime = 1.6f,
                        speed = 0.05f, gravity = 0.02f, drag = 0.7f,     // 양수 중력 = 서리가 가라앉음
                        noiseStrength = 0.14f, noiseFrequency = 0.3f, noiseScroll = 0.12f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 2, sheetSpread = 0.055f, sheetPeriod = 3.4f, sheetBoxiness = 0.8f,
                        sheetRoundness = 1f, sheetRings = 8f, sheetRingSpeed = 0.35f,
                        sheetWobble = 0.05f, sheetWobbleSpeed = 0.6f,
                        fadeIn = 0.3f, fadeOut = 0.5f,
                        sizeStart = 0.2f, sizePeak = 1f, sizeEnd = 0.6f,
                        ringEnabled = true, ringCount = 7, ringRadius = 0.3f, ringSize = 0.03f,
                        ringSpeed = 35f, ringTilt = 12f, ringBob = 0.015f, ringBobSpeed = 1f,
                    });

                // 독 — 무겁게 깔리는 면, 느린 일렁임.
                case WeaponAuraPresetKind.Toxic:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.55f, 1f, 0.25f, 1f),
                        colorB = new Color(0.15f, 0.6f, 0.2f, 1f),
                        alpha = 0.5f, colorIntensity = 1.4f,
                        emissionRate = 20f, startSize = 0.055f, lifetime = 1.5f,
                        speed = 0.06f, gravity = 0.05f, drag = 0.6f,
                        noiseStrength = 0.35f, noiseFrequency = 0.25f, noiseScroll = 0.3f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 3, sheetSpread = 0.13f, sheetPeriod = 2.8f, sheetBoxiness = 0.5f,
                        sheetRoundness = 1f, sheetRings = 3f, sheetRingSpeed = 0.5f,
                        sheetWobble = 0.18f, sheetWobbleSpeed = 1f,
                        fadeIn = 0.25f, fadeOut = 0.5f,
                        sizeStart = 0.4f, sizePeak = 1.1f, sizeEnd = 0.5f,
                    });

                // 공허 — 어둡고 넓게 퍼지는 면, 안쪽으로 빨려드는 링.
                case WeaponAuraPresetKind.Void:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.45f, 0.15f, 0.75f, 1f),
                        colorB = new Color(0.1f, 0.05f, 0.3f, 1f),
                        alpha = 0.6f, colorIntensity = 1.7f,
                        emissionRate = 18f, startSize = 0.06f, lifetime = 1.9f,
                        speed = 0.04f, gravity = 0f, drag = 0.8f,
                        noiseStrength = 0.3f, noiseFrequency = 0.18f, noiseScroll = 0.2f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 4, sheetSpread = 0.2f, sheetPeriod = 3.2f, sheetBoxiness = 0.4f,
                        sheetRoundness = 1f, sheetRings = 2f, sheetRingSpeed = -0.8f,
                        sheetWobble = 0.14f, sheetWobbleSpeed = 0.8f,
                        fadeIn = 0.35f, fadeOut = 0.45f,
                        sizeStart = 0.3f, sizePeak = 1f, sizeEnd = 0.7f,
                        ringEnabled = true, ringCount = 6, ringRadius = 0.36f, ringSize = 0.04f,
                        ringSpeed = -120f, ringTilt = 70f, ringBob = 0.02f, ringBobSpeed = 1.6f,
                    });

                // 전격 — 짧고 빠른 리본이 튀는 형태.
                case WeaponAuraPresetKind.Shock:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.6f, 0.85f, 1f, 1f),
                        colorB = new Color(1f, 1f, 0.75f, 1f),
                        alpha = 0.8f, colorIntensity = 2.2f,
                        emissionRate = 60f, startSize = 0.03f, lifetime = 0.4f,
                        speed = 0.35f, gravity = 0f, drag = 0.15f,
                        noiseStrength = 1.1f, noiseFrequency = 1.4f, noiseScroll = 1.6f,
                        renderStyle = WeaponAuraRenderStyle.Ribbon,
                        ribbonCount = 7, trailEnabled = true, trailRatio = 1f,
                        trailWidth = 0.35f, trailLifetime = 1f,
                        fadeIn = 0.05f, fadeOut = 0.3f,
                        sizeStart = 0.8f, sizePeak = 1f, sizeEnd = 0.3f,
                    });

                // 신성 — 넓고 부드러운 금빛 맥동 + 도는 광점
                case WeaponAuraPresetKind.Holy:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(1f, 0.95f, 0.7f, 1f),
                        colorB = new Color(1f, 0.78f, 0.3f, 1f),
                        alpha = 0.5f, colorIntensity = 2f,
                        emissionRate = 16f, startSize = 0.04f, lifetime = 1.4f,
                        speed = 0.06f, gravity = -0.03f, drag = 0.6f,
                        noiseStrength = 0.12f, noiseFrequency = 0.2f, noiseScroll = 0.15f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 3, sheetSpread = 0.16f, sheetPeriod = 2.6f,
                        sheetRoundness = 1f, sheetRings = 2f, sheetRingSpeed = 0.45f,
                        sheetWobble = 0.1f, sheetWobbleSpeed = 0.8f, sheetBoxiness = 0.3f,
                        fadeIn = 0.3f, fadeOut = 0.5f,
                        ringEnabled = true, ringCount = 8, ringRadius = 0.32f, ringSize = 0.035f,
                        ringSpeed = 45f, ringTilt = 8f, ringBob = 0.02f, ringBobSpeed = 1.2f,
                    });

                // 혈기 — 느리고 무거운 진홍 맥동
                case WeaponAuraPresetKind.Blood:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.85f, 0.1f, 0.15f, 1f),
                        colorB = new Color(0.3f, 0.02f, 0.05f, 1f),
                        alpha = 0.62f, colorIntensity = 1.5f,
                        emissionRate = 14f, startSize = 0.06f, lifetime = 2f,
                        speed = 0.04f, gravity = 0.06f, drag = 0.75f,
                        noiseStrength = 0.25f, noiseFrequency = 0.15f, noiseScroll = 0.18f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 2, sheetSpread = 0.14f, sheetPeriod = 3.6f,
                        sheetRoundness = 1f, sheetRings = 1.5f, sheetRingSpeed = 0.25f,
                        sheetWobble = 0.3f, sheetWobbleSpeed = 0.5f, sheetBoxiness = 0.5f,
                        fadeIn = 0.35f, fadeOut = 0.45f,
                    });

                // 비전 — 촘촘한 얇은 링이 빠르게 흐르고 룬처럼 도는 광점
                case WeaponAuraPresetKind.Arcane:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.35f, 0.5f, 1f, 1f),
                        colorB = new Color(0.8f, 0.45f, 1f, 1f),
                        alpha = 0.55f, colorIntensity = 1.8f,
                        emissionRate = 22f, startSize = 0.032f, lifetime = 1.2f,
                        speed = 0.07f, gravity = -0.02f, drag = 0.5f,
                        noiseStrength = 0.2f, noiseFrequency = 0.4f, noiseScroll = 0.4f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 4, sheetSpread = 0.15f, sheetPeriod = 2f,
                        sheetRoundness = 1f, sheetRings = 9f, sheetRingSpeed = 1.1f,
                        sheetWobble = 0.14f, sheetWobbleSpeed = 1.5f, sheetBoxiness = 0.35f,
                        fadeIn = 0.2f, fadeOut = 0.45f,
                        ringEnabled = true, ringCount = 6, ringRadius = 0.34f, ringSize = 0.03f,
                        ringSpeed = 90f, ringTilt = 78f, ringBob = 0.01f, ringBobSpeed = 2f,
                    });

                // 플라즈마 — 고주파로 진동하는 마젠타/시안
                case WeaponAuraPresetKind.Plasma:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(1f, 0.25f, 0.85f, 1f),
                        colorB = new Color(0.25f, 0.95f, 1f, 1f),
                        alpha = 0.6f, colorIntensity = 2.3f,
                        emissionRate = 40f, startSize = 0.03f, lifetime = 0.7f,
                        speed = 0.2f, gravity = 0f, drag = 0.35f,
                        noiseStrength = 0.8f, noiseFrequency = 1f, noiseScroll = 1.2f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 3, sheetSpread = 0.12f, sheetPeriod = 0.8f,
                        sheetRoundness = 1f, sheetRings = 6f, sheetRingSpeed = 2.4f,
                        sheetWobble = 0.35f, sheetWobbleSpeed = 4.5f, sheetBoxiness = 0.4f,
                        fadeIn = 0.1f, fadeOut = 0.4f,
                    });

                // 자연 — 아주 느린 큰 호흡, 은은한 초록
                case WeaponAuraPresetKind.Nature:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.45f, 1f, 0.5f, 1f),
                        colorB = new Color(0.9f, 1f, 0.4f, 1f),
                        alpha = 0.42f, colorIntensity = 1.3f,
                        emissionRate = 10f, startSize = 0.04f, lifetime = 2.2f,
                        speed = 0.04f, gravity = -0.04f, drag = 0.65f,
                        noiseStrength = 0.18f, noiseFrequency = 0.2f, noiseScroll = 0.2f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 2, sheetSpread = 0.18f, sheetPeriod = 4.2f,
                        sheetRoundness = 1f, sheetRings = 1f, sheetRingSpeed = 0.2f,
                        sheetWobble = 0.22f, sheetWobbleSpeed = 0.45f, sheetBoxiness = 0.3f,
                        fadeIn = 0.35f, fadeOut = 0.45f,
                    });

                // 그림자 — 낮은 알파로 어둡게 번지는 남색
                case WeaponAuraPresetKind.Shadow:
                    return Make(name, minLevel, new WeaponAuraProfile
                    {
                        colorA = new Color(0.2f, 0.22f, 0.45f, 1f),
                        colorB = new Color(0.5f, 0.35f, 0.7f, 1f),
                        alpha = 0.45f, colorIntensity = 1.2f,
                        emissionRate = 24f, startSize = 0.07f, lifetime = 1.7f,
                        speed = 0.05f, gravity = 0.01f, drag = 0.7f,
                        noiseStrength = 0.5f, noiseFrequency = 0.3f, noiseScroll = 0.35f,
                        renderStyle = WeaponAuraRenderStyle.Sheet,
                        sheetLayers = 4, sheetSpread = 0.17f, sheetPeriod = 2.4f,
                        sheetRoundness = 1f, sheetRings = 4f, sheetRingSpeed = -0.5f,
                        sheetWobble = 0.4f, sheetWobbleSpeed = 1f, sheetBoxiness = 0.35f,
                        fadeIn = 0.3f, fadeOut = 0.5f,
                    });

                default:
                    return CreateAuroraProfile(name, minLevel);
            }
        }

        /// <summary>
        /// 시드 기반 랜덤 프로필. 같은 시드는 항상 같은 결과라서
        /// 마음에 드는 조합이 나오면 시드만 적어 두면 됩니다.
        ///
        /// 완전 무작위가 아니라 "보기 좋은 범위" 안에서만 굴립니다.
        /// 색은 색상환에서 기준 색을 뽑고 유사색/보색 중 하나를 짝지어 조화를 유지합니다.
        /// </summary>
        public static WeaponAuraProfile CreateRandom(int seed, string name, int minLevel)
        {
            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Chance(float p) => rng.NextDouble() < p;

            float hue = (float)rng.NextDouble();
            // 유사색(차분) / 보색(대비) / 삼각(화려) 중 하나
            float offset = Chance(0.45f) ? Range(0.04f, 0.12f)
                         : Chance(0.6f) ? Range(0.45f, 0.55f)
                         : Range(0.28f, 0.36f);

            float sat = Range(0.55f, 0.95f);
            var colorA = Color.HSVToRGB(hue, sat, 1f);
            var colorB = Color.HSVToRGB(Mathf.Repeat(hue + offset, 1f), Range(0.45f, 0.9f), 1f);

            // 느린/보통/빠른 셋 중 하나의 성격을 고르고 나머지를 거기에 맞춥니다
            int tempo = rng.Next(3);
            float period = tempo == 0 ? Range(3f, 4.5f) : tempo == 1 ? Range(1.8f, 3f) : Range(0.7f, 1.6f);
            float rings = tempo == 0 ? Range(1f, 3f) : tempo == 1 ? Range(2f, 6f) : Range(4f, 10f);
            float ringSpeed = Range(0.2f, 1.6f) * (Chance(0.25f) ? -1f : 1f);

            bool ring = Chance(0.4f);

            return new WeaponAuraProfile
            {
                name = name,
                minLevel = minLevel,

                colorA = colorA,
                colorB = colorB,
                alpha = Range(0.4f, 0.7f),
                colorIntensity = Range(1.2f, 2.2f),

                emissionRate = Range(10f, 40f),
                startSize = Range(0.03f, 0.06f),
                lifetime = Range(0.8f, 2f),
                speed = Range(0.04f, 0.16f),
                gravity = Range(-0.1f, 0.06f),
                drag = Range(0.3f, 0.75f),

                noiseStrength = Range(0.15f, 0.7f),
                noiseFrequency = Range(0.2f, 0.8f),
                noiseScroll = Range(0.2f, 0.8f),

                renderStyle = WeaponAuraRenderStyle.Sheet,
                sheetLayers = rng.Next(2, 5),
                sheetSpread = Range(0.1f, 0.22f),
                sheetPeriod = period,
                sheetRoundness = 1f,
                sheetRings = rings,
                sheetRingSpeed = ringSpeed,
                sheetWobble = Range(0.08f, 0.35f),
                sheetWobbleSpeed = Range(0.5f, 3f),
                sheetBoxiness = Range(0.25f, 0.55f),

                fadeIn = Range(0.15f, 0.35f),
                fadeOut = Range(0.4f, 0.55f),
                sizeStart = Range(0.25f, 0.5f),
                sizePeak = 1f,
                sizeEnd = Range(0.2f, 0.6f),

                ringEnabled = ring,
                ringCount = rng.Next(4, 10),
                ringRadius = Range(0.26f, 0.38f),
                ringSize = Range(0.028f, 0.05f),
                ringSpeed = Range(30f, 120f) * (Chance(0.3f) ? -1f : 1f),
                ringTilt = Range(0f, 80f),
                ringBob = Range(0.01f, 0.035f),
                ringBobSpeed = Range(0.8f, 2.4f),
            };
        }

        /// <summary>기본값 위에 프리셋 값을 얹습니다.</summary>
        private static WeaponAuraProfile Make(string name, int minLevel, WeaponAuraProfile overrides)
        {
            var p = overrides.Clone();
            p.name = name;
            p.minLevel = minLevel;
            return p;
        }

        /// <summary>
        /// 레벨에 해당하는 티어 인덱스.
        ///
        /// 가장 낮은 티어의 기준값(기본 1)보다 낮은 등급도 그 티어를 씁니다.
        /// 안 그러면 등급 0짜리 무기만 오라가 통째로 사라집니다.
        /// </summary>
        public static int ResolveTier(int level)
        {
            var tiers = Runtime;
            if (tiers.Length == 0)
                return -1;

            int result = -1;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i] != null && level >= tiers[i].minLevel)
                    result = i;
            }

            return result >= 0 ? result : 0;
        }

        // ── 티어 추가 / 삭제 ─────────────────────────────────────────

        /// <summary>이 등급 값을 쓰는 티어가 이미 있는지</summary>
        public static bool HasLevel(int minLevel)
        {
            foreach (var tier in Runtime)
            {
                if (tier != null && tier.minLevel == minLevel)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 등급 값 하나에 대한 티어를 새로 만듭니다.
        ///
        /// ResolveTier가 "기준값 오름차순"을 전제로 하므로 정렬된 자리에 끼워 넣습니다.
        /// 바로 아래 티어를 복사해서 시작값으로 쓰기 때문에 추가 직후에도 어색하지 않습니다.
        /// </summary>
        /// <returns>새 티어의 인덱스. 이미 같은 등급이 있으면 -1.</returns>
        public static int AddTier(int minLevel)
        {
            if (HasLevel(minLevel))
                return -1;

            var tiers = Runtime;

            int insertAt = tiers.Length;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i] != null && tiers[i].minLevel > minLevel)
                {
                    insertAt = i;
                    break;
                }
            }

            var template = insertAt > 0 ? tiers[insertAt - 1] : (tiers.Length > 0 ? tiers[0] : null);
            var created = template != null ? template.Clone() : new WeaponAuraProfile();

            created.minLevel = minLevel;
            created.name = $"Custom {minLevel}";
            created.custom = true;
            created.enabled = true;

            var list = new System.Collections.Generic.List<WeaponAuraProfile>(tiers);
            list.Insert(insertAt, created);
            _runtime = list.ToArray();

            return insertAt;
        }

        /// <summary>추가한 티어를 지웁니다. 기본 티어는 지울 수 없습니다.</summary>
        public static bool RemoveTier(int index)
        {
            var tiers = Runtime;
            if (index < 0 || index >= tiers.Length)
                return false;

            if (tiers[index] == null || !tiers[index].custom)
                return false;

            var list = new System.Collections.Generic.List<WeaponAuraProfile>(tiers);
            list.RemoveAt(index);
            _runtime = list.ToArray();
            return true;
        }

        /// <summary>티어 프로필 조회 (범위를 벗어나면 null)</summary>
        public static WeaponAuraProfile? Get(int tier)
        {
            var tiers = Runtime;
            if (tier < 0 || tier >= tiers.Length)
                return null;
            return tiers[tier];
        }

        /// <summary>런타임 값을 기본값으로 되돌립니다.</summary>
        public static void ResetToDefaults()
        {
            _runtime = CreateDefaults();
        }

        /// <summary>저장 폴더 (모드 폴더 우선, 없으면 persistentDataPath)</summary>
        public static string GetSaveFolder()
        {
            string? root = null;
            try
            {
                root = Settings.AuraSettings.GetModRootPath();
            }
            catch
            {
                root = null;
            }

            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                root = Application.persistentDataPath;

            return root!;
        }

        /// <summary>
        /// 저장 파일 경로. slot이 비어 있으면 기본 파일(자동 로드 대상),
        /// 값이 있으면 프리셋 슬롯 파일입니다.
        /// </summary>
        public static string GetTuningPath(string? slot = null)
        {
            string fileName = string.IsNullOrEmpty(slot)
                ? TuningFileName
                : $"weapon_aura_preset_{slot}.json";

            return Path.Combine(GetSaveFolder(), fileName);
        }

        /// <summary>현재 런타임 값을 JSON으로 저장합니다.</summary>
        public static bool Save(out string path, string? slot = null)
        {
            path = GetTuningPath(slot);
            try
            {
                var data = new ProfileSetData { tiers = Runtime };
                File.WriteAllText(path, JsonUtility.ToJson(data, true), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 저장 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>JSON에서 런타임 값을 복원합니다.</summary>
        public static bool Load(out string path, string? slot = null)
        {
            path = GetTuningPath(slot);
            try
            {
                if (!File.Exists(path))
                    return false;

                var data = JsonUtility.FromJson<ProfileSetData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null || data.tiers == null || data.tiers.Length == 0)
                    return false;

                _runtime = data.tiers;
                Repair(_runtime);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 불러오기 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>해당 슬롯에 저장된 파일이 있는지</summary>
        /// <summary>
        /// 불러온 설정을 지금 쓸 수 있는 상태로 손봅니다.
        ///
        /// 예전 버전에서 오로라·전격 템플릿을 적용하면 renderStyle이 Ribbon으로 저장됐습니다.
        /// Ribbon은 무기를 감싸는 껍질을 만들지 않아서 오라가 꺼진 것처럼 보이고,
        /// 그 상태로 저장되면 다시 켤 방법이 없습니다. 불러올 때 되돌려 놓습니다.
        /// </summary>
        private static void Repair(WeaponAuraProfile[] tiers)
        {
            int fixedCount = 0;

            foreach (var tier in tiers)
            {
                if (tier == null || tier.renderStyle == WeaponAuraRenderStyle.Sheet)
                    continue;

                tier.renderStyle = WeaponAuraRenderStyle.Sheet;
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                UnityEngine.Debug.Log(
                    $"[WeaponAura] 저장된 설정에서 껍질이 없는 티어 {fixedCount}개를 복구했습니다.");
            }
        }

        public static bool SlotExists(string slot)
        {
            try
            {
                return File.Exists(GetTuningPath(slot));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 모드 시작 시 기본 저장 파일을 자동으로 불러옵니다.
        /// 색을 커스텀했으면 게임을 껐다 켜도 유지되는 게 자연스럽습니다.
        /// </summary>
        public static void AutoLoad()
        {
            if (Load(out string path))
                UnityEngine.Debug.Log($"[WeaponAura] 저장된 색 설정을 불러왔습니다: {path}");
        }

        /// <summary>
        /// 현재 값을 CreateDefaults()에 그대로 붙여넣을 수 있는 C# 코드로 변환합니다.
        /// (디버그 패널에서 튜닝한 결과를 코드로 굳힐 때 사용)
        /// </summary>
        public static string ToCSharpSnippet()
        {
            var sb = new StringBuilder();
            var tiers = Runtime;
            for (int i = 0; i < tiers.Length; i++)
            {
                var p = tiers[i];
                if (p == null)
                    continue;

                sb.AppendLine("new WeaponAuraProfile");
                sb.AppendLine("{");
                sb.AppendLine($"    name = \"{p.name}\",");
                sb.AppendLine($"    minLevel = {p.minLevel},");
                sb.AppendLine($"    colorA = new Color({F(p.colorA.r)}, {F(p.colorA.g)}, {F(p.colorA.b)}, 1f),");
                sb.AppendLine($"    colorB = new Color({F(p.colorB.r)}, {F(p.colorB.g)}, {F(p.colorB.b)}, 1f),");
                sb.AppendLine($"    alpha = {F(p.alpha)},");
                sb.AppendLine($"    emissionRate = {F(p.emissionRate)},");
                sb.AppendLine($"    startSize = {F(p.startSize)},");
                sb.AppendLine($"    lifetime = {F(p.lifetime)},");
                sb.AppendLine($"    speed = {F(p.speed)},");
                sb.AppendLine($"    gravity = {F(p.gravity)},");
                sb.AppendLine($"    noiseStrength = {F(p.noiseStrength)},");
                sb.AppendLine($"    noiseFrequency = {F(p.noiseFrequency)},");
                sb.AppendLine($"    noiseScroll = {F(p.noiseScroll)},");
                sb.AppendLine($"    textureName = \"{p.textureName}\",");
                sb.AppendLine($"    tilesX = {p.tilesX},");
                sb.AppendLine($"    tilesY = {p.tilesY},");
                sb.AppendLine($"    flipbookFps = {F(p.flipbookFps)},");
                sb.AppendLine($"    colorIntensity = {F(p.colorIntensity)},");
                sb.AppendLine($"    renderStyle = WeaponAuraRenderStyle.{p.renderStyle},");
                sb.AppendLine($"    stretchLength = {F(p.stretchLength)},");
                sb.AppendLine($"    ribbonCount = {p.ribbonCount},");
                sb.AppendLine($"    ribbonShowHeads = {(p.ribbonShowHeads ? "true" : "false")},");
                sb.AppendLine($"    sheetLayers = {p.sheetLayers},");
                sb.AppendLine($"    sheetSpread = {F(p.sheetSpread)},");
                sb.AppendLine($"    sheetUseWeaponMesh = {(p.sheetUseWeaponMesh ? "true" : "false")},");
                sb.AppendLine($"    sheetBoxiness = {F(p.sheetBoxiness)},");
                sb.AppendLine($"    sheetRoundness = {F(p.sheetRoundness)},");
                sb.AppendLine($"    sheetRings = {F(p.sheetRings)},");
                sb.AppendLine($"    sheetRingSpeed = {F(p.sheetRingSpeed)},");
                sb.AppendLine($"    sheetPeriod = {F(p.sheetPeriod)},");
                sb.AppendLine($"    sheetWobble = {F(p.sheetWobble)},");
                sb.AppendLine($"    sheetWobbleSpeed = {F(p.sheetWobbleSpeed)},");
                sb.AppendLine($"    drag = {F(p.drag)},");
                sb.AppendLine($"    rotationSpeed = {F(p.rotationSpeed)},");
                sb.AppendLine($"    startRotationRandom = {F(p.startRotationRandom)},");
                sb.AppendLine($"    fadeIn = {F(p.fadeIn)},");
                sb.AppendLine($"    fadeOut = {F(p.fadeOut)},");
                sb.AppendLine($"    sizeStart = {F(p.sizeStart)},");
                sb.AppendLine($"    sizePeak = {F(p.sizePeak)},");
                sb.AppendLine($"    sizeEnd = {F(p.sizeEnd)},");
                sb.AppendLine($"    trailEnabled = {(p.trailEnabled ? "true" : "false")},");
                sb.AppendLine($"    trailRatio = {F(p.trailRatio)},");
                sb.AppendLine($"    trailWidth = {F(p.trailWidth)},");
                sb.AppendLine($"    trailLifetime = {F(p.trailLifetime)},");
                sb.AppendLine($"    useMeshShape = {(p.useMeshShape ? "true" : "false")},");
                sb.AppendLine($"    shapeScale = {F(p.shapeScale)},");
                sb.AppendLine($"    fallbackRadius = {F(p.fallbackRadius)},");
                sb.AppendLine($"    normalOffset = {F(p.normalOffset)},");
                sb.AppendLine($"    ringEnabled = {(p.ringEnabled ? "true" : "false")},");
                sb.AppendLine($"    ringCount = {p.ringCount},");
                sb.AppendLine($"    ringRadius = {F(p.ringRadius)},");
                sb.AppendLine($"    ringSize = {F(p.ringSize)},");
                sb.AppendLine($"    ringSpeed = {F(p.ringSpeed)},");
                sb.AppendLine($"    ringTilt = {F(p.ringTilt)},");
                sb.AppendLine($"    ringBob = {F(p.ringBob)},");
                sb.AppendLine($"    ringBobSpeed = {F(p.ringBobSpeed)},");
                sb.AppendLine("},");
            }
            return sb.ToString();
        }

        private static string F(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "f";
        }
    }
}
