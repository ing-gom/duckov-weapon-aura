using System;
using Duckov.Options;

namespace WeaponAura.Settings
{
    /// <summary>총구 화염을 어떤 방식으로 커스터마이즈할지</summary>
    public enum MuzzleFlashMode
    {
        /// <summary>게임이 내는 화염의 형태는 그대로 두고 색·크기만 바꿉니다.</summary>
        TintDefault = 0,

        /// <summary>게임 화염을 지우고 우리 이펙트로 대체합니다.</summary>
        Replace = 1,

        /// <summary>게임 화염을 그대로 두고 그 위에 우리 이펙트를 더합니다.</summary>
        Overlay = 2,
    }

    /// <summary>
    /// 총구 화염 전역 설정.
    ///
    /// 등급별 색·크기는 <see cref="Systems.MuzzleFlashProfiles"/>가 JSON으로 들고 있고,
    /// 여기에는 켤지·누구에게·게임 기본 화염을 어떻게 할지만 둡니다. 셋 다 즉시 저장됩니다.
    /// </summary>
    public static class MuzzleFlashSettings
    {
        private const string KeyEnabled = "WeaponAura_MuzzleEnabled";
        private const string KeyScope = "WeaponAura_MuzzleScope";
        private const string KeyMode = "WeaponAura_MuzzleMode";

        /// <summary>총구 화염을 그릴지 (기본: 켜짐)</summary>
        public static bool Enabled = true;

        /// <summary>적용 대상 (기본: 내 총만)</summary>
        public static EffectScope Scope = EffectScope.PlayerOnly;

        /// <summary>
        /// 총구 화염을 어떻게 손볼지 (기본: 색만 바꾸기).
        ///
        /// 처음에는 우리 화염을 게임 화염 <b>위에 얹기</b>만 했는데, 게임 화염이 그대로 남아
        /// 있는 한 무엇을 바꿔도 그쪽이 화면을 차지해서 "변한 게 없다"로 보였습니다.
        /// 총마다 다르게 잘 만들어 둔 형태를 버릴 이유도 없어서, 기본은 그 형태를 그대로 쓰고
        /// 색과 크기만 등급에 맞게 바꿉니다. 형태까지 갈아엎고 싶을 때만 대체 모드를 씁니다.
        /// </summary>
        public static MuzzleFlashMode Mode = MuzzleFlashMode.TintDefault;

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
                int mode = OptionsManager.Load(KeyMode, (int)MuzzleFlashMode.TintDefault);
                Mode = mode == (int)MuzzleFlashMode.Replace ? MuzzleFlashMode.Replace
                     : mode == (int)MuzzleFlashMode.Overlay ? MuzzleFlashMode.Overlay
                     : MuzzleFlashMode.TintDefault;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 설정 로드 실패: {ex.Message}");
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

        public static void SetMode(MuzzleFlashMode value)
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
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 설정 저장 실패({key}): {ex.Message}");
            }
        }
    }
}
