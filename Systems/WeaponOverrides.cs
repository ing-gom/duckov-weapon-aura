using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using WeaponAura.Helpers;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 무기 하나(또는 분류 하나)에 붙는 이펙트 한 벌.
    ///
    /// 네 이펙트를 통째로 들고 갑니다 — "이 무기는 통째로 내 설정"이 되도록. 그래서 나중에
    /// 등급 기본값을 바꿔도 전용 설정을 만들어 둔 무기는 따라오지 않습니다. 그게 의도입니다.
    ///
    /// JsonUtility로 직렬화되므로 public 필드만 씁니다.
    /// </summary>
    [Serializable]
    public class WeaponOverride
    {
        /// <summary>
        /// 무엇에 붙는 설정인지. <c>weapon:240</c> · <c>class:AR</c> 형태입니다.
        ///
        /// 종류와 값을 한 문자열로 합쳐 두면 저장 배열이 하나로 끝나고, 나중에 축이
        /// 하나 더 늘어도(예: 구경별) 자료구조를 안 건드립니다.
        /// </summary>
        public string key = "";

        /// <summary>
        /// 만들 때의 무기 이름.
        ///
        /// <b>조회에는 쓰지 않습니다.</b> 어떤 무기인지는 <see cref="key"/>의 TypeID가
        /// 정합니다 — 이름은 언어 설정이나 게임 갱신으로 바뀔 수 있어서 기준이 될 수
        /// 없습니다. 여기 담아 두는 것은 TypeID를 못 읽을 때(카탈로그가 아직 없거나
        /// 그 무기가 사라진 경우) 목록에 보여 줄 마지막 수단입니다.
        /// </summary>
        public string label = "";

        /// <summary>
        /// 지금 화면에 보여 줄 이름. <b>TypeID에서 다시 읽습니다.</b>
        ///
        /// 저장해 둔 이름을 그대로 쓰면, 언어를 바꾸거나 게임이 이름을 손본 뒤에
        /// 목록만 옛 이름으로 남습니다. 실제로 무엇이 적용되는지는 TypeID가 정하므로
        /// 보여 주는 이름도 거기서 나와야 어긋나지 않습니다.
        /// </summary>
        public string ResolveLabel()
        {
            const string weaponPrefix = "weapon:";

            if (key.StartsWith(weaponPrefix, StringComparison.Ordinal)
                && int.TryParse(key.Substring(weaponPrefix.Length), out int typeId))
            {
                string name = Helpers.WeaponHelper.GetDisplayName(typeId);
                if (!string.IsNullOrEmpty(name) && name != "-" && !name.StartsWith("#", StringComparison.Ordinal))
                    return name;
            }

            return string.IsNullOrEmpty(label) ? key : label;
        }

        public WeaponAuraProfile aura = new WeaponAuraProfile();
        public BulletTrailProfile trail = new BulletTrailProfile();
        public MuzzleFlashProfile muzzle = new MuzzleFlashProfile();
        public MeleeSlashProfile melee = new MeleeSlashProfile();

        public WeaponOverride Clone()
        {
            return new WeaponOverride
            {
                key = key,
                label = label,
                aura = aura?.Clone() ?? new WeaponAuraProfile(),
                trail = trail?.Clone() ?? new BulletTrailProfile(),
                muzzle = muzzle?.Clone() ?? new MuzzleFlashProfile(),
                melee = melee?.Clone() ?? new MeleeSlashProfile(),
            };
        }
    }

    /// <summary>
    /// 무기별 · 분류별 전용 설정 저장소.
    ///
    /// 해석 순서는 <b>개별 무기 → 분류 → 등급</b>입니다. 구체적인 것이 항상 이깁니다.
    /// 전용 설정을 만들지 않은 무기는 기존 등급 티어를 그대로 쓰므로, 이 기능을 안 쓰는
    /// 사람 화면은 하나도 바뀌지 않습니다.
    ///
    /// 저장은 "손댄 것만" 합니다. 무기 142정 × 이펙트 4개를 전부 적으면 파일이 수 MB가
    /// 되지만, 실제로 만든 항목만 적으면 몇 KB입니다.
    /// </summary>
    public static class WeaponOverrides
    {
        public const string FileName = "weapon_aura_overrides.json";

        private const string WeaponPrefix = "weapon:";

        /// <summary>
        /// 예전에 쓰던 분류(타입별) 키의 접두사.
        ///
        /// 분류 층은 없앴습니다 — 총기별과 등급별 둘로 충분하고, 가운데 층이 있으면
        /// "왜 이 총이 이렇게 보이지"의 답이 세 군데로 갈립니다. 접두사만 남겨 두는 것은
        /// 예전 저장 파일에 남아 있는 항목을 <b>불러올 때 걷어내기</b> 위해서입니다.
        /// </summary>
        private const string LegacyClassPrefix = "class:";

        /// <summary>
        /// 파일에 담기는 전용 설정 한 줄.
        ///
        /// 프로필 넷을 <b>문자열로</b> 들고 있습니다. Unity가 중첩된 사용자 정의 클래스를
        /// 담지 못해서(<see cref="ProfileJson"/> 참고) 그대로 두면 통째로 사라집니다.
        /// </summary>
        [Serializable]
        private class OverrideEntryData
        {
            public string key = "";
            public string label = "";
            public string aura = "";
            public string trail = "";
            public string muzzle = "";
            public string melee = "";
        }

        [Serializable]
        private class OverrideSetData
        {
            /// <summary>예전 형식 — 늘 비어 있었습니다. 읽기만 합니다.</summary>
            public WeaponOverride[] overrides = Array.Empty<WeaponOverride>();

            /// <summary>
            /// 전용 설정 하나하나를 맨 위 객체로 직렬화한 <b>문자열</b>들.
            ///
            /// 처음에는 여기에 <c>OverrideEntryData[]</c>를 그대로 담았습니다. 같은 함정에
            /// 한 겹 더 빠진 것이었습니다 — 그것도 모드 어셈블리의 사용자 정의 클래스라
            /// 배열째로 버려져서 파일이 또 "{}"로 나왔습니다. 중첩되는 순간 종류를 가리지
            /// 않고 사라지므로, 문자열까지 내려야 끝납니다.
            /// </summary>
            public string[] items = Array.Empty<string>();
        }

        private static OverrideEntryData ToData(WeaponOverride entry)
        {
            return new OverrideEntryData
            {
                key = entry.key,
                label = entry.label,
                aura = ProfileJson.One(entry.aura),
                trail = ProfileJson.One(entry.trail),
                muzzle = ProfileJson.One(entry.muzzle),
                melee = ProfileJson.One(entry.melee),
            };
        }

        private static WeaponOverride FromData(OverrideEntryData data)
        {
            return new WeaponOverride
            {
                key = data.key,
                label = data.label,
                aura = ProfileJson.OneFrom<WeaponAuraProfile>(data.aura) ?? new WeaponAuraProfile(),
                trail = ProfileJson.OneFrom<BulletTrailProfile>(data.trail) ?? new BulletTrailProfile(),
                muzzle = ProfileJson.OneFrom<MuzzleFlashProfile>(data.muzzle) ?? new MuzzleFlashProfile(),
                melee = ProfileJson.OneFrom<MeleeSlashProfile>(data.melee) ?? new MeleeSlashProfile(),
            };
        }

        /// <summary>지금 값을 파일에 담을 형태로.</summary>
        private static OverrideSetData BuildData()
        {
            var list = new List<string>(_byKey.Count);

            foreach (var entry in _byKey.Values)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    continue;

                // 한 줄을 맨 위 객체로 직렬화합니다. OverrideEntryData는 문자열만 들고
                // 있어서 이 단계에서는 온전히 담깁니다.
                string json = ProfileJson.One(ToData(entry));
                if (!string.IsNullOrEmpty(json))
                    list.Add(json);
            }

            return new OverrideSetData { items = list.ToArray() };
        }

        /// <summary>저장본을 사전으로 되돌립니다. 새 형식이 먼저, 없으면 옛 형식.</summary>
        private static void ReadInto(OverrideSetData? data)
        {
            _byKey.Clear();

            if (data == null)
                return;

            if (data.items != null && data.items.Length > 0)
            {
                foreach (string raw in data.items)
                {
                    var item = ProfileJson.OneFrom<OverrideEntryData>(raw);
                    if (item == null || string.IsNullOrEmpty(item.key))
                        continue;

                    // 예전 버전에서 만든 분류 설정은 버립니다. 남겨 두면 목록에는 보이는데
                    // 어디에도 적용되지 않는 유령 항목이 됩니다.
                    if (item.key.StartsWith(LegacyClassPrefix, StringComparison.Ordinal))
                        continue;

                    var entry = FromData(item);
                    _byKey[entry.key] = entry;
                }

                return;
            }

            if (data.overrides == null)
                return;

            foreach (var entry in data.overrides)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    continue;

                if (entry.key.StartsWith(LegacyClassPrefix, StringComparison.Ordinal))
                    continue;

                entry.aura ??= new WeaponAuraProfile();
                entry.trail ??= new BulletTrailProfile();
                entry.muzzle ??= new MuzzleFlashProfile();
                entry.melee ??= new MeleeSlashProfile();

                _byKey[entry.key] = entry;
            }
        }

        /// <summary>키 → 설정. 조회가 발사 경로에서 일어나므로 사전으로 들고 있습니다.</summary>
        private static readonly Dictionary<string, WeaponOverride> _byKey =
            new Dictionary<string, WeaponOverride>(StringComparer.Ordinal);

        /// <summary>값이 바뀌었을 때 (설정 창·오라 재생성용)</summary>
        public static event Action? OnChanged;

        /// <summary>등록된 전용 설정 수</summary>
        public static int Count => _byKey.Count;

        // ── 키 ──────────────────────────────────────────────────────

        public static string WeaponKey(int typeId) => WeaponPrefix + typeId.ToString();

        /// <summary>사람이 읽을 수 있는 키 설명 (목록·로그용)</summary>
        public static string DescribeKey(string key)
        {
            if (key.StartsWith(WeaponPrefix, StringComparison.Ordinal))
            {
                string raw = key.Substring(WeaponPrefix.Length);
                return int.TryParse(raw, out int typeId)
                    ? WeaponHelper.GetDisplayName(typeId)
                    : raw;
            }

            return key;
        }

        // ── 조회 ────────────────────────────────────────────────────

        /// <summary>
        /// 이 무기에 적용할 전용 설정. 없으면 null (호출부가 등급 티어로 넘어갑니다).
        ///
        /// 층은 둘뿐입니다 — <b>이 무기 전용</b>이 있으면 그것, 없으면 등급별.
        /// </summary>
        public static WeaponOverride? Resolve(int typeId)
        {
            if (typeId <= 0 || _byKey.Count == 0)
                return null;

            return _byKey.TryGetValue(WeaponKey(typeId), out var exact) ? exact : null;
        }

        /// <summary>
        /// 해석 결과를 구분하는 짧은 표식.
        ///
        /// 오라는 "같은 상태면 다시 만들지 않는" 최적화가 걸려 있는데, 티어가 같고 전용
        /// 설정만 다른 두 무기를 번갈아 들면 그 판정이 둘을 같다고 봅니다. 그래서 추적
        /// 상태에 이 값을 함께 넣습니다. 전용 설정이 없으면 빈 문자열입니다.
        /// </summary>
        public static string ResolveKey(int typeId) => Resolve(typeId)?.key ?? "";

        public static WeaponOverride? Get(string key)
        {
            return _byKey.TryGetValue(key, out var found) ? found : null;
        }

        public static bool Has(string key) => _byKey.ContainsKey(key);

        /// <summary>등록된 키 전체 (목록 UI용). 순서가 매번 같도록 정렬해서 돌려줍니다.</summary>
        public static List<WeaponOverride> AllSorted()
        {
            var list = new List<WeaponOverride>(_byKey.Values);
            list.Sort((a, b) => string.Compare(a.ResolveLabel(), b.ResolveLabel(), StringComparison.CurrentCulture));
            return list;
        }

        // ── 만들기 / 지우기 ─────────────────────────────────────────

        /// <summary>
        /// 전용 설정을 새로 만듭니다. 시작값은 <b>지금 그 무기에 적용 중인 값</b>입니다.
        ///
        /// 빈 기본값에서 시작하면 만들자마자 무기 모습이 달라져서 "설정을 만들었더니
        /// 이펙트가 사라졌다"가 됩니다. 지금 보이는 것에서 출발해야 손대는 만큼만 바뀝니다.
        /// </summary>
        /// <returns>이미 있으면 기존 것을 그대로 돌려줍니다.</returns>
        public static WeaponOverride Create(string key, string label, int sampleTypeId)
        {
            if (_byKey.TryGetValue(key, out var existing))
                return existing;

            int quality = WeaponHelper.GetMetaQuality(sampleTypeId);

            var created = new WeaponOverride
            {
                key = key,
                label = label,
                aura = SnapshotAura(quality),
                trail = BulletTrailProfiles.Resolve(quality)?.Clone() ?? new BulletTrailProfile(),
                muzzle = MuzzleFlashProfiles.Resolve(quality)?.Clone() ?? new MuzzleFlashProfile(),
                melee = MeleeSlashProfiles.Resolve(quality)?.Clone() ?? new MeleeSlashProfile(),
            };

            // 고른 무기의 등급 티어가 꺼져 있으면 전용 설정도 꺼진 채로 복사됩니다.
            // 그러면 무기를 고르자마자 아무것도 안 보이고, 왜 안 보이는지도 알 수 없습니다
            // ("특정 무기를 고르면 이펙트가 아예 꺼진다"의 원인).
            //
            // 전용 설정을 만든다는 것은 <b>그 무기를 꾸미겠다</b>는 뜻입니다. 등급 단위로
            // 꺼 둔 것과는 의도가 다르므로 켜 놓고 시작합니다. 끄고 싶으면 끄면 됩니다.
            created.aura.enabled = true;
            created.trail.enabled = true;
            created.muzzle.enabled = true;
            created.melee.enabled = true;

            // 겹도 같이 켭니다. 프로필만 켜고 그 안의 겹이 꺼진 채로 복사되면
            // "켜져 있다는데 아무것도 안 나온다"가 됩니다.
            foreach (var layer in created.aura.layers)
            {
                if (layer != null)
                    layer.enabled = true;
            }

            // 속성 무기(불꽃·프로스트 등)는 그 색으로 시작합니다.
            //
            // 등급 색으로 시작하면 파란 프로스트 총에 붉은 신화 오라가 붙은 채로 열립니다.
            // 사용자가 곧바로 색부터 고쳐야 하는 시작 화면은 좋은 기본값이 아닙니다.
            // 색만 가져옵니다 — 형태·움직임 값은 원본에 대응하는 개념이 없습니다.
            ApplyAttributeColors(created, sampleTypeId);

            _byKey[key] = created;
            OnChanged?.Invoke();

            UnityEngine.Debug.Log($"[WeaponAura] 전용 설정 생성: {label} ({key}, 기준 등급 {quality})");
            return created;
        }

        /// <summary>
        /// 무기에 원래 붙어 있는 속성 색을 시작값으로 얹습니다. 속성이 없으면 아무 일도 없습니다.
        ///
        /// </summary>
        private static void ApplyAttributeColors(WeaponOverride target, int sampleTypeId)
        {
            if (!target.key.StartsWith(WeaponPrefix, StringComparison.Ordinal))
                return;

            WeaponAttributeColors colors;
            try
            {
                colors = WeaponAttributeEffect.FromCatalog(sampleTypeId);
            }
            catch
            {
                return;
            }

            if (!colors.Found)
                return;

            target.aura.colorA = colors.Primary;
            target.aura.colorB = colors.Secondary;

            target.trail.colorStart = colors.Primary;
            target.trail.colorEnd = colors.Secondary;

            target.muzzle.colorInner = colors.Secondary;
            target.muzzle.colorOuter = colors.Primary;

            target.melee.slashColor = colors.Primary;
            target.melee.colorInner = colors.Secondary;
            target.melee.colorOuter = colors.Primary;

            // 오라 위에 얹은 겹도 같은 색으로 시작합니다 — 본체와 겹의 색이 따로 놀면
            // 계승의 의미가 없습니다.
            foreach (var layer in target.aura.layers)
            {
                if (layer != null)
                    layer.color = colors.Primary;
            }

            UnityEngine.Debug.Log(
                $"[WeaponAura] 속성 색 시작값 적용: {target.label} — " +
                $"{colors.Primary} / {colors.Secondary} (출처 {colors.Source})");
        }

        private static WeaponAuraProfile SnapshotAura(int quality)
        {
            int tier = WeaponAuraProfiles.ResolveTier(quality);
            var source = WeaponAuraProfiles.Get(tier);
            return source?.Clone() ?? new WeaponAuraProfile();
        }

        public static bool Remove(string key)
        {
            if (!_byKey.Remove(key))
                return false;

            OnChanged?.Invoke();
            UnityEngine.Debug.Log($"[WeaponAura] 전용 설정 삭제: {key}");
            return true;
        }

        public static void Clear()
        {
            if (_byKey.Count == 0)
                return;

            _byKey.Clear();
            OnChanged?.Invoke();
        }

        /// <summary>값을 고친 뒤 알립니다 (편집기가 프로필을 직접 수정하므로 갱신 신호만).</summary>
        public static void NotifyChanged() => OnChanged?.Invoke();

        // ── 저장 / 불러오기 ─────────────────────────────────────────

        /// <summary>지금 값을 문자열 하나로 떠 둡니다 (되돌리기용).</summary>
        public static string Snapshot()
        {
            return JsonUtility.ToJson(BuildData());
        }

        /// <summary>
        /// 떠 둔 값으로 되돌립니다.
        ///
        /// 항목이 사라지는 경우(만들었다가 되돌리기)도 있으므로 사전을 통째로 갈아 끼웁니다.
        /// </summary>
        public static bool Restore(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<OverrideSetData>(json);
                if (data == null)
                    return false;

                ReadInto(data);

                OnChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 전용 설정 되돌리기 실패: {ex.Message}");
                return false;
            }
        }

        public static string GetPath() => Path.Combine(WeaponAuraProfiles.GetSaveFolder(), FileName);

        public static bool Save(out string path)
        {
            path = GetPath();

            try
            {
                var data = BuildData();

                string json = JsonUtility.ToJson(data, true);

                UnityEngine.Debug.Log(
                    $"[WeaponAura] 저장 직렬화(전용): {json.Length}자, 항목 {_byKey.Count}개, " +
                    $"앞부분={json.Substring(0, Mathf.Min(60, json.Length))}");

                File.WriteAllText(path, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 전용 설정 저장 실패: {ex.Message}");
                return false;
            }
        }

        public static bool Load(out string path)
        {
            path = GetPath();

            try
            {
                // 새 위치에 없으면 예전 위치(모드 폴더)에서 읽어 옵니다.
                path = WeaponAuraProfiles.ResolveReadPath(FileName);

                if (!File.Exists(path))
                    return false;

                var data = JsonUtility.FromJson<OverrideSetData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null)
                    return false;

                ReadInto(data);

                OnChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 전용 설정 불러오기 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>모드가 켜질 때 조용히 복원합니다. 파일이 없으면 아무 일도 없습니다.</summary>
        public static void AutoLoad()
        {
            if (Load(out string path))
                UnityEngine.Debug.Log($"[WeaponAura] 전용 설정 {_byKey.Count}개 불러옴: {path}");
        }
    }
}
