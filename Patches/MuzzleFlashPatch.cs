using System;
using System.Collections.Generic;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using WeaponAura.Helpers;
using WeaponAura.Settings;
using WeaponAura.Systems;

namespace WeaponAura.Patches
{
    /// <summary>
    /// 발사 순간 총구에 등급 색 화염을 얹습니다.
    ///
    /// 후킹 지점은 <c>ItemAgent_Gun.TransToFire</c>입니다. 탄환 잔상이 쓰는
    /// <c>ShootOneBullet</c>이 아닙니다 — 그쪽은 산탄총에서 <c>ShotCount</c>만큼(8번) 불려서
    /// 화염이 여덟 겹으로 터집니다. 게임이 자기 화염을 루프 <b>바깥</b>에서 한 번만 만드는
    /// 이유가 그것이고, 우리도 같은 자리에 붙습니다.
    /// </summary>
    public static class MuzzleFlashPatch
    {
        private const string HarmonyId = "WeaponAura.MuzzleFlash";

        private static Harmony? _harmony;
        private static bool _applied;

        /// <summary>이번 호출이 실제로 발사로 이어지는지 (게임의 조기 반환 조건과 같은 판정)</summary>
        private static bool _willFire;

        /// <summary>호출 전 총구 자식 수 — 이 발사로 새로 생긴 것을 골라내는 기준</summary>
        private static int _muzzleChildCount;

        /// <summary>지울 자식을 담아 둘 임시 목록 (매 발사 할당하지 않도록 재사용)</summary>
        private static readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>
        /// 마지막으로 쏜 총의 화염 프리팹. 설정 창 미리보기가 이걸 그대로 세워 놓고 찍습니다.
        /// 총마다 다른 물건이라, 한 발도 쏘기 전에는 무엇을 보여 줘야 할지 알 수 없습니다.
        /// </summary>
        public static GameObject? LastMuzzlePrefab { get; private set; }

