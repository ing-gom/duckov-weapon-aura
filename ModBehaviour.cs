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
            // 씬이 바뀌면 BulletPool이 총알 인스턴스를 통째로 새로 만듭니다. 지난 판에
            // 꺼 둔 궤적 기록을 들고 있어 봐야 파괴된 참조만 쌓입니다.
            VanillaTrailSuppressor.RestoreAll();
            BulletGlowController.RestoreAll();

            // 씬이 바뀔 때마다 다시 로드되도록 Awake에서 읽습니다.
            AuraSettings.Load();
            BulletTrailSettings.Load();
            MuzzleFlashSettings.Load();
            MeleeSlashSettings.Load();
                    }

        /// <summary>
        /// 각 단계를 따로 감쌉니다.
        ///
        /// 전체를 try 하나로 묶어 두면 앞쪽에서 예외가 하나 나는 순간 뒤쪽이 통째로 건너뛰어집니다.
        /// 특히 입력 차단 패치를 놓치면 설정 창이 떠도 게임이 멈추지 않습니다
        /// (게임은 일시정지 메뉴가 "보이는" 동안만 시간을 멈추는데, 자식 창이 열리면 메뉴가
        /// 스스로 숨어서 그 패치가 대신 "정지 중"이라고 알려 주는 구조입니다).
        /// 색 설정 파일 하나 못 읽은 것 때문에 그렇게 되면 원인을 찾기가 아주 어렵습니다.
        /// </summary>
        protected override void ModEnabled()
        {
            // 1순위 — 이게 빠지면 설정 창이 게임을 멈추지 못합니다.
            Step("입력 차단 패치", PlayerInputBlockPatch.ApplyPatches);

            // 일시정지 메뉴에 "무기 오라" 버튼 추가
            Step("일시정지 메뉴 버튼", PauseMenuButton.Install);

            Step("설정 로드", () =>
            {
                AuraSettings.Load();
                BulletTrailSettings.Load();
                MuzzleFlashSettings.Load();
                MeleeSlashSettings.Load();
            });

            // 씬이 바뀌면 ModEnabled가 다시 불릴 수 있습니다. 그냥 += 하면 구독이 쌓여서
            // 설정 한 번 바꿀 때마다 오라를 여러 번 다시 만듭니다. 먼저 떼고 붙입니다.
            Step("설정 변경 구독", () =>
            {
                AuraSettings.OnChanged -= OnSettingsChanged;
                AuraSettings.OnChanged += OnSettingsChanged;
                BulletTrailSettings.OnChanged -= OnTrailSettingsChanged;
                BulletTrailSettings.OnChanged += OnTrailSettingsChanged;
                MuzzleFlashSettings.OnChanged -= OnMuzzleSettingsChanged;
                MuzzleFlashSettings.OnChanged += OnMuzzleSettingsChanged;
                MeleeSlashSettings.OnChanged -= OnMeleeSettingsChanged;
                MeleeSlashSettings.OnChanged += OnMeleeSettingsChanged;
                WeaponOverrides.OnChanged -= OnSettingsChanged;
                WeaponOverrides.OnChanged += OnSettingsChanged;
            });

            // 저장해 둔 색 설정이 있으면 자동으로 복원합니다.
            Step("오라 프로필 로드", WeaponAuraProfiles.AutoLoad);
            Step("탄환 잔상 프로필 로드", BulletTrailProfiles.AutoLoad);
            Step("총구 화염 프로필 로드", MuzzleFlashProfiles.AutoLoad);
            Step("근접 참격 프로필 로드", MeleeSlashProfiles.AutoLoad);

            // 무기별·분류별 전용 설정. 등급 프로필 뒤에 읽어야 합니다 —
            // 전용 설정을 만들 때 등급 값을 시작점으로 복사하기 때문입니다.
            Step("전용 설정 로드", WeaponOverrides.AutoLoad);

            // 발사되는 총알에 탄환 등급 색 잔상 붙이기
            Step("탄환 잔상 패치", BulletTrailPatch.ApplyPatches);
            Step("총구 화염 패치", MuzzleFlashPatch.ApplyPatches);
            Step("근접 참격 패치", MeleeSlashPatch.ApplyPatches);

#if DEBUG
            UnityEngine.Debug.Log("[WeaponAura] 모드 활성화 — Ctrl+Shift+F8로 튜닝 패널");
#endif
        }

        /// <summary>한 단계가 실패해도 나머지는 계속 진행합니다.</summary>
        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WeaponAura] {name} 실패(나머지는 계속 진행): {ex}");
            }
        }

        protected override void ModDisabled()
        {
            // 정리도 마찬가지로 하나가 실패했다고 나머지를 남겨 두면 안 됩니다.
            // (특히 패치가 걸린 채로 남으면 다음 실행까지 영향을 줍니다)
            Step("설정 변경 구독 해제", () =>
            {
                AuraSettings.OnChanged -= OnSettingsChanged;
                BulletTrailSettings.OnChanged -= OnTrailSettingsChanged;
                MuzzleFlashSettings.OnChanged -= OnMuzzleSettingsChanged;
                MeleeSlashSettings.OnChanged -= OnMeleeSettingsChanged;
                WeaponOverrides.OnChanged -= OnSettingsChanged;
            });

            Step("오라 정리", WeaponAuraSystem.Clear);
            Step("무기 알갱이 정리", WeaponAuraLayerSystem.Dispose);
            Step("탄환 잔상 패치 해제", BulletTrailPatch.RemovePatches);
            Step("원본 궤적 복원", VanillaTrailSuppressor.RestoreAll);
            Step("발광체 복원", BulletGlowController.RestoreAll);
            Step("탄환 잔상 정리", BulletTrailSystem.Dispose);
            Step("총구 화염 패치 해제", MuzzleFlashPatch.RemovePatches);
            Step("총구 화염 정리", MuzzleFlashSystem.Dispose);
            Step("근접 참격 패치 해제", MeleeSlashPatch.RemovePatches);
            Step("근접 참격 정리", MeleeSlashSystem.Dispose);
            Step("레이어 뿜음 정리", EffectLayerBurst.Clear);
            Step("입력 차단 패치 해제", PlayerInputBlockPatch.RemovePatches);
            Step("일시정지 메뉴 버튼 해제", PauseMenuButton.Uninstall);
#if DEBUG
            Step("진단 창 정리", () => WeaponAuraDebugWindow.Instance.Dispose());
#endif
        }

        private void Update()
        {
            try
            {
                // 무기 오라 갱신 (내부에서 0.25초 간격으로 스로틀링)
                WeaponAuraSystem.Tick();

                // 설정 창에 보여 줄 장착 탄약 정보 (내부에서 0.25초 간격으로 스로틀링)
                BulletTrailSystem.Tick();

                // 설정 창에 보여 줄 근접무기 정보 (같은 간격으로 스로틀링)
                MeleeSlashSystem.Tick();

                // 무기 발광 (같은 간격으로 스로틀링)
                WeaponAuraLayerSystem.Tick();

                // 마지막 발사 뒤에도 다 타버린 뿜음을 걷습니다. Play()에서만
                // 정리하면 총을 내려놓는 순간 빈 오브젝트가 그대로 남습니다.
                EffectLayerBurst.Tick();

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

        /// <summary>
        /// 총알 잔상은 여기서 따라갑니다.
        ///
        /// 총알은 자기 Update에서 움직이고 스크립트 실행 순서는 보장되지 않으므로,
        /// Update에서 위치를 읽으면 프레임마다 꼬리가 총알보다 뒤처졌다 붙었다 합니다.
        /// LateUpdate는 항상 모든 Update 뒤에 돌기 때문에 그 어긋남이 없습니다.
        /// </summary>
        private void LateUpdate()
        {
            try
            {
                BulletTrailSystem.LateTick();
                MuzzleFlashSystem.LateTick();
                MeleeSlashSystem.LateTick();
                WeaponAuraLayerSystem.LateTick();
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] LateUpdate 오류(무시됨): {ex.Message}");
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

            // 전용 설정이 바뀌면 겹도 같이 다시 판단해야 합니다.
            WeaponAuraLayerSystem.RebuildNow();
        }

        /// <summary>알갱이를 끄면 지금 붙어 있는 것도 바로 걷어냅니다.</summary>
        private void OnParticleSettingsChanged()
        {
            WeaponAuraLayerSystem.RebuildNow();
        }

        /// <summary>
        /// 잔상을 끄면 이미 날아가는 총알의 꼬리까지 즉시 걷어냅니다.
        /// (끈 뒤에도 화면에 남아 있으면 꺼진 건지 알 수 없습니다)
        /// </summary>
        private void OnTrailSettingsChanged()
        {
            if (!BulletTrailSettings.Enabled)
                BulletTrailSystem.Clear();

            // 원본 궤적 숨기기를 끄면 곧바로 되돌립니다. 총알은 풀에서 재사용되므로
            // 여기서 안 켜면 그 판이 끝날 때까지 원본 궤적이 돌아오지 않습니다.
            // 잔상 자체를 끈 경우도 마찬가지 — 아무 궤적도 안 보이면 안 됩니다.
            if (!BulletTrailSettings.HideVanillaTrail || !BulletTrailSettings.Enabled)
                VanillaTrailSuppressor.RestoreAll();

            // 발광체도 같은 이유로 즉시 되돌립니다. 총알이 풀에서 재사용되므로
            // 여기서 안 되돌리면 그 판이 끝날 때까지 칠해진 채로 남습니다.
            if (!BulletTrailSettings.CustomizeGlow || !BulletTrailSettings.Enabled)
                BulletGlowController.RestoreAll();
        }

        /// <summary>총구 화염을 끄면 지금 떠 있는 화염도 바로 걷어냅니다.</summary>
        private void OnMuzzleSettingsChanged()
        {
            if (!MuzzleFlashSettings.Enabled)
                MuzzleFlashSystem.Clear();
        }

        /// <summary>근접 참격을 끄면 지금 흩날리는 알갱이도 바로 걷어냅니다.</summary>
        private void OnMeleeSettingsChanged()
        {
            if (!MeleeSlashSettings.Enabled)
            {
                MeleeSlashSystem.Clear();
                EffectLayerBurst.Clear();
            }
        }
    }
}
