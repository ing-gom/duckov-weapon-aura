using System;
using System.Collections.Generic;
using System.Reflection;
using ItemStatsSystem;
using UnityEngine;

namespace WeaponAura.Helpers
{
    /// <summary>무기 분류 — 총기냐 근접이냐</summary>
    public enum WeaponKind
    {
        Gun = 0,
        Melee = 1,
    }

    /// <summary>카탈로그 한 줄. 손에 없는 무기도 이만큼은 알 수 있습니다.</summary>
    public sealed class WeaponCatalogEntry
    {
        public int TypeId;
        public string Name = "-";
        public int Quality;
        public WeaponKind Kind;

        /// <summary>총기 분류(AR · SMG …). 근접무기는 null.</summary>
        public string? GunClass;

        /// <summary>인벤토리 아이콘. 메타데이터에 이미 들어 있어서 추가 로딩이 없습니다.</summary>
        public Sprite? Icon;

        /// <summary>
        /// 미리보기 모델을 만들 수 있는지. null이면 아직 확인 전입니다.
        ///
        /// 이름·태그로 걸러 내지 않고 실제로 만들어 보는 이유 — "맨손"처럼 근접무기 태그를
        /// 달았지만 모델이 없는 항목이 실제로 7개 있었습니다. 이름 규칙으로 거르면 게임이
        /// 항목을 추가할 때마다 조용히 틀립니다. 만들어 보는 쪽은 항상 맞습니다.
        /// </summary>
        public bool? HasModel;
    }

    /// <summary>
    /// 게임에 존재하는 <b>모든</b> 무기 목록.
    ///
    /// 지금까지 이 모드는 "손에 든 무기"만 볼 수 있었습니다. 그래서 무기별 설정을 하려면
    /// 그 총을 실제로 주울 때까지 꾸밀 수 없었습니다. 카탈로그는 그 제약을 없앱니다.
    ///
    /// 열거 경로가 넷인 이유 — <c>ItemAssetsCollection</c>의 공개 API가 게임 버전마다
    /// 다르고, 필터 기본값의 의미(등급 0~0인지 전체인지)가 코드만 봐서는 확정되지 않습니다.
    /// 하나가 빈손이면 다음으로 넘어가고, 어느 경로로 찾았는지 <see cref="ResolvedSource"/>에
    /// 남겨서 로그로 확인할 수 있게 합니다.
    /// </summary>
    public static class WeaponCatalog
    {
        public enum CatalogSource
        {
            None = 0,

            /// <summary>
            /// GetAllTypeIds(default) — <b>쓰지 않습니다.</b>
            ///
            /// 실측 결과 기본값은 "등급 0~0"으로 해석돼서 아이템 8개(무기 2정)만 나옵니다.
            /// 진단이 이 사실을 계속 확인할 수 있도록 열거 경로로는 남겨 두되,
            /// 카탈로그 구성에서는 제외합니다.
            /// </summary>
            Filter = 1,

            /// <summary>GetAllTypeIds(등급 0~int.MaxValue) — 범위를 명시. 실사용 1순위.</summary>
            FilterRange = 2,

            /// <summary>Instance.entries 리플렉션 (Entry가 비공개 중첩 타입이라 직접 못 씁니다)</summary>
            CollectionEntries = 3,

            /// <summary>1..NextTypeID 전수 조사 — 마지막 수단</summary>
            Scan = 4,
        }

        /// <summary>전수 조사 상한. NextTypeID를 못 읽었을 때만 씁니다.</summary>
        private const int ScanFallbackMax = 20000;

        private static List<WeaponCatalogEntry>? _all;

        /// <summary>실제로 목록을 채운 경로 (진단용)</summary>
        public static CatalogSource ResolvedSource { get; private set; } = CatalogSource.None;

        /// <summary>무기 전체. 최초 접근에서 한 번 만들고 캐시합니다.</summary>
        public static IReadOnlyList<WeaponCatalogEntry> All
        {
            get
            {
                if (_all == null)
                    _all = Build();
                return _all;
            }
        }

        /// <summary>다음 접근에서 다시 만들게 합니다 (게임 재시작 없이 확인할 때).</summary>
        public static void Invalidate()
        {
            _all = null;
            ResolvedSource = CatalogSource.None;
        }

