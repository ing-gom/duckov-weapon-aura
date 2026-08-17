using System;
using Duckov.Options;

namespace WeaponAura.Settings
{
    /// <summary>근접 참격 이펙트를 어떤 방식으로 커스터마이즈할지</summary>
    public enum MeleeSlashMode
    {
        /// <summary>게임이 내는 참격의 형태는 그대로 두고 색·크기만 바꿉니다.</summary>
        TintDefault = 0,

        /// <summary>게임 참격을 지우고 우리 이펙트로 대체합니다.</summary>
        Replace = 1,

        /// <summary>게임 참격에 색을 입히고 그 위에 흩뿌림 이펙트를 더합니다.</summary>
        Overlay = 2,
    }

    /// <summary>
    /// 근접 참격 전역 설정.
    ///
    /// 등급별 색·모양은 <see cref="Systems.MeleeSlashProfiles"/>가 JSON으로 들고 있고,
    /// 여기에는 켤지·누구에게·게임 기본 참격을 어떻게 할지만 둡니다. 셋 다 즉시 저장됩니다.
    /// </summary>
    public static class MeleeSlashSettings
    {
        private const string KeyEnabled = "WeaponAura_MeleeEnabled";
        private const string KeyScope = "WeaponAura_MeleeScope";
        private const string KeyMode = "WeaponAura_MeleeMode";

        /// <summary>근접 참격을 손댈지 (기본: 켜짐)</summary>
        public static bool Enabled = true;

        /// <summary>적용 대상 (기본: 내 무기만)</summary>
        public static EffectScope Scope = EffectScope.PlayerOnly;

        /// <summary>
        /// 참격을 어떻게 손볼지 (기본: 색 + 흩뿌림).
        ///
        /// 총구 화염은 "색만 바꾸기"가 기본이지만 여기는 다릅니다. 게임 참격은 흰색 판
        /// 하나라서 색을 입히는 것만으로도 충분히 읽히고, 휘두를 때 흩날리는 알갱이가
        /// 근접 무기 쪽에서 가장 눈에 띄는 부분입니다. 둘 다 기본으로 켜 둡니다.
        /// </summary>
        public static MeleeSlashMode Mode = MeleeSlashMode.Overlay;

        public static event Action? OnChanged;

        /// <summary>
        /// 이번 세션에서 사용자가 직접 바꿨는지. 씬이 바뀔 때마다 다시 읽어 덮어쓰면
        /// 방금 창에서 바꾼 값이 조용히 되돌아갑니다.
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
                int mode = OptionsManager.Load(KeyMode, (int)MeleeSlashMode.Overlay);
                Mode = mode == (int)MeleeSlashMode.Replace ? MeleeSlashMode.Replace
                     : mode == (int)MeleeSlashMode.TintDefault ? MeleeSlashMode.TintDefault
                     : MeleeSlashMode.Overlay;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 근접 참격 설정 로드 실패: {ex.Message}");
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

        public static void SetScope(EffectScope value)
        {
            _setByUser = true;

            if (Scope == value)
                return;

            Scope = value;
            Save(KeyScope, (int)value);
            OnChanged?.Invoke();
        }

        public static void SetMode(MeleeSlashMode value)
        {
            _setByUser = true;

            if (Mode == value)
                return;

            Mode = value;
            Save(KeyMode, (int)value);
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
                UnityEngine.Debug.LogWarning($"[WeaponAura] 근접 참격 설정 저장 실패({key}): {ex.Message}");
            }
        }
    }
}
