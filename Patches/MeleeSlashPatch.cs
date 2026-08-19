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
    /// 근접무기를 휘두를 때 나가는 참격에 등급 색을 입히고 흩뿌림을 더합니다.
    ///
    /// 후킹 지점은 <c>CA_Attack.OnUpdateAction</c>입니다. 게임은 공격을 시작한 뒤
    /// <c>slashFxDelayTime</c>이 지난 프레임에 <c>ItemAgent_MeleeWeapon.slashFx</c>를 하나 만들어
    /// 캐릭터의 자식으로 붙입니다(휘두르는 동작 중간에 나가야 해서 시작 시점이 아닙니다).
    /// 그래서 <see cref="MuzzleFlashPatch"/>와 같은 방법을 씁니다 — 호출 전 자식 수를 세어 두고,
    /// 호출 뒤에 늘어난 만큼이 방금 만들어진 참격입니다.
    ///
    /// 참격 프리팹이 없는 무기(맨손 등)에는 아무 일도 일어나지 않습니다. 그 경우 게임도
    /// 아무것도 만들지 않기 때문에, 위치와 방향을 알려 줄 기준이 없습니다.
    /// </summary>
    public static class MeleeSlashPatch
    {
        private const string HarmonyId = "WeaponAura.MeleeSlash";

        private static Harmony? _harmony;
        private static bool _applied;

        /// <summary>이번 호출을 지켜볼지 (설정·대상 판정을 미리 끝내 둡니다)</summary>
        private static bool _watching;

        /// <summary>호출 전 캐릭터의 자식 수 — 이 참격으로 새로 생긴 것을 골라내는 기준</summary>
        private static int _childCount;

        /// <summary>지울·물들일 자식을 담아 둘 임시 목록 (매 참격 할당하지 않도록 재사용)</summary>
        private static readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>
        /// 마지막으로 휘두른 무기의 참격 프리팹. 설정 창 미리보기가 이걸 그대로 세워 놓고 찍습니다.
        /// 무기마다 다른 물건이라, 한 번도 휘두르기 전에는 무엇을 보여 줘야 할지 알 수 없습니다.
        /// </summary>
        public static GameObject? LastSlashPrefab { get; private set; }

        public static void ApplyPatches()
        {
            if (_applied)
                return;

            try
            {
                var harmony = new Harmony(HarmonyId);

                var update = AccessTools.Method(typeof(CA_Attack), "OnUpdateAction",
                    new[] { typeof(float) });

                if (update == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[WeaponAura] CA_Attack.OnUpdateAction을 찾지 못해 근접 참격을 건너뜁니다.");
                    return;
                }

                harmony.Patch(update,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(MeleeSlashPatch), nameof(AttackPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(MeleeSlashPatch), nameof(AttackPostfix))));

                _harmony = harmony;
                _applied = true;

#if DEBUG
                UnityEngine.Debug.Log("[WeaponAura] 근접 참격 패치 적용 완료");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WeaponAura] MeleeSlashPatch.ApplyPatches 오류: {ex.Message}");
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
                UnityEngine.Debug.LogWarning($"[WeaponAura] MeleeSlashPatch.RemovePatches 오류: {ex.Message}");
            }
            finally
            {
                _harmony = null;
                _applied = false;
                _watching = false;
            }
        }

        /// <summary>
        /// 휘두르는 중 매 프레임. 판정을 여기서 끝내 두는 이유는 값이 하나라도 어긋나면
        /// 뒤에서 자식 수를 비교할 필요조차 없기 때문입니다 (공격은 매 프레임 돕니다).
        /// </summary>
        private static void AttackPrefix(CA_Attack __instance)
        {
            _watching = false;

            try
            {
                if (!MeleeSlashSettings.Enabled || __instance == null)
                    return;

                var character = __instance.characterController;
                if (character == null)
                    return;

                if (MeleeSlashSettings.Scope == EffectScope.PlayerOnly && !character.IsMainCharacter)
                    return;

                _childCount = __instance.transform.childCount;
                _watching = true;
            }
            catch
            {
                _watching = false;
            }
        }

        private static void AttackPostfix(CA_Attack __instance)
        {
            if (!_watching)
                return;

            _watching = false;

            try
            {
                if (__instance == null)
                    return;

                var root = __instance.transform;
                if (root == null || root.childCount <= _childCount)
                    return;

                var character = __instance.characterController;
                var melee = character != null ? character.GetMeleeWeapon() : null;
                if (melee == null)
                    return;

                RememberPrefab(melee);

                Item? item = melee.Item;
                int quality = WeaponHelper.GetQuality(item);

                // 이 근접무기에 전용 설정이 있으면 등급보다 우선합니다.
                var profile = MeleeSlashProfiles.Resolve(quality, item != null ? item.TypeID : 0);
                if (profile == null || !profile.enabled)
                    return;

                CollectSpawned(root, _childCount);
                if (_spawned.Count == 0)
                    return;

                // 방식과 무관하게 첫 참격에서 한 번. 색이 안 바뀌는 원인은 거의 항상
                // "그 참격에는 우리가 찾는 색 속성이 없다"인데, 로그가 없으면 그걸 알 길이 없습니다.
                MeleeSlashSystem.LogFirstSlash(_spawned[0], profile);

                // 참격이 놓인 자리와 방향. 참격을 지우는 모드에서만 씁니다 —
                // 남겨 두는 모드에서는 참격 자체를 넘겨서 그 궤적 위에 알갱이를 붙입니다.
                var origin = _spawned[0].transform;
                Vector3 position = origin.position;
                Quaternion rotation = origin.rotation;

                var mode = MeleeSlashSettings.Mode;

                if (mode == MeleeSlashMode.Replace)
                {
                    // 레이어는 모드와 무관하게 나갑니다. 지우는 모드에서는 얹어 갈 호가
                    // 없으니 그 자리에서 바로 뿜습니다.
                    MeleeSlashSystem.PlayLayers(null, profile, position, rotation);

                    foreach (var go in _spawned)
                        UnityEngine.Object.Destroy(go);

                    _spawned.Clear();

                    // 따라갈 호가 없으니 그 자리에서 부채꼴로 뿌리는 수밖에 없습니다.
                    MeleeSlashSystem.SpawnAt(position, rotation, profile);
                    return;
                }

                // 호가 남는 모드에서는 호 위에서 뿜습니다 — 참격 오브젝트의 트랜스폼은
                // 지름 8m짜리 호의 <b>회전 중심</b>이라, 거기 뿜으면 이펙트만 저 멀리
                // 가운데 뜹니다. 호는 이 프레임에 아직 안 나와 있을 수 있어서
                // 뿜는 시점은 MeleeSlashSystem이 잡습니다.
                MeleeSlashSystem.PlayLayers(_spawned[0], profile, position, rotation);

                foreach (var go in _spawned)
                {
                    // 모양을 먼저 바꿉니다 — 색은 렌더러 머티리얼에 걸리므로 순서가 상관없지만,
                    // 흩뿌림이 얹어 갈 메시는 <b>바뀐 뒤의</b> 것이어야 합니다.
                    MeleeSlashSystem.ApplyShape(go, profile);
                    MeleeSlashSystem.TintExisting(go, profile);
                }

                // 흩뿌림은 게임 참격 <b>위에서</b> 나와야 합니다. 좌표를 우리가 다시 계산하면
                // (캐릭터 위치 + 각도 + 오프셋) 게임이 그리는 호와 반드시 어긋나서,
                // 알갱이만 따로 노는 것처럼 보입니다.
                if (mode != MeleeSlashMode.TintDefault)
                    MeleeSlashSystem.Attach(_spawned[0], profile);

                _spawned.Clear();
            }
            catch
            {
                // 공격 경로에서 예외가 나면 근접 공격 자체가 끊깁니다. 이펙트는 포기합니다.
            }
            finally
            {
                _spawned.Clear();
            }
        }

        private static void RememberPrefab(ItemAgent_MeleeWeapon melee)
        {
            try
            {
                if (melee.slashFx != null)
                    LastSlashPrefab = melee.slashFx;
            }
            catch
            {
                // 미리보기 편의용일 뿐입니다. 못 얻으면 미리보기가 안내 문구를 띄웁니다.
            }
        }

        /// <summary>이 프레임에 캐릭터에 새로 붙은 오브젝트 = 게임이 만든 참격.</summary>
        private static void CollectSpawned(Transform root, int previousChildCount)
        {
            _spawned.Clear();

            if (previousChildCount < 0 || root.childCount <= previousChildCount)
                return;

            for (int i = previousChildCount; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != null)
                    _spawned.Add(child.gameObject);
            }
        }
    }
}