        /// <summary>
        /// 경로 하나로 TypeID를 열거합니다. 진단이 네 경로를 각각 비교할 수 있도록
        /// 공개해 둡니다. 실패하면 빈 배열.
        /// </summary>
        public static int[] EnumerateTypeIds(CatalogSource source)
        {
            try
            {
                switch (source)
                {
                    case CatalogSource.Filter:
                        return ItemAssetsCollection.GetAllTypeIds(default) ?? Array.Empty<int>();

                    case CatalogSource.FilterRange:
                        var filter = new ItemFilter { minQuality = 0, maxQuality = int.MaxValue };
                        return ItemAssetsCollection.GetAllTypeIds(filter) ?? Array.Empty<int>();

                    case CatalogSource.CollectionEntries:
                        return FromCollectionEntries();

                    case CatalogSource.Scan:
                        return FromScan();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 무기 열거 실패({source}): {ex.Message}");
            }

            return Array.Empty<int>();
        }

        private static List<WeaponCatalogEntry> Build()
        {
            // 싼 것부터. 앞이 성공하면 뒤는 아예 돌지 않습니다.
            //
            // CatalogSource.Filter는 일부러 뺐습니다 — 기본 필터는 등급 0짜리 8개만
            // 돌려주는데, 그것도 "0개는 아니"라서 순서에 넣어 두면 조용히 채택됩니다.
            // 실제로 첫 실측에서 무기 2정짜리 카탈로그가 만들어졌습니다.
            var order = new[]
            {
                CatalogSource.FilterRange,
                CatalogSource.CollectionEntries,
                CatalogSource.Scan,
            };

            foreach (var source in order)
            {
                var ids = EnumerateTypeIds(source);
                if (ids.Length == 0)
                    continue;

                var built = ToEntries(ids);

                // 아이디는 나왔는데 무기가 하나도 없으면 그 경로는 못 믿습니다.
                if (built.Count == 0)
                    continue;

                ResolvedSource = source;
                UnityEngine.Debug.Log(
                    $"[WeaponAura] 무기 카탈로그: {built.Count}개 (경로={source}, 후보 {ids.Length}개)");
                return built;
            }

            ResolvedSource = CatalogSource.None;
            UnityEngine.Debug.LogWarning(
                "[WeaponAura] 무기 카탈로그를 만들지 못했습니다 — 네 경로 모두 빈손입니다.");
            return new List<WeaponCatalogEntry>();
        }

        /// <summary>TypeID 목록에서 무기만 걸러 카탈로그 줄로 만듭니다.</summary>
        private static List<WeaponCatalogEntry> ToEntries(int[] typeIds)
        {
            var result = new List<WeaponCatalogEntry>();
            var seen = new HashSet<int>();

            foreach (int typeId in typeIds)
            {
                if (typeId <= 0 || !seen.Add(typeId))
                    continue;

                bool isGun = WeaponHelper.IsGun(typeId);
                bool isMelee = !isGun && WeaponHelper.IsMeleeWeapon(typeId);

                if (!isGun && !isMelee)
                    continue;

                string name = WeaponHelper.GetDisplayName(typeId);

                // 이름이 *Item_SNP_MingChao_Carletta* 처럼 별표로 감싸여 있으면 번역 키를
                // 찾지 못한 것입니다 = 아직 게임에 안 나온 내부 항목. 목록에 올리면
                // 고를 수는 있는데 무엇인지 알 수 없는 줄이 됩니다.
                if (IsUntranslated(name))
                    continue;

                Sprite? icon = null;
                try
                {
                    icon = ItemAssetsCollection.GetMetaData(typeId).icon;
                }
                catch
                {
                    // 아이콘이 없어도 목록에는 올립니다 — 이름만으로도 고를 수 있습니다.
                }

                result.Add(new WeaponCatalogEntry
                {
                    TypeId = typeId,
                    Name = name,
                    Quality = WeaponHelper.GetMetaQuality(typeId),
                    Kind = isGun ? WeaponKind.Gun : WeaponKind.Melee,
                    GunClass = isGun ? WeaponHelper.GetGunClass(typeId) : null,
                    Icon = icon,
                });
            }

            // 총기 먼저, 그 안에서는 분류 → 등급 → 이름. 목록이 매번 같은 순서로 나와야
            // "아까 그 자리"를 다시 찾을 수 있습니다.
            result.Sort((a, b) =>
            {
                int byKind = a.Kind.CompareTo(b.Kind);
                if (byKind != 0)
                    return byKind;

                int byClass = string.CompareOrdinal(a.GunClass ?? "", b.GunClass ?? "");
                if (byClass != 0)
                    return byClass;

                int byQuality = a.Quality.CompareTo(b.Quality);
                if (byQuality != 0)
                    return byQuality;

                return string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
            });

            return result;
        }

        /// <summary>번역 키를 못 찾아 별표로 감싸인 이름인지 (*Item_XXX* 형태)</summary>
        private static bool IsUntranslated(string name)
        {
            return name.Length >= 2
                   && name[0] == '*'
                   && name[name.Length - 1] == '*';
        }

        /// <summary>
        /// 각 무기의 모델을 실제로 한 번 만들어 보고 <see cref="WeaponCatalogEntry.HasModel"/>을
        /// 채웁니다. 이미 확인된 항목은 건너뜁니다.
        ///
        /// 카탈로그를 만들 때가 아니라 <b>목록을 처음 열 때</b> 부릅니다 — 모델 생성은
        /// 게임 월드가 올라와 있어야 하고, 메인 메뉴에서 돌리면 전부 실패로 굳습니다.
        /// 실측 기준 142정에 20ms 남짓이라 창이 열리는 순간 한 번이면 체감되지 않습니다.
        /// </summary>
        public static void ValidateModels()
        {
            var host = new GameObject("WeaponAura_CatalogValidate");
            host.transform.position = new Vector3(0f, -2000f, 0f);

            int checkedCount = 0, without = 0;

            try
            {
                foreach (var entry in All)
                {
                    if (entry.HasModel.HasValue)
                        continue;

                    var handle = WeaponModelSource.Create(entry.TypeId, host.transform);
                    entry.HasModel = handle != null;
                    handle?.Dispose();

                    checkedCount++;
                    if (handle == null)
                        without++;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 모델 확인 실패: {ex.Message}");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }

            if (checkedCount > 0)
                UnityEngine.Debug.Log($"[WeaponAura] 모델 확인: {checkedCount}정 중 {without}정은 모델이 없습니다.");
        }

        /// <summary>모델이 없다고 확인된 것을 뺀 목록 (라이브러리 UI용)</summary>
        public static List<WeaponCatalogEntry> WithModel()
        {
            var result = new List<WeaponCatalogEntry>();

            foreach (var entry in All)
            {
                // 아직 확인 전(null)이면 일단 보여 줍니다 — 확인이 안 됐다고 숨기면
                // 월드가 없을 때 목록이 통째로 비어 버립니다.
                if (entry.HasModel != false)
                    result.Add(entry);
            }

            return result;
        }

        /// <summary>
        /// <c>Instance.entries</c>를 리플렉션으로 읽습니다.
        ///
        /// 필드 자체는 public이지만 원소 타입(<c>ItemAssetsCollection.Entry</c>)이 비공개
        /// 중첩 클래스여서 C#에서 직접 다룰 수 없습니다. 그래서 리플렉션이 필요합니다.
        /// </summary>
        private static int[] FromCollectionEntries()
        {
            var collection = ItemAssetsCollection.Instance;
            if (collection == null)
                return Array.Empty<int>();

            var field = typeof(ItemAssetsCollection).GetField("entries",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field?.GetValue(collection) is not System.Collections.IEnumerable entries)
                return Array.Empty<int>();

            var ids = new List<int>();
            FieldInfo? typeIdField = null;

            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;

                if (typeIdField == null)
                {
                    typeIdField = entry.GetType().GetField("typeID",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (typeIdField?.GetValue(entry) is int typeId && typeId > 0)
                    ids.Add(typeId);
            }

            return ids.ToArray();
        }

        /// <summary>
        /// 1부터 NextTypeID까지 훑으면서 메타데이터가 있는 것만 추립니다.
        ///
        /// 마지막 수단입니다. 수천 번 호출이지만 사전 조회라 창을 처음 열 때 한 번이면
        /// 체감되지 않습니다. 무엇보다 필터 의미에 기대지 않아서 확실합니다.
        /// </summary>
        private static int[] FromScan()
        {
            int max = ScanFallbackMax;

            try
            {
                var collection = ItemAssetsCollection.Instance;
                if (collection != null && collection.NextTypeID > 0)
                    max = Mathf.Min(collection.NextTypeID + 1, ScanFallbackMax);
            }
            catch
            {
                // 기본 상한으로 진행
            }

            var ids = new List<int>();

            for (int typeId = 1; typeId <= max; typeId++)
            {
                try
                {
                    if (ItemAssetsCollection.GetMetaData(typeId).id > 0)
                        ids.Add(typeId);
                }
                catch
                {
                    // 빈 자리는 건너뜁니다.
                }
            }

            return ids.ToArray();
        }
    }
}
