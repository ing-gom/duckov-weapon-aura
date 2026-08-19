using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 탄약 등급 하나에 대한 총구 화염 파라미터.
    /// JsonUtility로 직렬화되므로 public 필드만 사용합니다.
    /// </summary>
    [Serializable]
    public class MuzzleFlashProfile : ISerializationCallbackReceiver, ILayerHost
    {
        public string name = "";

        /// <summary>이 프로필이 담당하기 시작하는 탄환 등급</summary>
        public int grade = 1;

        /// <summary>이 등급 탄약에 총구 화염을 그릴지</summary>
        public bool enabled = true;

        // ── 색 ───────────────────────────────────────────────
        /// <summary>중심 색 (가장 밝은 부분)</summary>
        public Color colorInner = new Color(1f, 0.95f, 0.85f, 1f);
        /// <summary>바깥 색 — 등급이 읽히는 곳입니다</summary>
        public Color colorOuter = new Color(1f, 0.6f, 0.2f, 1f);
        public float alpha = 0.9f;
        public float intensity = 1.4f;

        // ── 모양 ─────────────────────────────────────────────
        /// <summary>화염 지름(m)</summary>
        public float size = 0.16f;
        /// <summary>화염이 남아 있는 시간(초)</summary>
        public float duration = 0.06f;

        /// <summary>
        /// 게임 기본 화염을 그대로 쓰는 모드에서의 크기 배율.
        /// 형태는 게임 것을 쓰되 등급이 높을수록 크게 터지게 할 수 있습니다.
        /// </summary>
        public float sizeScale = 1f;

        // ── 모양 ─────────────────────────────────────────────
        /// <summary>내장 도형. 파일을 고르지 않았을 때 씁니다.</summary>
        public MuzzleFlashShape shape = MuzzleFlashShape.Glow;

        /// <summary>
        /// assets/vfx_textures 안의 파일 이름(확장자 제외). 비어 있으면 내장 도형을 씁니다.
        /// 값이 있으면 도형보다 우선합니다.
        /// </summary>
        public string textureName = "";

        // ── 튀어나가는 알갱이 ────────────────────────────────
        /// <summary>0이면 알갱이 없음</summary>
        public int sparkCount = 6;
        public float sparkSize = 0.018f;

        /// <summary>
        /// 총구에서 앞으로 날아가는 거리(m).
        ///
        /// 속도(m/s)가 아니라 거리로 둡니다. 속도로 두면 수명이 바뀔 때마다 실제 도달 거리가
        /// 같이 변해서, "얼마나 퍼지는지"를 슬라이더 하나로 잡을 수가 없습니다.
        /// 속도는 이 값과 수명에서 역산합니다.
        /// </summary>
        public float sparkDistance = 1.2f;

        /// <summary>퍼지는 각도(도). 크면 사방으로 흩어집니다.</summary>
        public float sparkSpread = 16f;

        /// <summary>
        /// 사라질 때까지 위로 떠오르는 높이(m). 음수면 그만큼 가라앉습니다.
        ///
        /// 중력 배율을 직접 노출했더니 이동량이 수명의 제곱에 비례해서, 같은 배율이라도
        /// 수명이 조금만 길어지면 6m씩 솟구치거나 바닥을 뚫고 내려갔습니다.
        /// 도달 높이를 정해 두고 중력을 역산하면 수명과 무관하게 항상 이 범위 안에 있습니다.
        /// </summary>
        public float sparkRise = -0.2f;

        /// <summary>회전 속도(도/초). 0이면 안 돕니다.</summary>
        public float sparkSpin;

        /// <summary>
        /// 진행 방향으로 늘일지. 불티에는 어울리지만 하트·별처럼 모양이 있는 것은
        /// 늘어나면 무엇인지 알아볼 수 없게 됩니다.
        /// </summary>
        public bool sparkStretch = true;

        // ── 레이어 ──────────────────────────────────────────────
        //
        // 본체 이펙트 위에 얹는 알갱이입니다. 본체는 한 벌이라 "여기에 불티도 조금"
        // 같은 조합을 만들 수 없어서, 자리·색·모양이 제각각인 레이어를 더 쌓습니다.
        //
        // 배열은 직렬화에서 빠집니다(Unity가 중첩된 사용자 정의 클래스를 담지 못합니다).
        // 저장 직전에 문자열로 옮겨 담고 읽은 직후에 되돌립니다 — 오라와 같은 방식입니다.

        [NonSerialized]
        public WeaponEffectLayer[] layers = Array.Empty<WeaponEffectLayer>();

        /// <summary>파일에 실제로 담기는 형태.</summary>
        public string[] layersJson = Array.Empty<string>();

        public void OnBeforeSerialize()
        {
            layersJson = ProfileJson.Pack(layers);
        }

        public void OnAfterDeserialize()
        {
            layers = ProfileJson.Unpack<WeaponEffectLayer>(layersJson);
        }

        public WeaponEffectLayer[] Layers => layers;
        public int LayerLimit => LayerList.Max;
        public Color LayerSeedColor => colorOuter;

        public WeaponEffectLayer? AddLayer() => LayerList.Add(ref layers, colorOuter);
        public bool RemoveLayer(int index) => LayerList.Remove(ref layers, index);
        public WeaponEffectLayer? GetLayer(int index) => LayerList.Get(layers, index);

        public MuzzleFlashProfile Clone()
        {
            var clone = new MuzzleFlashProfile();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(MuzzleFlashProfile other)
        {
            if (other == null)
                return;

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(other), this);

            // 레이어는 직렬화에서 빠져 있으므로 따로 옮깁니다.
            layers = LayerList.Clone(other.layers);
        }
    }

    /// <summary>
    /// 등급별 총구 화염 프로필 묶음.
    ///
    /// 기준은 <b>장전한 탄약 등급</b>입니다 — 총구에서 터지는 색과 날아가는 잔상 색이
    /// 같은 축을 쓰면 한 흐름으로 읽힙니다. 탄환 잔상과 마찬가지로 고정 7단계입니다.
    /// </summary>
    public static class MuzzleFlashProfiles
    {
        public const string TuningFileName = "weapon_aura_muzzle_flash.json";

        /// <summary>
        /// 저장 파일 형식 번호.
        ///
        /// 처음 배포한 기본값(크기 0.1~0.22m, 지속 0.045~0.08초)은 게임 기본 화염에 묻혀
        /// 보이지 않았습니다. 그 값이 이미 저장돼 있으면 새 기본값이 적용되지 않으므로,
        /// 번호가 낮은 파일은 무시하고 기본값으로 시작합니다.
        /// 기능이 막 나온 참이라 아직 공들여 맞춰 둔 설정이 있을 수 없습니다.
        /// </summary>
        private const int CurrentVersion = 7;

        [Serializable]
        private class ProfileSetData
        {
            public int version;
            /// <summary>
            /// 예전 형식. Unity가 이 배열을 저장하지 못해 늘 비어 있었습니다 —
            /// 옛 파일을 읽을 때만 쓰고, 쓸 때는 아래 items에 담습니다.
            /// </summary>
            public MuzzleFlashProfile[] grades = Array.Empty<MuzzleFlashProfile>();

            /// <summary>프로필 하나하나를 맨 위 객체로 직렬화한 문자열들.</summary>
            public string[] items = Array.Empty<string>();
        }

        /// <summary>
        /// 저장본에서 프로필을 꺼냅니다. 새 형식(items)이 먼저이고, 없으면 옛 형식을 봅니다.
        ///
        /// 옛 형식은 사실상 늘 비어 있었지만(Unity가 못 담았습니다) 읽는 쪽을 남겨 두는
        /// 비용이 거의 없고, 어딘가에서 담긴 파일이 있다면 그것까지 살릴 수 있습니다.
        /// </summary>
        private static MuzzleFlashProfile[] Read(ProfileSetData? data)
        {
            if (data == null)
                return Array.Empty<MuzzleFlashProfile>();

            var unpacked = ProfileJson.Unpack<MuzzleFlashProfile>(data.items);
            if (unpacked.Length > 0)
                return unpacked;

            return data.grades ?? Array.Empty<MuzzleFlashProfile>();
        }

        private static MuzzleFlashProfile[]? _runtime;

        public static MuzzleFlashProfile[] Runtime
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
        /// 기본 7등급. 바깥 색은 탄환 잔상과 같은 계열을 씁니다.
        ///
        /// 처음에는 시야를 가릴까 봐 아주 작고 짧게 잡았는데, 게임 기본 화염에 완전히 묻혀서
        /// "바뀐 게 없다"로 보였습니다. 보이지 않는 커스터마이즈는 없는 것과 같아서
        /// 확실히 읽히는 크기로 올렸습니다. 과하면 등급별로 줄이거나 끄면 됩니다.
        /// </summary>
        public static MuzzleFlashProfile[] CreateDefaults()
        {
            return new[]
            {
                Grade("Worn", 1, 0, new Color(0.72f, 0.85f, 0.95f), alpha: 0.55f, intensity: 1.1f),
                Grade("Common", 2, 1, new Color(0.4f, 1f, 0.5f), alpha: 0.62f, intensity: 1.2f),
                Grade("Fine", 3, 2, new Color(0.3f, 0.7f, 1f), alpha: 0.7f, intensity: 1.3f),
                Grade("Rare", 4, 3, new Color(0.7f, 0.4f, 1f), alpha: 0.76f, intensity: 1.4f),
                Grade("Epic", 5, 4, new Color(1f, 0.85f, 0.35f), alpha: 0.82f, intensity: 1.5f),
                Grade("Legendary", 6, 5, new Color(1f, 0.6f, 0.2f), alpha: 0.88f, intensity: 1.6f),
                Grade("Mythic", 7, 6, new Color(1f, 0.3f, 0.3f), alpha: 0.92f, intensity: 1.7f),
            };
        }

        /// <param name="step">시각 강도 단계 0~6. 등급 값과 분리해야 특수 등급이 들어와도
        /// 화염이 터무니없이 커지지 않습니다.</param>
        private static MuzzleFlashProfile Grade(string name, int grade, int step,
            Color outer, float alpha, float intensity)
        {
            float t = step / 6f;

            return new MuzzleFlashProfile
            {
                name = name,
                grade = grade,
                enabled = true,

                // 중심은 실제 화염처럼 흰빛이 돌고, 등급 색은 바깥에서 드러냅니다.
                colorInner = Color.Lerp(outer, Color.white, 0.6f),
                colorOuter = outer,
                alpha = alpha,
                intensity = intensity,

                // 크기는 실측 기준입니다. 로그로 재 보니 무기 길이가 2m, 게임 기본 화염이
                // 폭 6.5m였습니다. 처음에 잡았던 0.18~0.38m는 게임 화염의 3~6%라
                // 눈에 띌 수가 없었습니다.
                size = Mathf.Lerp(0.9f, 2.2f, t),
                duration = Mathf.Lerp(0.07f, 0.13f, t),
                sizeScale = Mathf.Lerp(1f, 1.6f, t),

                shape = MuzzleFlashShape.Glow,
                sparkCount = Mathf.RoundToInt(Mathf.Lerp(2f, 10f, t)),
                sparkDistance = Mathf.Lerp(0.8f, 2.6f, t),
                sparkSize = Mathf.Lerp(0.12f, 0.32f, t),
                sparkSpread = 16f,
                sparkRise = -0.2f,
                sparkSpin = 0f,
                sparkStretch = true,
            };
        }

        /// <summary>
        /// 시드 기반 무작위 프로필. 값을 따로 굴리지 않고 성격(작고 날카로움 / 보통 / 크고 둔함)을
        /// 먼저 고른 뒤 거기에 맞춥니다.
        /// </summary>
        public static MuzzleFlashProfile CreateRandom(int seed, string name, int grade)
        {
            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Chance(float p) => rng.NextDouble() < p;

            float hue = (float)rng.NextDouble();
            var outer = Color.HSVToRGB(hue, Range(0.45f, 0.95f), 1f);

            // ── 모양을 먼저 고르고 나머지를 거기에 맞춥니다 ──
            //
            // 빛무리(화염)와 하트·별은 어울리는 값이 정반대입니다. 화염은 늘어나는 불티가
            // 아래로 떨어져야 하고, 도형은 늘어나면 안 되고 크고 느리게 떠올라야 읽힙니다.
            // 모양만 따로 굴리면 "늘어난 하트가 땅으로 처박히는" 조합이 절반쯤 나옵니다.
            //
            // 사용자가 vfx_textures에 넣어 둔 PNG도 후보에 넣습니다. 폴더 내용이 바뀌면
            // 같은 시드라도 결과가 달라지는데, 그건 파일을 넣고 뺀 쪽의 선택입니다.
            var builtIn = MuzzleFlashShapes.All;
            var files = WeaponAuraResources.GetTextureNames();

            var shape = MuzzleFlashShape.Glow;
            string textureName = "";

            // 빛무리(=평범한 화염)를 따로 4분의 1 확률로 둡니다. 후보 전체에서 균등하게
            // 뽑으면 도형이 늘어날수록 화염이 나올 확률이 계속 줄어서, 몇 번을 굴려도
            // 하트·별만 나옵니다. 총구 화염 기능인데 화염이 안 나오면 곤란합니다.
            if (!Chance(0.25f))
            {
                int fileCount = files != null ? files.Length : 0;
                int shapedCount = builtIn.Length - 1 + fileCount;

                if (shapedCount > 0)
                {
                    int choice = rng.Next(shapedCount);

                    if (choice < builtIn.Length - 1)
                    {
                        // 0번이 빛무리라 한 칸 밀어 건너뜁니다.
                        shape = builtIn[choice + 1];
                    }
                    else
                    {
                        textureName = files![choice - (builtIn.Length - 1)] ?? "";
                    }
                }
            }

            bool shaped = shape != MuzzleFlashShape.Glow || !string.IsNullOrEmpty(textureName);

            float size;
            float duration;

            if (shaped)
            {
                // 도형은 알갱이가 주인공입니다. 코어는 절반쯤 아예 끕니다.
                size = Chance(0.5f) ? 0f : Range(0.4f, 1.2f);
                duration = Range(0.2f, 0.5f);
            }
            else
            {
                switch (rng.Next(3))
                {
                    case 0:     // 작고 날카롭게
                        size = Range(0.6f, 1.1f);
                        duration = Range(0.055f, 0.08f);
                        break;

                    case 1:     // 보통
                        size = Range(1.1f, 1.9f);
                        duration = Range(0.08f, 0.12f);
                        break;

                    default:    // 크고 둔하게
                        size = Range(1.9f, 3.2f);
                        duration = Range(0.11f, 0.18f);
                        break;
                }
            }

            bool sparks = shaped || Chance(0.75f);

            return new MuzzleFlashProfile
            {
                name = name,
                grade = grade,

                // 중심을 얼마나 하얗게 태울지도 같이 굴립니다.
                colorInner = Color.Lerp(outer, Color.white, Range(0.35f, 0.8f)),
                colorOuter = outer,
                alpha = Range(0.6f, 0.95f),
                intensity = Range(0.9f, 1.7f),

                size = size,
                duration = duration,
                sizeScale = Range(0.8f, 1.8f),

                shape = shape,
                textureName = textureName,

                sparkCount = sparks ? (shaped ? rng.Next(3, 10) : rng.Next(3, 13)) : 0,
                sparkDistance = shaped ? Range(0.8f, 3f) : Range(0.6f, 2.5f),
                sparkSize = shaped ? Range(0.4f, 1.4f) : Range(0.1f, 0.5f),
                sparkSpread = shaped ? Range(15f, 55f) : Range(8f, 40f),

                // 도형은 위로 떠오르는 쪽이 더 그럴듯합니다. 미터 단위라 굴려도 안 튑니다.
                sparkRise = shaped ? Range(0.2f, 1.5f) : Range(-0.5f, 0.2f),
                sparkSpin = shaped ? Range(-300f, 300f) : (Chance(0.3f) ? Range(-360f, 360f) : 0f),

                // 늘어나면 하트가 하트로 안 보입니다.
                sparkStretch = !shaped,
            };
        }

        /// <summary>한 번 눌러 통째로 갈아 끼우는 프리셋 종류</summary>
        public enum PresetKind
        {
            /// <summary>기본 화염 — 내장 글로우 + 앞으로 튀는 불티</summary>
            Flash = 0,

            /// <summary>하트가 뿅뿅 떠오릅니다 (화염 없음)</summary>
            Hearts = 1,

            /// <summary>별가루가 흩뿌려집니다</summary>
            Stardust = 2,
        }

        /// <summary>
        /// 프리셋을 씌웁니다. 등급 값·이름·켜짐 여부는 부르는 쪽에서 유지합니다.
        ///
        /// "화염 대신 하트가 나가는" 식의 연출은 값 대여섯 개를 동시에 맞춰야 나옵니다
        /// (코어 끄기 · 도형 바꾸기 · 중력 뒤집기 · 늘이기 끄기 · 회전). 슬라이더로 하나씩
        /// 찾아가라고 하면 아무도 도달하지 못하는 조합이라 버튼으로 만들어 둡니다.
        /// </summary>
        public static MuzzleFlashProfile CreatePreset(PresetKind kind, string name, int grade)
        {
            var profile = new MuzzleFlashProfile { name = name, grade = grade };

            switch (kind)
            {
                case PresetKind.Hearts:
                    profile.shape = MuzzleFlashShape.Heart;
                    profile.colorInner = new Color(1f, 0.75f, 0.85f);
                    profile.colorOuter = new Color(1f, 0.25f, 0.5f);
                    profile.alpha = 0.95f;
                    profile.intensity = 1.2f;

                    // 화염 코어는 끕니다 — 하트만 나가야 합니다.
                    profile.size = 0f;
                    profile.duration = 0.4f;
                    profile.sizeScale = 1f;

                    profile.sparkCount = 5;
                    profile.sparkDistance = 1.6f;
                    profile.sparkSize = 1.2f;
                    profile.sparkSpread = 32f;
                    profile.sparkRise = 1f;          // 1m 떠오르고 사라집니다
                    profile.sparkSpin = 120f;
                    profile.sparkStretch = false;    // 늘어나면 하트로 안 보입니다
                    break;

                case PresetKind.Stardust:
                    profile.shape = MuzzleFlashShape.Star;
                    profile.colorInner = new Color(1f, 0.97f, 0.8f);
                    profile.colorOuter = new Color(1f, 0.8f, 0.25f);
                    profile.alpha = 0.9f;
                    profile.intensity = 1.6f;

                    profile.size = 1.0f;
                    profile.duration = 0.25f;
                    profile.sizeScale = 1f;

                    profile.sparkCount = 12;
                    profile.sparkDistance = 2.2f;
                    profile.sparkSize = 0.6f;
                    profile.sparkSpread = 45f;
                    profile.sparkRise = 0.4f;
                    profile.sparkSpin = 240f;
                    profile.sparkStretch = false;
                    break;

                case PresetKind.Flash:
                default:
                    var preset = Grade(name, grade, 4, new Color(1f, 0.6f, 0.2f),
                        alpha: 0.88f, intensity: 1.6f);
                    profile.CopyFrom(preset);
                    profile.name = name;
                    profile.grade = grade;
                    break;
            }

            return profile;
        }

        /// <summary>탄환 등급에 해당하는 프로필. 가장 낮은 등급보다 낮아도 첫 프로필을 씁니다.</summary>
        /// <summary>
        /// 이 무기의 총구 화염 프로필.
        ///
        /// 무기 전용(또는 분류 전용) 설정이 있으면 그것이 이깁니다. 없으면 기존대로
        /// 등급으로 고릅니다 — 전용 설정을 안 만든 사람 화면은 그대로입니다.
        /// </summary>
        public static MuzzleFlashProfile? Resolve(int quality, int weaponTypeId)
        {
            var over = WeaponOverrides.Resolve(weaponTypeId);
            return over != null ? over.muzzle : Resolve(quality);
        }

        public static MuzzleFlashProfile? Resolve(int quality)
        {
            var grades = Runtime;
            if (grades.Length == 0)
                return null;

            MuzzleFlashProfile? result = null;
            foreach (var profile in grades)
            {
                if (profile != null && quality >= profile.grade)
                    result = profile;
            }

            return result ?? grades[0];
        }

        public static MuzzleFlashProfile? Get(int index)
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
                var data = new ProfileSetData { version = CurrentVersion, items = ProfileJson.Pack(Runtime) };
                string json = JsonUtility.ToJson(data, true);

                // 만들어진 문자열을 그대로 확인합니다. 저장 파일이 빈 값으로 나오던 원인을
                // 가리기 위한 것입니다 — 여기서 이미 비어 있으면 직렬화 문제이고,
                // 여기서는 멀쩡한데 파일이 비어 있으면 쓰기 문제입니다.
                UnityEngine.Debug.Log(
                    $"[WeaponAura] 저장 직렬화(총구): {json.Length}자, 항목 {Runtime.Length}개, " +
                    $"앞부분={json.Substring(0, Mathf.Min(60, json.Length))}");

                File.WriteAllText(path, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 저장 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>지금 값을 문자열 하나로 떠 둡니다 (되돌리기용).</summary>
        public static string Snapshot()
        {
            return JsonUtility.ToJson(new ProfileSetData { items = ProfileJson.Pack(Runtime) });
        }

        /// <summary>떠 둔 값으로 되돌립니다.</summary>
        public static bool Restore(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<ProfileSetData>(json);
                var loaded = Read(data);
                if (loaded.Length == 0)
                    return false;

                _runtime = loaded;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 되돌리기 실패: {ex.Message}");
                return false;
            }
        }

        public static bool Load(out string path)
        {
            path = GetTuningPath();
            try
            {
                // 새 위치에 없으면 예전 위치(모드 폴더)에서 읽어 옵니다.
                path = WeaponAuraProfiles.ResolveReadPath(TuningFileName);

                if (!File.Exists(path))
                    return false;

                var data = JsonUtility.FromJson<ProfileSetData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null || data.grades == null || data.grades.Length == 0)
                    return false;

                if (data.version < CurrentVersion)
                {
                    UnityEngine.Debug.Log(
                        $"[WeaponAura] 총구 화염 설정이 예전 형식(v{data.version})이라 기본값으로 시작합니다.");
                    return false;
                }

                _runtime = data.grades;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 불러오기 실패: {ex.Message}");
                return false;
            }
        }

        public static void AutoLoad()
        {
            if (Load(out string path))
                UnityEngine.Debug.Log($"[WeaponAura] 저장된 총구 화염 설정을 불러왔습니다: {path}");
        }
    }
}