        public static void ApplyPatches()
        {
            if (_applied)
                return;

            try
            {
                var harmony = new Harmony(HarmonyId);

                var fire = AccessTools.Method(typeof(ItemAgent_Gun), "TransToFire");
                if (fire == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[WeaponAura] ItemAgent_Gun.TransToFire를 찾지 못해 총구 화염을 건너뜁니다.");
                    return;
                }

                harmony.Patch(fire,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(MuzzleFlashPatch), nameof(FirePrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(MuzzleFlashPatch), nameof(FirePostfix))));

                _harmony = harmony;
                _applied = true;

#if DEBUG
                UnityEngine.Debug.Log("[WeaponAura] 총구 화염 패치 적용 완료");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WeaponAura] MuzzleFlashPatch.ApplyPatches 오류: {ex.Message}");
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
                UnityEngine.Debug.LogWarning($"[WeaponAura] MuzzleFlashPatch.RemovePatches 오류: {ex.Message}");
            }
            finally
            {
                _harmony = null;
                _applied = false;
                _willFire = false;
            }
        }

        /// <summary>
        /// 발사 직전. 게임은 탄이 없거나 내구도가 0이면 아무것도 하지 않고 빠져나가므로,
        /// 같은 조건을 미리 재 두고 실제로 쏜 경우에만 화염을 냅니다.
        /// </summary>
        private static void FirePrefix(ItemAgent_Gun __instance)
        {
            _willFire = false;

            try
            {
                if (!MuzzleFlashSettings.Enabled || __instance == null)
                    return;

                if (__instance.BulletCount <= 0 || __instance.Item == null || __instance.Item.Durability <= 0f)
                    return;

                _willFire = true;

                // 두 모드 다 "이 호출 중에 새로 생긴 자식"을 찾아야 합니다.
                // 그게 게임이 방금 만든 총구 화염이고, 색을 입히든 지우든 대상이 그것입니다.
                var muzzle = __instance.muzzle;
                _muzzleChildCount = muzzle != null ? muzzle.childCount : -1;
            }
            catch
            {
                _willFire = false;
            }
        }

        private static void FirePostfix(ItemAgent_Gun __instance)
        {
            if (!_willFire)
                return;

            _willFire = false;

            try
            {
                if (__instance == null)
                    return;

                var muzzle = __instance.muzzle;
                if (muzzle == null)
                    return;

                var holder = __instance.Holder;
                bool fromPlayer = holder != null && holder.IsMainCharacter;

                if (MuzzleFlashSettings.Scope == EffectScope.PlayerOnly && !fromPlayer)
                    return;

                Item? ammo = __instance.BulletItem;
                if (ammo == null)
                    return;

                int quality = WeaponHelper.GetQuality(ammo);

                // 화염은 탄약 등급으로 고르지만, 이 총에 전용 설정이 있으면 그쪽이 이깁니다.
                var gun = __instance.Item;
                int weaponTypeId = gun != null ? gun.TypeID : 0;

                RememberPrefab(__instance);

                var mode = MuzzleFlashSettings.Mode;

                // 대체 모드만 게임 화염을 지웁니다.
                if (mode == MuzzleFlashMode.Replace)
                {
                    RemoveDefaultFlash(muzzle, _muzzleChildCount);
                }
                else
                {
                    // 게임 화염이 남는 모드에서는 그 형태를 그대로 쓰고 색·크기만 바꿉니다.
                    // 함께 표시도 마찬가지입니다 — 내 이펙트만 등급 색이고 게임 화염은
                    // 원래 주황색이면 둘이 따로 노는 것처럼 보입니다.
                    var profile = MuzzleFlashProfiles.Resolve(quality, weaponTypeId);
                    if (profile == null || !profile.enabled)
                        return;

                    CollectSpawned(muzzle, _muzzleChildCount);
                    foreach (var go in _spawned)
                        MuzzleFlashSystem.TintExisting(go, profile);

                    _spawned.Clear();
                }

                // 내 이펙트는 색만 바꾸기를 뺀 나머지 두 모드에서 나갑니다.
                if (mode != MuzzleFlashMode.TintDefault)
                    MuzzleFlashSystem.Spawn(muzzle, quality, weaponTypeId);

                // 레이어는 모드와 무관하게 나갑니다.
                //
                // 본체 화염을 게임 것으로 두고(색만 바꾸기) 레이어만 더 얹는 조합이
                // 실제로 쓸 만합니다 — 원본 화염은 그대로, 그 위에 불티만 추가.
                var layered = MuzzleFlashProfiles.Resolve(quality, weaponTypeId);
                if (layered != null && layered.enabled)
                {
                    EffectLayerBurst.Play(layered.layers, muzzle.position, muzzle.rotation,
                        Vector3.one * Mathf.Max(0.05f, layered.size));
                }
            }
            catch
            {
                // 발사 경로에서 예외가 나면 총이 멈춥니다. 화염은 포기합니다.
            }
        }

        /// <summary>
        /// 이 발사 중에 총구에 새로 붙은 오브젝트를 지웁니다 = 게임이 만든 기본 화염.
        ///
        /// 우리 화염은 총구의 자식이 아니라 따로 떠서 위치만 따라가므로 여기 걸리지 않습니다.
        /// </summary>
        private static void RemoveDefaultFlash(Transform muzzle, int previousChildCount)
        {
            CollectSpawned(muzzle, previousChildCount);

            foreach (var go in _spawned)
                UnityEngine.Object.Destroy(go);

            _spawned.Clear();
        }

        private static void RememberPrefab(ItemAgent_Gun gun)
        {
            try
            {
                var setting = gun.GunItemSetting;
                if (setting != null && setting.muzzleFxPfb != null)
                    LastMuzzlePrefab = setting.muzzleFxPfb;
            }
            catch
            {
                // 미리보기 편의용일 뿐입니다. 못 얻으면 미리보기가 안내 문구를 띄웁니다.
            }
        }

        /// <summary>이 발사 중에 총구에 새로 붙은 오브젝트 = 게임이 만든 기본 화염.</summary>
        private static void CollectSpawned(Transform muzzle, int previousChildCount)
        {
            _spawned.Clear();

            if (previousChildCount < 0 || muzzle.childCount <= previousChildCount)
                return;

            for (int i = previousChildCount; i < muzzle.childCount; i++)
            {
                var child = muzzle.GetChild(i);
                if (child != null)
                    _spawned.Add(child.gameObject);
            }
        }
    }
}
