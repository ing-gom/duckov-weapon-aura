using System;
using HarmonyLib;
using ItemStatsSystem;
using WeaponAura.Helpers;
using WeaponAura.Settings;
using WeaponAura.Systems;

namespace WeaponAura.Patches
{
    /// <summary>
    /// 발사되는 총알에 장착 탄환 등급을 붙여 잔상을 만듭니다.
    ///
    /// <see cref="Projectile"/>은 자기가 어떤 탄약으로 만들어졌는지 모릅니다
    /// (<c>ProjectileContext</c>에는 무기 ID만 들어 있습니다). 그래서 총알을 만드는
    /// <c>ItemAgent_Gun.ShootOneBullet</c>에서 탄약 등급을 먼저 기록해 두고,
    /// 그 안에서 호출되는 <c>Projectile.Init</c> 직후에 꺼내 씁니다.
    ///
    /// 기록은 한 번 쓰면 바로 지웁니다 — 수류탄처럼 총이 아닌 곳에서 만들어진 투사체가
    /// 남은 값을 주워 쓰면 엉뚱한 색이 붙습니다.
    /// </summary>
    public static class BulletTrailPatch
    {
        private const string HarmonyId = "WeaponAura.BulletTrail";

        private static Harmony? _harmony;
        private static bool _applied;

        private static bool _pendingValid;
        private static int _pendingQuality;

        public static void ApplyPatches()
        {
            if (_applied)
                return;

            try
            {
                var harmony = new Harmony(HarmonyId);

                var shoot = AccessTools.Method(typeof(ItemAgent_Gun), "ShootOneBullet");
                var init = AccessTools.Method(typeof(Projectile), "Init", new[] { typeof(ProjectileContext) });

                if (shoot == null || init == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[WeaponAura] 발사 지점을 찾지 못해 탄환 잔상을 건너뜁니다 " +
                        $"(ShootOneBullet={shoot != null}, Projectile.Init={init != null}).");
                    return;
                }

                harmony.Patch(shoot,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(BulletTrailPatch), nameof(ShootPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(BulletTrailPatch), nameof(ShootPostfix))));

                harmony.Patch(init,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(BulletTrailPatch), nameof(InitPostfix))));

                _harmony = harmony;
                _applied = true;

#if DEBUG
                UnityEngine.Debug.Log("[WeaponAura] 탄환 잔상 패치 적용 완료");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WeaponAura] BulletTrailPatch.ApplyPatches 오류: {ex.Message}");
            }
        }

        public static void RemovePatches()
        {
            if (!_applied)
                return;

            try
            {
                var harmony = _harmony ?? new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] BulletTrailPatch.RemovePatches 오류: {ex.Message}");
            }
            finally
            {
                _harmony = null;
                _applied = false;
                _pendingValid = false;
            }
        }

        /// <summary>발사 직전 — 이 총에 들어 있는 탄약의 등급을 기록해 둡니다.</summary>
        private static void ShootPrefix(ItemAgent_Gun __instance)
        {
            _pendingValid = false;

            try
            {
                if (!BulletTrailSettings.Enabled || __instance == null)
                    return;

                var holder = __instance.Holder;
                bool fromPlayer = holder != null && holder.IsMainCharacter;

                if (BulletTrailSettings.Scope == EffectScope.PlayerOnly && !fromPlayer)
                    return;

                Item? ammo = __instance.BulletItem;
                if (ammo == null)
                    return;

                _pendingQuality = WeaponHelper.GetQuality(ammo);
                _pendingValid = true;
            }
            catch
            {
                _pendingValid = false;
            }
        }

        /// <summary>
        /// 발사가 끝나면 기록을 지웁니다.
        /// (총알 생성이 중간에 취소돼도 다음 투사체가 이 값을 물려받지 않도록)
        /// </summary>
        private static void ShootPostfix()
        {
            _pendingValid = false;
        }

        /// <summary>총알이 초기화된 직후 — 기록해 둔 등급으로 잔상을 붙입니다.</summary>
        private static void InitPostfix(Projectile __instance)
        {
            if (!_pendingValid)
                return;

            _pendingValid = false;

            try
            {
                BulletTrailSystem.Apply(__instance, _pendingQuality);
            }
            catch
            {
                // 총알 생성 경로에서 예외가 나면 발사 자체가 끊깁니다. 잔상은 포기합니다.
            }
        }
    }
}
