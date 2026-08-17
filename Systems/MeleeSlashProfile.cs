using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 근접무기 등급 하나에 대한 참격 이펙트 파라미터.
    /// JsonUtility로 직렬화되므로 public 필드만 사용합니다.
    /// </summary>
    [Serializable]
    public class MeleeSlashProfile
    {
        public string name = "";

        /// <summary>이 프로필이 담당하기 시작하는 무기 등급</summary>
        public int grade = 1;

        /// <summary>이 등급 무기에 참격 이펙트를 손댈지</summary>
        public bool enabled = true;

        // ── 참격 색 (휘두를 때 나오는 호 자체) ───────────────
        /// <summary>
        /// 게임이 그리는 참격 호의 색. 기본이 흰색인 그것입니다.
        ///
        /// 흩뿌림 색과 따로 둡니다. 참격은 알갱이가 <b>한 장</b>이라 "안쪽에서 바깥으로
        /// 번지는" 두 색이 성립하지 않습니다 — 두 색 사이에서 무작위로 하나가 뽑히는데,
        /// 안쪽이 흰빛이면 대부분 흰색이 나와서 "색이 안 바뀐다"가 됩니다.
        /// </summary>
        public Color slashColor = new Color(0.6f, 0.85f, 1f, 1f);

        /// <summary>참격 호의 투명도</summary>
        public float slashAlpha = 0.95f;

        /// <summary>참격 호의 밝기</summary>
        public float slashIntensity = 1.4f;

        // ── 흩뿌림 색 ────────────────────────────────────────
        /// <summary>흩날리는 알갱이의 안쪽 색 (갓 튄 밝은 부분)</summary>
        public Color colorInner = new Color(1f, 1f, 1f, 1f);
        /// <summary>흩날리는 알갱이의 바깥 색 — 등급이 읽히는 곳입니다</summary>
        public Color colorOuter = new Color(0.6f, 0.85f, 1f, 1f);
        public float alpha = 0.9f;
        public float intensity = 1.3f;

        /// <summary>
        /// 휘두를 때 그려지는 호의 모양.
        ///
        /// 게임 참격은 <b>1×1 쿼드</b> 하나이고 호는 그 위의 텍스처에 그려져 있습니다
        /// (실측: 메시 'Quad' 정점 4개). 그래서 모양을 바꾸는 길은 메시가 아니라 텍스처입니다.
        /// 총구 화염과 같은 목록(내장 도형 · 직접 그린 도형 · vfx_textures의 PNG)을 씁니다.
        /// 비어 있으면 게임 기본 텍스처를 그대로 둡니다.
        /// </summary>
        public string slashTexture = "";

        /// <summary>
        /// 흩뿌림이 참격 판의 어느 쪽에 몰릴지(도).
        ///
        /// 호가 판 위에 어떻게 그려져 있는지는 텍스처 나름이라 코드로는 알 수 없습니다.
        /// 알갱이가 호에서 비껴 있으면 이걸 돌려 맞춥니다. 0이면 판의 위쪽입니다.
        /// </summary>
        public float slashFacing;

        // ── 흩뿌림 모양 ──────────────────────────────────────
        /// <summary>내장 도형. 파일을 고르지 않았을 때 씁니다.</summary>
        public MuzzleFlashShape shape = MuzzleFlashShape.Sparkle;

        /// <summary>
        /// assets/vfx_textures 안의 파일 이름(확장자 제외). 비어 있으면 내장 도형을 씁니다.
        /// 값이 있으면 도형보다 우선합니다. 총구 화염과 같은 목록을 공유합니다.
        /// </summary>
        public string textureName = "";

        // ── 휘두를 때 흩날리는 알갱이 ────────────────────────
        /// <summary>0이면 흩뿌림 없음</summary>
        public int sparkCount = 14;
        public float sparkSize = 0.22f;

        /// <summary>
        /// 휘두른 방향으로 날아가는 거리(m).
        /// 속도가 아니라 거리로 둡니다 — 수명을 바꿔도 퍼지는 범위가 흔들리지 않습니다.
        /// </summary>
        public float sparkDistance = 2.2f;

        /// <summary>
        /// 부채꼴 폭(도). 휘두르는 궤적을 따라 옆으로 얼마나 벌어지는지입니다.
        ///
        /// 총구 화염의 원뿔과 달리 <b>가로로 납작한</b> 부채꼴입니다. 참격은 수평으로
        /// 그어지는 동작이라, 원뿔로 뿌리면 위아래로도 똑같이 퍼져서 폭발처럼 보입니다.
        /// </summary>
        public float sparkArc = 70f;

        /// <summary>사라질 때까지 위로 떠오르는 높이(m). 음수면 그만큼 가라앉습니다.</summary>
        public float sparkRise = 0.25f;

        /// <summary>회전 속도(도/초). 0이면 안 돕니다.</summary>
        public float sparkSpin = 90f;

        /// <summary>알갱이가 남아 있는 시간(초)</summary>
        public float sparkDuration = 0.35f;

        /// <summary>
        /// 참격을 따라가며 알갱이를 뿌리는 시간(초).
        ///
        /// 한 번에 다 터뜨리면 참격이 지나간 자리가 아니라 시작점에만 뭉칩니다.
        /// 참격이 호를 그리는 동안 나눠 뿌려야 지나간 자리에 잔상처럼 남습니다.
        /// 게임 참격이 보이는 시간과 비슷하게 잡는 것이 기준입니다.
        /// </summary>
        public float sparkEmitWindow = 0.15f;

        /// <summary>
        /// 알갱이를 얹을 고리의 반지름 배율.
        ///
        /// 기준은 게임 참격 알갱이의 현재 크기(지름)의 절반입니다. 1이면 그 원 위에 딱
        /// 붙고, 낮추면 안쪽으로 당겨집니다. 무기마다 참격 그림이 판 안에서 차지하는 비율이
        /// 달라서(테두리까지 꽉 찬 것도, 가운데만 그린 것도 있습니다) 눈으로 맞출 수
        /// 있어야 합니다 — 이게 어긋나면 다시 "따로 노는" 것으로 보입니다.
        /// </summary>
        public float sparkRing = 0.9f;

        /// <summary>
        /// 흩어지는 정도 0~1.
        ///
        /// 0이면 참격 표면의 법선 방향 그대로 — 판에 수직으로만 뿜어서 판때기처럼 보입니다.
        /// 1이면 완전히 제멋대로입니다. 중간이 칼밥이 튀는 것처럼 보입니다.
        /// </summary>
        public float sparkScatter = 0.6f;

        /// <summary>
        /// 진행 방향으로 늘일지. 칼날 파편에는 어울리지만 하트·별처럼 모양이 있는 것은
        /// 늘어나면 무엇인지 알아볼 수 없게 됩니다.
        /// </summary>
        public bool sparkStretch = true;

        public MeleeSlashProfile Clone()
        {
            var clone = new MeleeSlashProfile();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(MeleeSlashProfile other)
        {
            if (other == null)
                return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(other), this);
        }
    }

    /// <summary>
    /// 등급별 근접 참격 프로필 묶음.
    ///
    /// 기준은 <b>들고 있는 근접무기의 등급</b>입니다 — 무기 오라와 같은 축이라,
    /// 무기를 감싼 색과 휘두를 때 나는 색이 한 벌로 읽힙니다.
    /// 탄환 잔상·총구 화염과 마찬가지로 고정 7단계입니다.
    /// </summary>
    public static class MeleeSlashProfiles
    {
        public const string TuningFileName = "weapon_aura_melee_slash.json";

        /// <summary>
        /// 저장 파일 형식 번호. 낮은 파일은 무시하고 기본값으로 시작합니다.
        ///
        /// v1은 참격이 생긴 지점에서 원뿔로 한 번 터뜨리는 방식이라 값이 그 전제에 맞춰져
        /// 있었습니다(멀리 · 넓게). 지금은 참격 궤적 위에서 나눠 뿌리므로 같은 값이면
        /// 알갱이가 호를 벗어나 날아갑니다.
        /// </summary>
        private const int CurrentVersion = 5;

        [Serializable]
        private class ProfileSetData
        {
            public int version;
            public MeleeSlashProfile[] grades = Array.Empty<MeleeSlashProfile>();
        }

        private static MeleeSlashProfile[]? _runtime;

        public static MeleeSlashProfile[] Runtime
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
        /// 기본 7등급. 바깥 색은 무기 오라·탄환 잔상과 같은 계열을 씁니다.
        ///
        /// 안쪽은 흰빛을 남겨 둡니다. 게임 참격이 원래 흰색 판이라, 안쪽까지 등급 색으로
        /// 칠하면 칼이 지나간 자리가 아니라 색종이가 날아간 것처럼 보입니다.
        /// </summary>
        public static MeleeSlashProfile[] CreateDefaults()
        {
            return new[]
            {
                Grade("Worn", 1, 0, new Color(0.72f, 0.85f, 0.95f), alpha: 0.7f, intensity: 1.05f),
                Grade("Common", 2, 1, new Color(0.4f, 1f, 0.5f), alpha: 0.75f, intensity: 1.15f),
                Grade("Fine", 3, 2, new Color(0.3f, 0.7f, 1f), alpha: 0.8f, intensity: 1.25f),
                Grade("Rare", 4, 3, new Color(0.7f, 0.4f, 1f), alpha: 0.84f, intensity: 1.35f),
                Grade("Epic", 5, 4, new Color(1f, 0.85f, 0.35f), alpha: 0.88f, intensity: 1.45f),
                Grade("Legendary", 6, 5, new Color(1f, 0.6f, 0.2f), alpha: 0.92f, intensity: 1.55f),
                Grade("Mythic", 7, 6, new Color(1f, 0.3f, 0.3f), alpha: 0.95f, intensity: 1.65f),
            };
        }

        /// <param name="step">시각 강도 단계 0~6. 등급 값과 분리해야 특수 등급이 들어와도
        /// 참격이 터무니없이 커지지 않습니다.</param>
        private static MeleeSlashProfile Grade(string name, int grade, int step,
            Color outer, float alpha, float intensity)
        {
            float t = step / 6f;

            return new MeleeSlashProfile
            {
                name = name,
                grade = grade,
                enabled = true,

                // 참격 호는 등급 색을 그대로 씁니다. 흰빛을 섞으면 원래의 흰 참격과
                // 구분이 안 가서 "안 바뀌었다"로 보입니다.
                slashColor = outer,
                slashAlpha = Mathf.Min(1f, alpha + 0.05f),
                slashIntensity = intensity,

                // 흩뿌림은 갓 튄 쪽이 밝고 바깥이 등급 색입니다.
                colorInner = Color.Lerp(outer, Color.white, 0.75f),
                colorOuter = outer,
                alpha = alpha,
                intensity = intensity,

                shape = MuzzleFlashShape.Sparkle,
                sparkCount = Mathf.RoundToInt(Mathf.Lerp(10f, 34f, t)),
                sparkSize = Mathf.Lerp(0.1f, 0.28f, t),

                // 알갱이가 이미 참격 호 위에서 생기므로 멀리 날릴 이유가 없습니다.
                // 크게 잡으면 호를 벗어나 흩어져서, 다시 참격과 따로 노는 것처럼 보입니다.
                sparkDistance = Mathf.Lerp(0.35f, 0.9f, t),
                sparkArc = 70f,
                sparkRise = 0.15f,
                sparkSpin = Mathf.Lerp(60f, 180f, t),
                sparkDuration = Mathf.Lerp(0.25f, 0.45f, t),
                sparkEmitWindow = 0.15f,
                sparkRing = 0.9f,
                sparkScatter = 0.6f,
                sparkStretch = true,
            };
        }

        /// <summary>
        /// 시드 기반 무작위 프로필. 모양을 먼저 고르고 나머지를 거기에 맞춥니다 —
        /// 늘어나는 하트가 땅으로 처박히는 조합이 나오지 않게 하기 위해서입니다.
        /// </summary>
        public static MeleeSlashProfile CreateRandom(int seed, string name, int grade)
        {
            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Chance(float p) => rng.NextDouble() < p;

            float hue = (float)rng.NextDouble();
            var outer = Color.HSVToRGB(hue, Range(0.45f, 0.95f), 1f);

            // 내장 도형과 사용자 PNG를 한 줄로 이어 붙여 고릅니다.
            var builtIn = MuzzleFlashShapes.All;
            var files = WeaponAuraResources.GetTextureNames();

            var shape = MuzzleFlashShape.Sparkle;
            string textureName = "";

            int fileCount = files != null ? files.Length : 0;
            int total = builtIn.Length + fileCount;
            if (total > 0)
            {
                int choice = rng.Next(total);
                if (choice < builtIn.Length)
                    shape = builtIn[choice];
                else
                    textureName = files![choice - builtIn.Length] ?? "";
            }

            // 빛무리·반짝임은 잘게 많이 뿌려야 하고, 하트·별은 크고 적게 띄워야 읽힙니다.
            bool shaped = !string.IsNullOrEmpty(textureName)
                          || (shape != MuzzleFlashShape.Glow && shape != MuzzleFlashShape.Sparkle);

            return new MeleeSlashProfile
            {
                name = name,
                grade = grade,

                slashColor = outer,
                slashAlpha = Range(0.75f, 1f),
                slashIntensity = Range(1f, 1.8f),

                colorInner = Color.Lerp(outer, Color.white, Range(0.45f, 0.9f)),
                colorOuter = outer,
                alpha = Range(0.65f, 0.98f),
                intensity = Range(0.9f, 1.7f),

                shape = shape,
                textureName = textureName,

                sparkCount = shaped ? rng.Next(5, 16) : rng.Next(12, 40),
                sparkSize = shaped ? Range(0.3f, 0.9f) : Range(0.06f, 0.3f),

                // 참격 호 위에서 생기므로 거리는 "호에서 얼마나 벗어나는지"입니다.
                // 크게 굴리면 그 순간 참격과 무관한 폭발이 됩니다.
                sparkDistance = Range(0.2f, 1.2f),
                sparkArc = Range(30f, 150f),

                // 도형은 떠오르는 쪽이, 파편은 흩어져 떨어지는 쪽이 그럴듯합니다.
                sparkRise = shaped ? Range(0.2f, 1f) : Range(-0.5f, 0.3f),
                sparkSpin = shaped ? Range(-300f, 300f) : (Chance(0.4f) ? Range(-360f, 360f) : 0f),
                sparkDuration = shaped ? Range(0.35f, 0.8f) : Range(0.2f, 0.5f),
                sparkEmitWindow = Range(0.08f, 0.3f),
                sparkRing = Range(0.7f, 1.05f),
                sparkScatter = shaped ? Range(0.4f, 1f) : Range(0.3f, 0.9f),

                // 늘어나면 하트가 하트로 안 보입니다.
                sparkStretch = !shaped,
            };
        }

        /// <summary>한 번 눌러 통째로 갈아 끼우는 프리셋 종류</summary>
        public enum PresetKind
        {
            /// <summary>칼날 파편이 궤적을 따라 흩어집니다 (기본)</summary>
            Slash = 0,

            /// <summary>불티가 넓게 흩날리며 떨어집니다</summary>
            Ember = 1,

            /// <summary>꽃잎처럼 하트가 천천히 떠오릅니다</summary>
            Petal = 2,
        }

        /// <summary>
        /// 프리셋을 씌웁니다. 등급 값·이름·켜짐 여부는 부르는 쪽에서 유지합니다.
        ///
        /// "칼을 휘두르면 하트가 흩날리는" 식의 연출은 값 대여섯 개를 동시에 맞춰야 나옵니다
        /// (도형 · 크기 · 개수 · 중력 뒤집기 · 늘이기 끄기 · 회전). 버튼 하나로 둡니다.
        /// </summary>
        public static MeleeSlashProfile CreatePreset(PresetKind kind, string name, int grade)
        {
            var profile = new MeleeSlashProfile { name = name, grade = grade };

            switch (kind)
            {
                case PresetKind.Ember:
                    profile.shape = MuzzleFlashShape.Glow;
                    profile.slashColor = new Color(1f, 0.45f, 0.12f);
                    profile.slashAlpha = 0.95f;
                    profile.slashIntensity = 1.7f;
                    profile.colorInner = new Color(1f, 0.93f, 0.7f);
                    profile.colorOuter = new Color(1f, 0.45f, 0.12f);
                    profile.alpha = 0.95f;
                    profile.intensity = 1.7f;

                    profile.sparkCount = 40;
                    profile.sparkSize = 0.12f;
                    profile.sparkDistance = 0.8f;
                    profile.sparkArc = 120f;
                    profile.sparkRise = -0.4f;      // 불티는 흩어지며 떨어집니다
                    profile.sparkSpin = 0f;
                    profile.sparkDuration = 0.55f;
                    profile.sparkEmitWindow = 0.2f;
                    profile.sparkRing = 0.95f;
                    profile.sparkScatter = 0.85f;
                    profile.sparkStretch = true;
                    break;

                case PresetKind.Petal:
                    profile.shape = MuzzleFlashShape.Heart;
                    profile.slashColor = new Color(1f, 0.3f, 0.55f);
                    profile.slashAlpha = 0.95f;
                    profile.slashIntensity = 1.3f;
                    profile.colorInner = new Color(1f, 0.8f, 0.88f);
                    profile.colorOuter = new Color(1f, 0.3f, 0.55f);
                    profile.alpha = 0.95f;
                    profile.intensity = 1.2f;

                    profile.sparkCount = 10;
                    profile.sparkSize = 0.6f;
                    profile.sparkDistance = 0.4f;
                    profile.sparkArc = 90f;
                    profile.sparkRise = 0.8f;       // 떠오르며 사라집니다
                    profile.sparkSpin = 140f;
                    profile.sparkDuration = 0.9f;
                    profile.sparkEmitWindow = 0.25f;
                    profile.sparkRing = 0.85f;
                    profile.sparkScatter = 0.5f;
                    profile.sparkStretch = false;   // 늘어나면 하트로 안 보입니다
                    break;

                case PresetKind.Slash:
                default:
                    var preset = Grade(name, grade, 4, new Color(0.55f, 0.85f, 1f),
                        alpha: 0.9f, intensity: 1.45f);
                    profile.CopyFrom(preset);
                    profile.name = name;
                    profile.grade = grade;
                    break;
            }

            return profile;
        }

        /// <summary>무기 등급에 해당하는 프로필. 가장 낮은 등급보다 낮아도 첫 프로필을 씁니다.</summary>
        public static MeleeSlashProfile? Resolve(int quality)
        {
            var grades = Runtime;
            if (grades.Length == 0)
                return null;

            MeleeSlashProfile? result = null;
            foreach (var profile in grades)
            {
                if (profile != null && quality >= profile.grade)
                    result = profile;
            }

            return result ?? grades[0];
        }

        public static MeleeSlashProfile? Get(int index)
        {
            var grades = Runtime;
            if (index < 0 || index >= grades.Length)
                return null;
            return grades[index];
        }

        /// <summary>무기 등급이 몇 번째 프로필에 걸리는지 (설정 창의 "따라가기"용)</summary>
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
                var data = new ProfileSetData { version = CurrentVersion, grades = Runtime };
                File.WriteAllText(path, JsonUtility.ToJson(data, true), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 근접 참격 저장 실패: {ex.Message}");
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

                if (data.version < CurrentVersion)
                {
                    UnityEngine.Debug.Log(
                        $"[WeaponAura] 근접 참격 설정이 예전 형식(v{data.version})이라 기본값으로 시작합니다.");
                    return false;
                }

                _runtime = data.grades;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 근접 참격 불러오기 실패: {ex.Message}");
                return false;
            }
        }

        public static void AutoLoad()
        {
            if (Load(out string path))
                UnityEngine.Debug.Log($"[WeaponAura] 저장된 근접 참격 설정을 불러왔습니다: {path}");
        }
    }
}
