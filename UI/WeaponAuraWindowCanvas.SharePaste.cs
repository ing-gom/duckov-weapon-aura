using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 설정 주고받기 창.
    ///
    /// 두 가지를 한 자리에 둡니다 — <b>내보내기</b>(이 탭만 / 통째로)와 <b>받기</b>.
    /// 예전에는 오라 탭 안에 복사·붙여넣기 버튼 두 개만 있었고, 담기는 것도 오라 프로필
    /// 하나뿐이었습니다. 무기 하나를 통째로 꾸며 놓고 남에게 주려면 네 이펙트가 다 가야
    /// 하는데 오라만 갔습니다.
    ///
    /// 붙여넣기는 <b>적용 전에 무엇이 들었는지 보여 줍니다.</b> 예전에는 클립보드를 바로
    /// 읽어 그 자리에서 덮어썼습니다 — 엉뚱한 것을 복사해 둔 상태에서 누르면 편집 중이던
    /// 설정이 날아갔고, 되돌릴 방법은 저장 없이 창을 닫는 것뿐이었습니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private GameObject? _shareRoot;
        private TMP_InputField? _shareField;
        private TextMeshProUGUI? _shareStatus;
        private TextMeshProUGUI? _shareHint;

        private bool ShareOpen => _shareRoot != null && _shareRoot.activeSelf;

        // 예전 이름을 쓰던 곳들이 있어 남겨 둡니다.
        private bool PasteOpen => ShareOpen;

        private void OpenShareWindow()
        {
            if (_shareRoot == null)
                BuildShareWindow();

            if (_shareRoot == null)
                return;

            _shareRoot.SetActive(true);

            if (_shareField != null)
                _shareField.text = SafeClipboard();

            RefreshShareStatus();
        }

        private void CloseShareWindow()
        {
            if (_shareRoot != null)
                _shareRoot.SetActive(false);
        }

        // 예전 이름
        private void OpenPasteWindow() => OpenShareWindow();
        private void ClosePasteWindow() => CloseShareWindow();

        /// <summary>
        /// 넣은 글에서 코드만 남깁니다.
        ///
        /// 채팅이나 문서에서 복사하면 줄바꿈·공백이 섞여 들어옵니다. Base64에는 그런 것이
        /// 없으므로 지워도 안전하고, 안 지우면 "올바른 코드인데 안 된다"가 됩니다.
        /// </summary>
        private static string CleanCode(string? raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            var sb = new System.Text.StringBuilder(raw!.Length);

            foreach (char c in raw!)
            {
                if (!char.IsWhiteSpace(c))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static string SafeClipboard()
        {
            try
            {
                return GUIUtility.systemCopyBuffer ?? "";
            }
            catch
            {
                return "";
            }
        }

        private void BuildShareWindow()
        {
            if (_canvasRoot == null)
                return;

            var backdrop = MakeImage("ShareBackdrop", _canvasRoot.transform, new Color(0f, 0f, 0f, 0.82f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _shareRoot = backdrop.gameObject;

            var panel = MakeImage("SharePanel", backdrop.transform, PanelColor);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(820f, 440f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = MakeText("Title", panel.transform, L.Section.Share, 26, TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(title.rectTransform, 34f);

            // ── 내보내기 ──
            var exportLabel = MakeText("ExportLabel", panel.transform, L.Share.ExportLabel, 19,
                ButtonAccentColor * 1.6f, TextAlignmentOptions.MidlineLeft);
            SetHeight(exportLabel.rectTransform, 28f);

            var exportRow = MakeRect("ExportRow", panel.transform);
            SetHeight(exportRow, 42f);

            var exportLayout = exportRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            exportLayout.spacing = 10f;
            exportLayout.childControlWidth = true;
            exportLayout.childControlHeight = true;
            exportLayout.childForceExpandWidth = true;

            MakeButton(exportRow, L.Share.CopyTab, 0f, CopyCurrentTab, ButtonColor);
            MakeButton(exportRow, L.Share.CopyAll, 0f, CopyEverything, ButtonAccentColor);

            _shareHint = MakeText("ExportHint", panel.transform, "", 16, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(_shareHint.rectTransform, 24f);

            // ── 받기 ──
            var importLabel = MakeText("ImportLabel", panel.transform, L.Share.ImportLabel, 19,
                ButtonAccentColor * 1.6f, TextAlignmentOptions.MidlineLeft);
            SetHeight(importLabel.rectTransform, 28f);

            _shareField = MakeCodeField(panel.transform, 96f);
            _shareField.onValueChanged.AddListener(_ => RefreshShareStatus());

            _shareStatus = MakeText("Status", panel.transform, "", 17, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(_shareStatus.rectTransform, 28f);

            var row = MakeRect("Buttons", panel.transform);
            SetHeight(row, 44f);

            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;

            MakeButton(row, L.Share.PasteFromClipboard, 0f, () =>
            {
                if (_shareField != null)
                    _shareField.text = SafeClipboard();

                RefreshShareStatus();
            }, ButtonColor);

            MakeButton(row, L.Share.PasteApply, 0f, ApplyPastedCode, ButtonAccentColor);
            MakeButton(row, L.Confirm.Cancel, 0f, CloseShareWindow, ButtonColor);

            ApplyFont(panel.gameObject);
        }

        // ── 내보내기 ────────────────────────────────────────────────

        /// <summary>지금 보고 있는 탭의 프로필 하나만 담습니다.</summary>
        private void CopyCurrentTab()
        {
            string code;

            switch (_tab)
            {
                case WindowTab.Trail:
                    code = ShareCodec.Encode(ShareKind.Trail, null, CurrentTrailProfile(), null, null);
                    break;

                case WindowTab.Muzzle:
                    code = ShareCodec.Encode(ShareKind.Muzzle, null, null, CurrentMuzzleProfile(), null);
                    break;

                case WindowTab.Melee:
                    code = ShareCodec.Encode(ShareKind.Melee, null, null, null, CurrentMeleeProfile());
                    break;

                default:
                    code = ShareCodec.Encode(ShareKind.Aura, CurrentProfile(), null, null, null);
                    break;
            }

            PutOnClipboard(code, L.Share.CopiedTab);
        }

        /// <summary>네 이펙트를 통째로 담습니다. 오라의 겹도 함께 갑니다.</summary>
        private void CopyEverything()
        {
            string code = ShareCodec.Encode(ShareKind.All,
                CurrentProfile(), CurrentTrailProfile(), CurrentMuzzleProfile(), CurrentMeleeProfile());

            PutOnClipboard(code, L.Share.CopiedAll);
        }

        private void PutOnClipboard(string code, string message)
        {
            try
            {
                GUIUtility.systemCopyBuffer = code;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 클립보드 복사 실패: {ex.Message}");
                ShowHint(L.Share.Failed);
                return;
            }

            if (_shareHint != null)
                _shareHint.text = string.Format(message, code.Length);

            ShowHint(string.Format(message, code.Length));
        }

        // ── 받기 ────────────────────────────────────────────────────

        /// <summary>
        /// 넣은 코드에 무엇이 들었는지 미리 알려 줍니다.
        ///
        /// 적용을 눌러 봐야 아는 것과 누르기 전에 아는 것은 다릅니다 — 이 동작은 지금
        /// 편집 중인 설정을 덮어씁니다.
        /// </summary>
        private void RefreshShareStatus()
        {
            if (_shareStatus == null)
                return;

            string code = CleanCode(_shareField != null ? _shareField.text : "");

            if (code.Length == 0)
            {
                _shareStatus.text = "";
                return;
            }

            if (!ShareCodec.LooksLikeCode(code))
            {
                _shareStatus.text = L.Share.NotFound;
                _shareStatus.color = WarnTextColor;
                return;
            }

            var content = ShareCodec.Decode(code);
            if (content == null)
            {
                _shareStatus.text = L.Share.Failed;
                _shareStatus.color = WarnTextColor;
                return;
            }

            _shareStatus.color = DimTextColor;
            _shareStatus.text = string.Format(L.Share.PasteReady, DescribeContent(content));
        }

        private static string DescribeContent(ShareContent content)
        {
            var parts = new List<string>();

            if (content.Aura != null)
            {
                parts.Add(content.LayerCount > 0
                    ? $"{L.Tab.Aura}(+{content.LayerCount})"
                    : L.Tab.Aura);
            }

            if (content.Trail != null)
                parts.Add(L.Tab.Trail);

            if (content.Muzzle != null)
                parts.Add(L.Tab.Muzzle);

            if (content.Melee != null)
                parts.Add(L.Tab.Melee);

            return parts.Count == 0 ? "-" : string.Join(" · ", parts);
        }

        /// <summary>
        /// 코드에 든 것을 각자의 프로필에 넣습니다.
        ///
        /// <b>지금 어느 탭인지는 보지 않습니다.</b> 코드가 무엇을 담고 있는지가 정합니다 —
        /// 잔상 코드를 오라 탭에서 붙여도 잔상에 들어갑니다. 탭을 맞춰 오라고 하는 것은
        /// 사용자가 기억해야 할 규칙만 늘립니다.
        /// </summary>
        private void ApplyPastedCode()
        {
            string code = CleanCode(_shareField != null ? _shareField.text : "");
            var content = ShareCodec.Decode(code);

            if (content == null)
            {
                ShowHint(L.Share.NotFound);
                return;
            }

            int applied = 0;

            if (content.Aura != null && ApplyIncomingAura(content.Aura))
                applied++;

            if (content.Trail != null)
            {
                var target = CurrentTrailProfile();
                if (target != null)
                {
                    int grade = target.grade;
                    string name = target.name;

                    target.CopyFrom(content.Trail);

                    // 등급 기준값과 이름은 이 자리의 정체성입니다. 남의 설정을 받아도
                    // "몇 등급 자리인지"까지 덮어쓰면 목록이 뒤엉킵니다.
                    target.grade = grade;
                    target.name = name;
                    applied++;
                }
            }

            if (content.Muzzle != null)
            {
                var target = CurrentMuzzleProfile();
                if (target != null)
                {
                    int grade = target.grade;
                    string name = target.name;

                    target.CopyFrom(content.Muzzle);

                    target.grade = grade;
                    target.name = name;
                    applied++;
                }
            }

            if (content.Melee != null)
            {
                var target = CurrentMeleeProfile();
                if (target != null)
                {
                    int grade = target.grade;
                    string name = target.name;

                    target.CopyFrom(content.Melee);

                    target.grade = grade;
                    target.name = name;
                    applied++;
                }
            }

            if (applied == 0)
            {
                ShowHint(L.Share.Failed);
                return;
            }

            SyncAllTabs();
            WeaponAuraSystem.RebuildNow();
            WeaponAuraLayerSystem.RebuildNow();

            ShowHint(string.Format(L.Share.PastedCount, DescribeContent(content)));
            CloseShareWindow();
        }

        private bool ApplyIncomingAura(WeaponAuraProfile incoming)
        {
            var target = CurrentProfile();
            if (target == null)
                return false;

            int minLevel = target.minLevel;
            string name = target.name;
            bool custom = target.custom;

            target.CopyFrom(incoming);

            target.minLevel = minLevel;
            target.name = name;
            target.custom = custom;

            return true;
        }
    }
}
