using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 탄환 잔상 1등급분의 시각 파라미터.
    /// JsonUtility로 직렬화되므로 public 필드만 사용합니다.
    /// </summary>
    [Serializable]
    public class BulletTrailProfile
    {
        /// <summary>표시 이름 (설정 창·로그용)</summary>
        public string name = "";

        /// <summary>이 프로필이 담당하기 시작하는 탄환 등급</summary>
        public int grade = 1;

        /// <summary>
        /// 이 등급 탄환에 잔상을 그릴지.
        ///
        /// 낮은 등급 탄환까지 전부 빛나면 화면이 지저분해지므로 등급 단위로 끕니다.
        /// (모드 전체 스위치는 <see cref="Settings.BulletTrailSettings.Enabled"/>입니다)
        /// </summary>
        public bool enabled = true;

        // ── 색 ───────────────────────────────────────────────
        /// <summary>총알 쪽(머리) 색</summary>
        public Color colorStart = new Color(0.45f, 0.8f, 1f, 1f);
        /// <summary>사라지는 쪽(꼬리) 색</summary>
        public Color colorEnd = new Color(0.25f, 0.4f, 1f, 1f);
        /// <summary>전체 불투명도 배율</summary>
        public float alpha = 0.85f;
        /// <summary>색 밝기 배율 (가산 합성이라 1을 넘기면 더 발광함)</summary>
        public float intensity = 1.6f;

        // ── 모양 ─────────────────────────────────────────────
        /// <summary>잔상이 남아 있는 시간(초). 길수록 꼬리가 깁니다.</summary>
        public float length = 0.2f;
        /// <summary>총알 쪽 굵기(m)</summary>
        public float startWidth = 0.05f;
        /// <summary>꼬리 쪽 굵기(m)</summary>
        public float endWidth = 0.005f;
        /// <summary>true면 가산 합성으로 발광합니다. false면 일반 알파 합성.</summary>
        public bool additive = true;

        // ── 잔상 방식 ────────────────────────────────────────

        /// <summary>
        /// 꼬리를 선으로 그릴지, 지나간 자리에 도형을 뿌릴지.
        /// 기본은 선 — 이미 쓰던 사람의 화면이 업데이트만으로 바뀌면 안 됩니다.
        /// </summary>
        public BulletTrailStyle style = BulletTrailStyle.Line;

        /// <summary>자국을 1m당 몇 개 남길지. 총알 속도와 무관하게 간격이 일정해집니다.</summary>
        public float stampRate = 6f;

        /// <summary>자국 하나의 크기(m)</summary>
        public float stampSize = 0.14f;

        /// <summary>자국이 사라지기까지의 시간(초)</summary>
        public float stampLife = 0.45f;

        /// <summary>자국 모양 (<see cref="stampTextureName"/>이 비어 있을 때 씁니다)</summary>
        public BulletHeadShape stampShape = BulletHeadShape.Diamond;

        /// <summary>직접 그린 도형 또는 PNG 파일 이름</summary>
        public string stampTextureName = "";

        // ── 총알 머리 ────────────────────────────────────────
        //
        // 게임 원본 총알에는 본체 모델이 없습니다. 0.04초짜리 짧은 궤적 하나가 곧
        // "날아가는 총알"로 보이는 구조입니다(BulletNormal·BulletSMG 실측). 그래서
        // 원본 궤적을 숨기면 총알 자체가 사라지고 꼬리만 남습니다.
        //
        // 여기 값들은 그 머리를 모드가 대신 그릴 때 씁니다. 원본과 같은 방식 —
        // 아주 짧고 굵은 궤적 — 이라 카메라를 향해 저절로 눕고, 총알을 정확히 따라갑니다.
        // 원본 궤적을 숨기지 않는 동안에는 그리지 않습니다(겹쳐서 두 겹이 됩니다).

        // 기본값은 게임 원본 총알 실측치에 맞춰 뒀습니다 — BulletNormal의 궤적이
        // 굵기 0.19m · 지속 0.04초입니다. 처음에 잔상 굵기(0.03~0.075)를 기준으로 잡았다가
        // 머리가 원본의 3분의 1도 안 되게 가늘어서 거의 안 보였습니다.

        /// <summary>머리 굵기(m). 0이면 머리를 그리지 않습니다.</summary>
        public float headWidth = 0.19f;
        /// <summary>
        /// 머리 가로세로비 (굵기 대비 길이). 1이면 정사각, 3이면 원본 대시 느낌.
        ///
        /// 예전에는 길이를 초로 잡았는데, 그러면 실제 길이가 <c>시간 × 총알 속도</c>라
        /// 굵기의 열 배가 넘는 바늘이 되어 어떤 도형을 넣어도 늘어나 버렸습니다.
        /// 비율로 잡으면 총알 속도와 무관하게 도형이 제 모양을 유지합니다.
        /// </summary>
        public float headAspect = 2.5f;
        /// <summary>머리 밝기 배율. 꼬리보다 밝아야 총알처럼 읽힙니다.</summary>
        public float headIntensity = 1.35f;

        /// <summary>머리 모양 (<see cref="headTextureName"/>이 비어 있을 때 씁니다)</summary>
        public BulletHeadShape headShape = BulletHeadShape.Capsule;

        /// <summary>
        /// 직접 그린 도형 이름 또는 <c>assets/vfx_textures/</c>의 PNG 파일 이름.
        /// 비어 있으면 <see cref="headShape"/>의 내장 도형을 씁니다.
        /// </summary>
        public string headTextureName = "";

        /// <summary>
        /// true면 머리 색을 꼬리 머리쪽 색(<see cref="colorStart"/>)에서 가져옵니다.
        ///
        /// 기본이 true인 이유 — 대부분은 총알과 꼬리가 같은 색이길 원하고, 그때
        /// 색을 두 군데서 따로 맞추는 건 번거롭기만 합니다. 따로 쓰고 싶을 때만 끕니다.
        /// </summary>
        public bool headFollowTrailColor = true;

        /// <summary><see cref="headFollowTrailColor"/>가 false일 때 쓰는 머리 색</summary>
        public Color headColor = new Color(1f, 1f, 1f, 1f);

        /// <summary>지금 설정에 따라 실제로 쓸 머리 색.</summary>
        public Color ResolveHeadColor() => headFollowTrailColor ? colorStart : headColor;

        // ── 총알 발광체 ──────────────────────────────────────
        //
        // 총알을 따라다니는 빛(SodaPointLight)입니다. 기본값은 전부 "원본 그대로"라
        // 옵션을 켜도 슬라이더를 움직이기 전까지는 화면이 달라지지 않습니다.
        // 원본 색·크기는 총알 프리팹마다 달라서 절대값 대신 배율로 다룹니다.

        /// <summary>발광체를 보일지. 끄면 총알 주변 빛이 사라집니다.</summary>
        public bool glowVisible = true;

        /// <summary>크기 배율 (1 = 원본)</summary>
        public float glowScale = 1f;

        /// <summary>밝기 배율 (1 = 원본)</summary>
        public float glowIntensity = 1f;

        /// <summary>색을 어디서 가져올지</summary>
        public BulletGlowColorMode glowColorMode = BulletGlowColorMode.Vanilla;

        /// <summary><see cref="BulletGlowColorMode.Custom"/>일 때 쓰는 색</summary>
        public Color glowColor = new Color(1f, 1f, 1f, 1f);

        public BulletTrailProfile Clone()
        {
            var clone = new BulletTrailProfile();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(BulletTrailProfile other)
        {
            if (other == null)
                return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(other), this);
        }
    }

    /// <summary>
    /// 잔상 색 계산. 런타임(TrailRenderer)과 설정 창 미리보기가 <b>같은 함수</b>를 써야
    /// 미리보기에서 고른 색이 실제 총알에도 그대로 나옵니다.
    /// </summary>
    public static class BulletTrailShading
    {
        /// <summary>
        /// 밝기 배율을 색과 알파로 나눠 담습니다.
        ///
        /// 밝기를 색에 그대로 곱하면 채널이 차례로 1에 붙습니다 — 주황(1, 0.55, 0.15)에
        /// 1.8을 곱하면 (1, 0.99, 0.27)이 되어 노랑을 지나 흰색으로 뭉갭니다.
        /// "길이가 길어질 때 중간이 그냥 흰색"으로 보이던 게 이것입니다.
        ///
        /// 그래서 가장 밝은 채널이 1에 닿는 지점까지만 곱해 색조·채도를 지키고,
        /// 더 밝히지 못한 몫은 알파로 넘깁니다. 가산 합성에서 화면에 더해지는 양은
        /// 대략 rgb × alpha라서, 알파를 올리는 것이 색을 망가뜨리지 않고 밝기를 올리는 길입니다.
        /// </summary>
        public static void Resolve(Color color, float intensity, float baseAlpha,
            out Color rgb, out float alpha)
        {
            intensity = Mathf.Max(0f, intensity);
            baseAlpha = Mathf.Max(0f, baseAlpha);

            float peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (peak <= 0.0001f)
            {
                rgb = new Color(0f, 0f, 0f, 1f);
                alpha = Mathf.Clamp01(baseAlpha);
                return;
            }

            // 색조를 지키려면 세 채널에 같은 배율을 곱해야 합니다. 그 배율의 상한이 1/peak입니다.
            float scale = Mathf.Min(intensity, 1f / peak);
            rgb = new Color(color.r * scale, color.g * scale, color.b * scale, 1f);

            float overflow = scale > 0.0001f ? intensity / scale : 1f;
            alpha = Mathf.Clamp01(baseAlpha * overflow);
        }
    }

    /// <summary>
    /// 등급별 탄환 잔상 프로필 묶음.
    ///
    /// 무기 오라 티어와 달리 등급을 추가·삭제하지 않습니다. 탄환 등급은 게임이 1~7로만
    /// 굴리기 때문에 고정 7단계로 충분하고, 사용자가 손댈 수 있는 값이 적을수록
    /// 어떤 총알이 어떤 색으로 나가는지 예측하기 쉽습니다.
    /// </summary>
    public static class BulletTrailProfiles
    {
        public const string TuningFileName = "weapon_aura_bullet_trail.json";

        [Serializable]
        private class ProfileSetData
        {
            public BulletTrailProfile[] grades = Array.Empty<BulletTrailProfile>();
        }

        private static BulletTrailProfile[]? _runtime;

        public static BulletTrailProfile[] Runtime
        {
            get
            {
                if (_runtime == null || _runtime.Length == 0)
                    _runtime = CreateDefaults();
                return _runtime;
            }
        }

        public static int Count => Runtime.Length;

        /// <summary>
        /// 기본 7등급. 무기 오라의 등급 색과 같은 계열을 씁니다 —
        /// 같은 등급이면 무기와 총알이 같은 색으로 보여야 등급이 읽힙니다.
        /// 위로 갈수록 꼬리가 길어지고 굵어집니다.
        /// </summary>
        public static BulletTrailProfile[] CreateDefaults()
        {
            return new[]
            {
                // 1등급도 켜 둡니다. 초반 탄약이 대부분 여기라, 꺼 두면 설치하고 처음 쏴 봤을 때
                // "아무 것도 안 나온다"가 됩니다. 대신 아주 짧고 흐리게 둡니다.
                Grade("Worn", 1, 0,
                    new Color(0.72f, 0.85f, 0.95f), new Color(0.55f, 0.68f, 0.8f),
                    alpha: 0.45f, intensity: 1.1f, enabled: true),

                Grade("Common", 2, 1,
                    new Color(0.4f, 1f, 0.5f), new Color(0.15f, 0.6f, 0.3f),
                    alpha: 0.6f, intensity: 1.25f, enabled: true),

                Grade("Fine", 3, 2,
                    new Color(0.3f, 0.7f, 1f), new Color(0.15f, 0.35f, 0.9f),
                    alpha: 0.7f, intensity: 1.4f, enabled: true),

                Grade("Rare", 4, 3,
                    new Color(0.7f, 0.4f, 1f), new Color(0.4f, 0.15f, 0.85f),
                    alpha: 0.78f, intensity: 1.55f, enabled: true),

                Grade("Epic", 5, 4,
                    new Color(1f, 0.85f, 0.35f), new Color(1f, 0.5f, 0.1f),
                    alpha: 0.85f, intensity: 1.7f, enabled: true),

                Grade("Legendary", 6, 5,
                    new Color(1f, 0.6f, 0.2f), new Color(1f, 0.22f, 0.05f),
                    alpha: 0.9f, intensity: 1.85f, enabled: true),

                Grade("Mythic", 7, 6,
                    new Color(1f, 0.3f, 0.3f), new Color(0.8f, 0.05f, 0.3f),
                    alpha: 0.95f, intensity: 2f, enabled: true),
            };
        }

        /// <param name="step">시각 강도 단계 0~6 — 길이·굵기 파생값 계산에만 씁니다.
        /// 등급 값과 분리해야 특수 등급이 들어와도 꼬리가 터무니없이 길어지지 않습니다.</param>
        private static BulletTrailProfile Grade(string name, int grade, int step,
            Color start, Color end, float alpha, float intensity, bool enabled)
        {
            float t = step / 6f;

            return new BulletTrailProfile
            {
                name = name,
                grade = grade,
                enabled = enabled,

                colorStart = start,
                colorEnd = end,
                alpha = alpha,
                intensity = intensity,

                length = Mathf.Lerp(0.1f, 0.3f, t),
                startWidth = Mathf.Lerp(0.03f, 0.075f, t),
                endWidth = Mathf.Lerp(0.002f, 0.012f, t),
                additive = true,

                // 원본 총알(0.19m · 0.04초) 언저리에서 등급에 따라 조금씩 키웁니다.
                headWidth = Mathf.Lerp(0.16f, 0.24f, t),
                headAspect = Mathf.Lerp(2.2f, 3.2f, t),
                headIntensity = Mathf.Lerp(1.2f, 1.5f, t),
                headShape = BulletHeadShape.Capsule,
                headFollowTrailColor = true,
            };
        }

        /// <summary>
        /// 시드 기반 무작위 프로필. 같은 시드는 항상 같은 결과라서
        /// 마음에 드는 조합이 나오면 시드만 적어 두면 됩니다.
        ///
        /// 값을 하나씩 따로 굴리지 않습니다. 그러면 "굵고 짧은데 꼬리만 길다" 같은
        /// 어색한 조합이 절반쯤 나옵니다. 색은 색상환에서 조화로운 짝을 뽑고,
        /// 모양은 성격(예광탄 / 보통 / 실선)을 먼저 고른 뒤 거기에 맞춥니다.
        /// </summary>
        public static BulletTrailProfile CreateRandom(int seed, string name, int grade)
        {
            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Chance(float p) => rng.NextDouble() < p;

            float hue = (float)rng.NextDouble();

            // 유사색(차분) / 보색(대비) / 삼각(화려) 중 하나
            float offset = Chance(0.45f) ? Range(0.04f, 0.12f)
                         : Chance(0.6f) ? Range(0.45f, 0.55f)
                         : Range(0.28f, 0.36f);

            // 머리는 밝게, 꼬리는 짙게 두면 날아가는 방향이 읽힙니다.
            var colorStart = Color.HSVToRGB(hue, Range(0.3f, 0.8f), 1f);
            var colorEnd = Color.HSVToRGB(Mathf.Repeat(hue + offset, 1f),
                Range(0.6f, 1f), Range(0.7f, 1f));

            float length;
            float startWidth;

            switch (rng.Next(3))
            {
                case 0:     // 짧고 굵은 예광탄
                    length = Range(0.08f, 0.16f);
                    startWidth = Range(0.055f, 0.09f);
                    break;

                case 1:     // 보통
                    length = Range(0.16f, 0.3f);
                    startWidth = Range(0.035f, 0.06f);
                    break;

                default:    // 길고 가는 실선
                    length = Range(0.3f, 0.55f);
                    startWidth = Range(0.018f, 0.04f);
                    break;
            }

            return new BulletTrailProfile
            {
                name = name,
                grade = grade,

                colorStart = colorStart,
                colorEnd = colorEnd,
                alpha = Range(0.55f, 0.95f),
                intensity = Range(0.9f, 1.8f),

                length = length,
                startWidth = startWidth,

                // 꼬리는 항상 머리보다 가늘게. 반대로 나오면 총알이 뒤로 나는 것처럼 보입니다.
                endWidth = startWidth * Range(0.05f, 0.35f),

                additive = Chance(0.8f),

                // 머리도 꼬리 굵기에서 파생시킵니다. 따로 굴리면 가는 실선 끝에
                // 커다란 구슬이 달린 조합이 나옵니다. 다만 원본 총알(0.19m)보다
                // 너무 가늘면 안 보이므로 아래를 막아 둡니다.
                headWidth = Mathf.Max(0.12f, startWidth * Range(2.2f, 3.5f)),
                headAspect = Range(1.4f, 3.6f),
                headIntensity = Range(1.15f, 1.6f),
                headShape = BulletHeadShapes.All[rng.Next(BulletHeadShapes.All.Length)],

                // 색까지 따로 굴리면 머리와 꼬리가 따로 노는 조합이 절반쯤 나옵니다.
                headFollowTrailColor = true,
            };
        }

        /// <summary>
        /// 탄환 등급에 해당하는 프로필.
        ///
        /// 가장 낮은 등급의 기준값보다 낮아도 첫 프로필을 씁니다 — 등급 0짜리 탄약만
        /// 잔상이 통째로 사라지면 "왜 이 총만 안 되지"가 됩니다.
        /// </summary>
        public static BulletTrailProfile? Resolve(int quality)
        {
            var grades = Runtime;
            if (grades.Length == 0)
                return null;

            BulletTrailProfile? result = null;
            foreach (var profile in grades)
            {
                if (profile != null && quality >= profile.grade)
                    result = profile;
            }

            return result ?? grades[0];
        }

        /// <summary>인덱스로 조회 (범위를 벗어나면 null)</summary>
        public static BulletTrailProfile? Get(int index)
        {
            var grades = Runtime;
            if (index < 0 || index >= grades.Length)
                return null;
            return grades[index];
        }

        /// <summary>탄환 등급이 몇 번째 프로필에 걸리는지 (설정 창의 "따라가기"용)</summary>
        public static int IndexOfQuality(int quality)
        {
            var grades = Runtime;
            int result = -1;
            for (int i = 0; i < grades.Length; i++)
            {
                if (grades[i] != null && quality >= grades[i].grade)
                    result = i;
            }
            return result >= 0 ? result : 0;
        }

        public static void ResetToDefaults()
        {
            _runtime = CreateDefaults();
        }

        public static string GetTuningPath()
        {
            return Path.Combine(WeaponAuraProfiles.GetSaveFolder(), TuningFileName);
        }

        public static bool Save(out string path)
        {
            path = GetTuningPath();
            try
            {
                var data = new ProfileSetData { grades = Runtime };
                File.WriteAllText(path, JsonUtility.ToJson(data, true), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 탄환 잔상 저장 실패: {ex.Message}");
                return false;
            }
        }

        public static bool Load(out string path)
        {
            path = GetTuningPath();
            try
            {
                if (!File.Exists(path))
                    return false;

                var data = JsonUtility.FromJson<ProfileSetData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null || data.grades == null || data.grades.Length == 0)
                    return false;

                _runtime = data.grades;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 탄환 잔상 불러오기 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>모드 시작 시 저장 파일을 자동으로 불러옵니다.</summary>
        public static void AutoLoad()
        {
            if (Load(out string path))
                UnityEngine.Debug.Log($"[WeaponAura] 저장된 탄환 잔상 설정을 불러왔습니다: {path}");
        }
    }
}
