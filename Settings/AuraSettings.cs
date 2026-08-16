using System;
using System.IO;
using System.Reflection;
using Duckov.Options;
using UnityEngine;

namespace WeaponAura.Settings
{
    /// <summary>무기 오라 표시 세기</summary>
    public enum WeaponAuraMode
    {
        Disabled = 0,
        Subtle = 1,
        Normal = 2,
        Intense = 3,
    }

    /// <summary>
    /// 오라 티어를 무엇으로 결정할지
    /// </summary>
    public enum AuraTierSource
    {
        /// <summary>아이템 등급(quality)</summary>
        Quality = 0,
        /// <summary>무기 종류 (근접 / 권총·SMG / 소총 / 중화기)</summary>
        WeaponClass = 1,
        /// <summary>항상 최고 티어 (연출 확인·스크린샷용)</summary>
        Fixed = 2,
    }

    /// <summary>
    /// 모드 설정. OptionsManager에 저장하고 게임 설정 화면 대신
    /// 튜닝 패널(Ctrl+Shift+F8)에서 직접 조절합니다.
    /// </summary>
    public static class AuraSettings
    {
        private const string KeyMode = "WeaponAura_Mode";
        private const string KeyTierSource = "WeaponAura_TierSource";
        private const string KeyFixedTier = "WeaponAura_FixedTier";

        /// <summary>표시 세기 (기본: 보통)</summary>
        public static WeaponAuraMode AuraMode = WeaponAuraMode.Normal;

        /// <summary>티어 결정 방식 (기본: 아이템 등급)</summary>
        public static AuraTierSource TierSource = AuraTierSource.Quality;

        /// <summary>TierSource가 Fixed일 때 사용할 티어 (세션 한정)</summary>
        public static int FixedTier = DefaultFixedTier;

        private const int DefaultFixedTier = 3;

        public static event Action? OnChanged;

        private static string? _cachedModRoot;

        /// <summary>
        /// 이번 세션에서 사용자가 세기를 직접 바꿨는지.
        ///
        /// Load()는 씬이 바뀔 때마다 Awake에서 다시 불립니다. 그때 저장 파일을 다시 읽어
        /// AuraMode를 덮어쓰면, 방금 창에서 끈 상태가 조용히 되돌아가고 창이 보여주는 상태와
        /// 실제 값이 어긋납니다("끄기를 눌러도 계속 켜져 있음"). 한 번이라도 직접 바꿨으면
        /// 그 값이 이번 세션의 정답이므로 다시 읽지 않습니다.
        /// </summary>
        private static bool _modeSetByUser;

        public static void Load()
        {
            try
            {
                // 표시 세기만 저장/복원합니다 — 이건 실제 사용자 설정입니다.
                if (!_modeSetByUser)
                {
                    // 전체 끄기는 없앴습니다. 오라를 원치 않는 등급은 티어별로 끕니다
                    // (WeaponAuraProfile.enabled). 그래서 Subtle 밑으로는 내려가지 않습니다 —
                    // 예전에 Disabled로 저장해 둔 사람이 오라가 영영 안 보이면 안 됩니다.
                    AuraMode = (WeaponAuraMode)Mathf.Clamp(
                        OptionsManager.Load(KeyMode, (int)WeaponAuraMode.Normal),
                        (int)WeaponAuraMode.Subtle, (int)WeaponAuraMode.Intense);
                }

                // 티어 기준·고정 티어는 디버그 패널에서 여러 등급을 비교해 보기 위한 수단입니다.
                // 이걸 저장하면 실험용으로 잠깐 바꾼 값이 다음 플레이까지 남아서
                // "무기 등급이 무시되는" 상태가 조용히 이어집니다(실제로 그런 일이 있었습니다).
                // 그래서 항상 기본값(아이템 등급)으로 시작하고, 세션 안에서만 바꿀 수 있게 합니다.
                TierSource = AuraTierSource.Quality;
                FixedTier = DefaultFixedTier;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 설정 로드 실패: {ex.Message}");
            }
        }

        /// <summary>기본값이 아닌 디버그 오버라이드가 걸려 있는지 (패널 경고용)</summary>
        public static bool HasTierOverride => TierSource != AuraTierSource.Quality;

        public static void SetMode(WeaponAuraMode value)
        {
            _modeSetByUser = true;

            if (AuraMode == value)
                return;

            UnityEngine.Debug.Log($"[WeaponAura] 표시 세기 변경: {AuraMode} → {value}");

            AuraMode = value;
            Save(KeyMode, (int)value);
            OnChanged?.Invoke();
        }

        /// <summary>세션 한정 — 저장하지 않습니다 (디버그 미리보기용).</summary>
        public static void SetTierSource(AuraTierSource value)
        {
            if (TierSource == value)
                return;

            TierSource = value;
            OnChanged?.Invoke();
        }

        public static void SetFixedTier(int value)
        {
            value = Mathf.Clamp(value, 0, 11);
            if (FixedTier == value)
                return;

            FixedTier = value;
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
                UnityEngine.Debug.LogWarning($"[WeaponAura] 설정 저장 실패({key}): {ex.Message}");
            }
        }

        /// <summary>
        /// 모드 루트 경로. 어셈블리 위치에서 assets 폴더를 가진 상위 디렉터리를 찾습니다.
        /// </summary>
        public static string? GetModRootPath()
        {
            if (_cachedModRoot != null)
                return _cachedModRoot.Length == 0 ? null : _cachedModRoot;

            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    var dir = new DirectoryInfo(Path.GetDirectoryName(assemblyPath) ?? "");
                    while (dir != null)
                    {
                        if (Directory.Exists(Path.Combine(dir.FullName, "assets")))
                        {
                            _cachedModRoot = dir.FullName;
                            return _cachedModRoot;
                        }
                        dir = dir.Parent;
                    }
                }

                string dataPath = Application.dataPath;
                string? gamePath = Path.GetDirectoryName(dataPath);
                string[] candidates =
                {
                    Path.Combine(Path.Combine(dataPath, "Mods"), "WeaponAura"),
                    Path.Combine(Path.Combine(gamePath ?? "", "Mods"), "WeaponAura"),
                };

                foreach (string candidate in candidates)
                {
                    if (Directory.Exists(candidate))
                    {
                        _cachedModRoot = candidate;
                        return _cachedModRoot;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 모드 경로 탐색 실패: {ex.Message}");
            }

            _cachedModRoot = "";
            return null;
        }
    }
}
