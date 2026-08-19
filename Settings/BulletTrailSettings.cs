using System;
using Duckov.Options;

namespace WeaponAura.Settings
{
    /// <summary>
    /// 탄환 잔상 전역 설정.
    ///
    /// 등급별 색·굵기는 <see cref="Systems.BulletTrailProfiles"/>가 JSON으로 들고 있고,
    /// 여기에는 "켤지"와 "누구 총알에"만 둡니다. 둘 다 즉시 저장됩니다 —
    /// 저장 버튼을 눌러야 유지되는 값과 아닌 값이 섞이면 헷갈립니다.
    /// </summary>
    public static class BulletTrailSettings
    {
        private const string KeyEnabled = "WeaponAura_TrailEnabled";
        private const string KeyScope = "WeaponAura_TrailScope";
        private const string KeyHideVanilla = "WeaponAura_TrailHideVanilla";
        private const string KeyCustomizeGlow = "WeaponAura_TrailCustomizeGlow";

        /// <summary>탄환 잔상을 그릴지 (기본: 켜짐)</summary>
        public static bool Enabled = true;

        /// <summary>적용 대상 (기본: 내 총알만)</summary>
        public static EffectScope Scope = EffectScope.PlayerOnly;

        /// <summary>
        /// 게임 원본 궤적을 숨기고 모드 잔상만 남길지 (기본: 꺼짐)
        ///
        /// 기본값이 꺼짐인 이유 — 이미 쓰고 있는 사람의 화면이 업데이트만으로
        /// 조용히 바뀌면 안 됩니다.
        /// </summary>
        public static bool HideVanillaTrail;

        /// <summary>
        /// 총알 발광체(게임 원본 빛)를 등급별로 조절할지 (기본: 꺼짐)
        ///
        /// 꺼져 있는 동안에는 발광체를 아예 건드리지 않습니다 — 지금 쓰는 사람들
        /// 화면이 업데이트만으로 달라지면 안 됩니다.
        /// </summary>
        public static bool CustomizeGlow;

        /// <summary>값이 바뀌었을 때 (설정 창 갱신용)</summary>
        public static event Action? OnChanged;

        /// <summary>
        /// 이번 세션에서 사용자가 직접 바꿨는지.
        ///
        /// Load()는 씬이 바뀔 때마다 다시 불립니다. 그때 저장 파일을 다시 읽어 덮어쓰면
        /// 방금 창에서 끈 상태가 조용히 되돌아갑니다(무기 오라 세기에서 실제로 있었던 문제).
        /// </summary>
        private static bool _setByUser;

        public static void Load()
        {
            if (_setByUser)
                return;

            try
            {
                Enabled = OptionsManager.Load(KeyEnabled, 1) != 0;
                Scope = OptionsManager.Load(KeyScope, (int)EffectScope.PlayerOnly) == (int)EffectScope.Everyone
                    ? EffectScope.Everyone
                    : EffectScope.PlayerOnly;
                HideVanillaTrail = OptionsManager.Load(KeyHideVanilla, 0) != 0;
                CustomizeGlow = OptionsManager.Load(KeyCustomizeGlow, 0) != 0;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 탄환 잔상 설정 로드 실패: {ex.Message}");
            }
        }

        public static void SetEnabled(bool value)
        {
            _setByUser = true;

            if (Enabled == value)
                return;

            Enabled = value;
            Save(KeyEnabled, value ? 1 : 0);
            OnChanged?.Invoke();
        }

        public static void SetHideVanillaTrail(bool value)
        {
            _setByUser = true;

            if (HideVanillaTrail == value)
                return;

            HideVanillaTrail = value;
            Save(KeyHideVanilla, value ? 1 : 0);
            OnChanged?.Invoke();
        }

        public static void SetCustomizeGlow(bool value)
        {
            _setByUser = true;

            if (CustomizeGlow == value)
                return;

            CustomizeGlow = value;
            Save(KeyCustomizeGlow, value ? 1 : 0);
            OnChanged?.Invoke();
        }

        public static void SetScope(EffectScope value)
        {
            _setByUser = true;

            if (Scope == value)
                return;

            Scope = value;
            Save(KeyScope, (int)value);
            OnChanged?.Invoke();
        }

        private static void Save(string key, int value)
        {
            try
            {
                OptionsManager.Save(key, value);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 탄환 잔상 설정 저장 실패({key}): {ex.Message}");
            }
        }
    }
}
