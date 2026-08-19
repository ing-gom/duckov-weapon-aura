using System;
using ItemStatsSystem;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// 무기 판별 및 등급 조회. ItemAssetsCollection의 메타데이터 태그를 봅니다.
    ///
    /// 부착물(머즐·소음기·스톡 등)은 GunType 태그만 갖고 Tag_Gun이 없기 때문에,
    /// 총기로 인정하려면 "GunType 계열 + Gun 계열"이 둘 다 있어야 합니다.
    ///
    /// 판정은 전부 <b>TypeID</b> 기준입니다. Item 인스턴스를 받는 쪽은 TypeID를 꺼내
    /// 넘기기만 합니다 — 손에 없는 무기(카탈로그의 프리팹)도 같은 규칙으로 걸러야 하기
    /// 때문입니다.
    /// </summary>
    public static class WeaponHelper
    {
        /// <summary>런타임에 따라 "Tag_Gun" 또는 "Gun"으로 올 수 있습니다.</summary>
        private static readonly string[] GunTags = { "Tag_Gun", "Gun" };

        /// <summary>총기 분류 태그의 접두사. 뒤에 붙는 것이 분류 이름(AR·SMG…)입니다.</summary>
        private static readonly string[] GunTypePrefixes = { "Tag_GunType_", "GunType_" };

        private static readonly string[] MeleeTags = { "MeleeWeapon", "Tag_MeleeWeapon" };
        private static readonly string[] RocketTags = { "GunType_Rocket", "Tag_GunType_Rocket" };

        /// <summary>총기 또는 근접무기</summary>
        public static bool IsWeapon(Item? item) => IsWeapon(TypeIdOf(item));

        /// <summary>총기 또는 근접무기 (TypeID 기준)</summary>
        public static bool IsWeapon(int typeId) => IsGun(typeId) || IsMeleeWeapon(typeId);

        public static bool IsGun(Item? item) => IsGun(TypeIdOf(item));

        public static bool IsGun(int typeId)
        {
            if (typeId <= 0)
                return false;

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                if (meta.id <= 0 || meta.tags == null)
                    return false;

                bool hasGunType = false;
                bool hasGunTag = false;

                foreach (var tag in meta.tags)
                {
                    if (tag?.name == null)
                        continue;
                    if (GunClassFrom(tag.name) != null)
                        hasGunType = true;
                    if (Array.IndexOf(GunTags, tag.name) >= 0)
                        hasGunTag = true;
                }

                return hasGunType && hasGunTag;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsMeleeWeapon(Item? item) => IsMeleeWeapon(TypeIdOf(item));

        public static bool IsMeleeWeapon(int typeId) => HasAnyTag(typeId, MeleeTags);

        public static bool IsRocketWeapon(Item? item) => IsRocketWeapon(TypeIdOf(item));

        public static bool IsRocketWeapon(int typeId) => HasAnyTag(typeId, RocketTags);

        /// <summary>
        /// 총기 분류 이름 (AR · SMG · SR …). 총기가 아니거나 분류 태그가 없으면 null.
        ///
        /// 태그 목록을 고정 배열로 갖지 않고 접두사로 잘라 내는 이유 — 게임이 새 분류를
        /// 추가하면 고정 배열은 그 무기를 통째로 놓칩니다. 접두사 방식은 이름만 새로 나옵니다.
        /// </summary>
        public static string? GetGunClass(int typeId)
        {
            if (typeId <= 0)
                return null;

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                if (meta.id <= 0 || meta.tags == null)
                    return null;

                foreach (var tag in meta.tags)
                {
                    string? cls = tag?.name == null ? null : GunClassFrom(tag.name);
                    if (cls != null)
                        return cls;
                }
            }
            catch
            {
                // 아래에서 null
            }

            return null;
        }

        public static string? GetGunClass(Item? item) => GetGunClass(TypeIdOf(item));

        /// <summary>근접무기 전체를 묶는 분류 이름. 총기 분류(AR·SMG…)와 같은 자리에 씁니다.</summary>
        public const string MeleeClass = "Melee";

        /// <summary>
        /// 분류 키 — 총기는 분류 이름, 근접무기는 <see cref="MeleeClass"/>. 무기가 아니면 null.
        ///
        /// 근접무기에는 GunType 태그가 없어서 총기 분류만으로는 "근접 전부"를 한 번에
        /// 지정할 수 없습니다. 같은 축에 얹어 두면 분류별 설정이 무기 종류를 안 가립니다.
        /// </summary>
        public static string? GetClassKey(int typeId)
        {
            string? gunClass = GetGunClass(typeId);
            if (gunClass != null)
                return gunClass;

            return IsMeleeWeapon(typeId) ? MeleeClass : null;
        }

        /// <summary>"Tag_GunType_AR" · "GunType_AR" → "AR". 분류 태그가 아니면 null.</summary>
        private static string? GunClassFrom(string tagName)
        {
            foreach (string prefix in GunTypePrefixes)
            {
                if (tagName.Length > prefix.Length && tagName.StartsWith(prefix, StringComparison.Ordinal))
                    return tagName.Substring(prefix.Length);
            }

            return null;
        }

        /// <summary>
        /// 아이템 등급.
        ///
        /// 게임이 인벤토리에서 색으로 보여주는 희귀도는 <b>인스턴스별</b> <c>Item.DisplayQuality</c>입니다.
        /// (루팅될 때 굴려지므로 같은 총이라도 개체마다 다릅니다)
        /// 메타데이터의 <c>quality</c>는 프리팹 기본값이라 같은 총이면 항상 같은 값이 나옵니다.
        /// 그래서 표시 등급을 우선 쓰고, 없을 때만 메타데이터로 폴백합니다.
        /// </summary>
        public static int GetQuality(Item? item)
        {
            if (item == null)
                return 0;

            int display = GetDisplayQuality(item);
            if (display > 0)
                return display;

            return GetMetaQuality(item);
        }

        /// <summary>인스턴스 표시 등급 (게임 UI가 색으로 보여주는 값). 없으면 0.</summary>
        public static int GetDisplayQuality(Item? item)
        {
            if (item == null)
                return 0;

            try
            {
                return (int)item.DisplayQuality;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>프리팹 기본 등급 (메타데이터). 개체와 무관하게 고정입니다.</summary>
        public static int GetMetaQuality(Item? item) => GetMetaQuality(TypeIdOf(item));

        /// <summary>프리팹 기본 등급 (메타데이터, TypeID 기준)</summary>
        public static int GetMetaQuality(int typeId)
        {
            if (typeId <= 0)
                return 0;

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                return meta.id <= 0 ? 0 : meta.quality;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>표시용 아이템 이름</summary>
        public static string GetDisplayName(Item? item)
        {
            if (item == null)
                return "-";

            try
            {
                string name = item.DisplayName ?? "";
                return string.IsNullOrEmpty(name) ? item.name : name;
            }
            catch
            {
                return "-";
            }
        }

        /// <summary>표시용 이름 (TypeID 기준 — 손에 없는 무기도 이름을 알 수 있습니다)</summary>
        public static string GetDisplayName(int typeId)
        {
            if (typeId <= 0)
                return "-";

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                if (meta.id <= 0)
                    return "-";

                string name = meta.DisplayName ?? "";
                if (!string.IsNullOrEmpty(name))
                    return name;

                // meta.name은 비공개 필드입니다 — 공개 프로퍼티 Name을 씁니다.
                string raw = meta.Name ?? "";
                return string.IsNullOrEmpty(raw) ? $"#{typeId}" : raw;
            }
            catch
            {
                return $"#{typeId}";
            }
        }

        private static int TypeIdOf(Item? item)
        {
            if (item == null)
                return 0;

            try
            {
                return item.TypeID;
            }
            catch
            {
                return 0;
            }
        }

        private static bool HasAnyTag(int typeId, string[] names)
        {
            if (typeId <= 0)
                return false;

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(typeId);
                if (meta.id <= 0 || meta.tags == null)
                    return false;

                foreach (var tag in meta.tags)
                {
                    if (tag?.name != null && Array.IndexOf(names, tag.name) >= 0)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
