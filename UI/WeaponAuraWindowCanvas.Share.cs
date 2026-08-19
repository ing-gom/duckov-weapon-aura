using System;
using System.Text;
using UnityEngine;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 등급 설정을 문자열 한 줄로 주고받습니다.
    ///
    /// 프로필은 이미 JSON으로 저장되고 있어서, 그것을 Base64로 접으면 채팅·디스코드에
    /// 그대로 붙여넣을 수 있는 한 줄이 됩니다. 시드 기반 랜덤이 "마음에 드는 조합을
    /// 다시 만드는" 길이라면, 이쪽은 "남에게 건네는" 길입니다.
    ///
    /// 앞에 표식을 붙여 둡니다. 엉뚱한 문자열을 붙여넣었을 때 조용히 망가지는 대신
    /// 안내를 띄울 수 있고, 나중에 형식이 바뀌면 번호로 갈라낼 수 있습니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        /// <summary>예전 형식. 지금은 <see cref="ShareCodec"/>가 읽고 씁니다.</summary>
        private const string SharePrefix = ShareCodec.LegacyPrefix;

        private void CopyProfileToClipboard()
        {
            var profile = CurrentProfile();
            if (profile == null)
                return;

            try
            {
                string json = JsonUtility.ToJson(profile);
                string code = SharePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                GUIUtility.systemCopyBuffer = code;
                ShowHint(string.Format(L.Share.Copied, profile.minLevel, code.Length));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 설정 복사 실패: {ex.Message}");
                ShowHint(L.Share.Failed);
            }
        }

        /// <summary>
        /// 코드 하나를 지금 편집 중인 프로필에 적용합니다.
        ///
        /// 예전에는 클립보드를 바로 읽어 그 자리에서 덮어썼습니다. 무엇이 들었는지 모르는
        /// 채로 적용되니, 엉뚱한 것을 복사해 둔 상태에서 누르면 편집 중이던 설정이
        /// 날아갔습니다. 지금은 붙여넣기 창이 코드를 보여 준 뒤 이 함수를 부릅니다.
        /// </summary>
        /// <returns>적용했으면 true. 실패하면 안내만 띄우고 false.</returns>
        private bool ApplyShareCode(string? raw)
        {
            var target = CurrentProfile();
            if (target == null)
                return false;

            string code = (raw ?? "").Trim();

            if (!code.StartsWith(SharePrefix, StringComparison.Ordinal))
            {
                ShowHint(L.Share.NotFound);
                return false;
            }

            try
            {
                string json = Encoding.UTF8.GetString(
                    Convert.FromBase64String(code.Substring(SharePrefix.Length)));

                var incoming = JsonUtility.FromJson<WeaponAuraProfile>(json);
                if (incoming == null)
                {
                    ShowHint(L.Share.Failed);
                    return false;
                }

                // 등급 기준값·이름·추가 여부는 이 티어의 정체성입니다. 남의 설정을 받아도
                // "몇 등급 자리인지"까지 덮어쓰면 티어 목록이 뒤엉킵니다.
                int minLevel = target.minLevel;
                string name = target.name;
                bool custom = target.custom;

                target.CopyFrom(incoming);

                target.minLevel = minLevel;
                target.name = name;
                target.custom = custom;

                SyncFromProfile();
                ApplyEdit(true);

                ShowHint(string.Format(L.Share.Pasted, target.minLevel));
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 설정 붙여넣기 실패: {ex.Message}");
                ShowHint(L.Share.Failed);
                return false;
            }
        }
    }
}
