using System;
using ItemStatsSystem;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// 무기 판별 및 등급 조회. ItemAssetsCollection의 메타데이터 태그를 봅니다.
    ///
    /// 부착물(머즐·소음기·스톡 등)은 GunType 태그만 갖고 Tag_Gun이 없기 때문에,
    /// 총기로 인정하려면 "GunType 계열 + Gun 계열"이 둘 다 있어야 합니다.
    /// </summary>
    public static class WeaponHelper
    {
        /// <summary>런타임에 따라 "Tag_Gun" 또는 "Gun"으로 올 수 있습니다.</summary>
        private static readonly string[] GunTags = { "Tag_Gun", "Gun" };

        private static readonly string[] GunTypeTags =
        {
            "GunType_PST",    "Tag_GunType_PST",     // 권총
            "GunType_SMG",    "Tag_GunType_SMG",     // 기관단총
            "GunType_AR",     "Tag_GunType_AR",      // 돌격소총
            "GunType_SR",     "Tag_GunType_SR",      // 저격총
            "GunType_SNP",    "Tag_GunType_SNP",     // 스나이퍼
            "GunType_SG",     "Tag_GunType_SG",      // 산탄총
            "GunType_SHT",    "Tag_GunType_SHT",     // 샷건
            "GunType_LMG",    "Tag_GunType_LMG",     // 경기관총
            "GunType_DMR",    "Tag_GunType_DMR",     // 지정사수소총
            "GunType_BR",     "Tag_GunType_BR",      // 전투소총
            "GunType_PWS",    "Tag_GunType_PWS",     // 소형 에너지건
            "GunType_ARR",    "Tag_GunType_ARR",     // 활
            "GunType_MAG",    "Tag_GunType_MAG",     // 매그넘
            "GunType_Rocket", "Tag_GunType_Rocket",  // 로켓 발사기
        };

        private static readonly string[] MeleeTags = { "MeleeWeapon", "Tag_MeleeWeapon" };
        private static readonly string[] RocketTags = { "GunType_Rocket", "Tag_GunType_Rocket" };

        /// <summary>총기 또는 근접무기</summary>
        public static bool IsWeapon(Item? item)
        {
            return IsGun(item) || IsMeleeWeapon(item);
        }

        public static bool IsGun(Item? item)
        {
            if (item == null)
                return false;

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(item.TypeID);
                if (meta.id <= 0 || meta.tags == null)
                    return false;

                bool hasGunType = false;
                bool hasGunTag = false;

                foreach (var tag in meta.tags)
                {
                    if (tag == null)
                        continue;
                    if (Array.IndexOf(GunTypeTags, tag.name) >= 0)
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

        public static bool IsMeleeWeapon(Item? item)
        {
            return HasAnyTag(item, MeleeTags);
        }

        public static bool IsRocketWeapon(Item? item)
        {
            return HasAnyTag(item, RocketTags);
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
        public static int GetMetaQuality(Item? item)
        {
            if (item == null)
                return 0;

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(item.TypeID);
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

        private static bool HasAnyTag(Item? item, string[] names)
        {
            if (item == null)
                return false;

            try
            {
                var meta = ItemAssetsCollection.GetMetaData(item.TypeID);
                if (meta.id <= 0 || meta.tags == null)
                    return false;

                foreach (var tag in meta.tags)
                {
                    if (tag != null && Array.IndexOf(names, tag.name) >= 0)
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
