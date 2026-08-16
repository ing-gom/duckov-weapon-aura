using System;
using Ducky.Sdk.ModBehaviours;
using UnityEngine;
using WeaponAura.Patches;
using WeaponAura.Settings;
using WeaponAura.Systems;
using WeaponAura.UI;

namespace WeaponAura
{
    /// <summary>
    /// WeaponAura 모드 진입점.
    ///
    /// 들고 있는 무기 주위에 등급별 오라를 그립니다. 다른 모드에 의존하지 않고,
    /// 아이템 등급 또는 무기 종류로 티어를 정합니다.
    /// </summary>
    public class ModBehaviour : ModBehaviourBase
    {
        private void Awake()
        {
            // 씬이 바뀔 때마다 다시 로드되도록 Awake에서 읽습니다.
            AuraSettings.Load();
        }

        protected override void ModEnabled()
        {
            try
            {
                AuraSettings.Load();

                // 씬이 바뀌면 ModEnabled가 다시 불릴 수 있습니다. 그냥 += 하면 구독이 쌓여서
                // 설정 한 번 바꿀 때마다 오라를 여러 번 다시 만듭니다. 먼저 떼고 붙입니다.
                AuraSettings.OnChanged -= OnSettingsChanged;
                AuraSettings.OnChanged += OnSettingsChanged;

                // 저장해 둔 색 설정이 있으면 자동으로 복원합니다.
                WeaponAuraProfiles.AutoLoad();

                // 튜닝 패널이 열려 있는 동안 발사 입력 차단
                PlayerInputBlockPatch.ApplyPatches();

                // 일시정지 메뉴에 "무기 오라" 버튼 추가
                PauseMenuButton.Install();

#if DEBUG
                UnityEngine.Debug.Log("[WeaponAura] 모드 활성화 — Ctrl+Shift+F8로 튜닝 패널");
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WeaponAura] ModEnabled 오류: {ex}");
            }
        }

        protected override void ModDisabled()
        {
            try
            {
                AuraSettings.OnChanged -= OnSettingsChanged;
                WeaponAuraSystem.Clear();
                PlayerInputBlockPatch.RemovePatches();
                PauseMenuButton.Uninstall();
#if DEBUG
                WeaponAuraDebugWindow.Instance.Dispose();
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WeaponAura] ModDisabled 오류: {ex}");
            }
        }

        private void Update()
        {
            try
            {
                // 무기 오라 갱신 (내부에서 0.25초 간격으로 스로틀링)
                WeaponAuraSystem.Tick();

                // 일시정지 메뉴가 떠 있으면 버튼이 붙어 있는지 확인 (0.5초 간격)
                PauseMenuButton.Tick();

#if DEBUG
                HandlePanelHotkey();
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] Update 오류(무시됨): {ex.Message}");
#endif
            }
        }

#if DEBUG
        private void OnGUI()
        {
            try
            {
                // 닫혀 있으면 즉시 반환하므로 평소 비용은 없습니다.
                WeaponAuraDebugWindow.Instance.OnGUI();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] OnGUI 오류(무시됨): {ex.Message}");
            }
        }

        /// <summary>
        /// 진단 패널 단축키(F8).
        ///
        /// 배포 빌드에는 넣지 않습니다. 플레이어가 쓰는 기능은 일시정지 메뉴의 설정 창이
        /// 전부 담고 있고, IMGUI 패널은 수치를 날것으로 노출해서 실수로 열면 혼란만 줍니다.
        /// </summary>
        private void HandlePanelHotkey()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                WeaponAuraDebugWindow.Instance.Toggle();
        }
#endif

        private void OnSettingsChanged()
        {
            WeaponAuraSystem.RebuildNow();
        }
    }
}
