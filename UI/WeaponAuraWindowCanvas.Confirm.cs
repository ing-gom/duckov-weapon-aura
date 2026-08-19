using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 저장하지 않은 변경을 지키는 장치.
    ///
    /// 이 창은 슬라이더를 움직이는 즉시 게임에 반영됩니다 — 그게 이 창의 장점이지만,
    /// 동시에 "실수로 만진 것"과 "저장할 것"의 구분이 사라진다는 뜻이기도 합니다.
    /// 예전에는 저장을 안 누르고 닫으면 그 변경이 그대로 남았습니다. 파일에는 안 적히니
    /// 다음 실행에는 사라지지만, <b>이번 판</b>에는 계속 적용된 채로 남습니다.
    ///
    /// 그래서 창을 열 때 다섯 저장소의 값을 문자열로 떠 두고, 닫을 때 견줍니다.
    /// 달라졌으면 물어보고, "저장하지 않고 닫기"를 고르면 떠 둔 값으로 되돌립니다.
    ///
    /// 변경을 일일이 추적하지 않고 스냅샷을 견주는 이유 — 편집 경로가 슬라이더·색 선택기·
    /// 토글·프리셋·무작위·붙여넣기로 흩어져 있어서, 하나라도 빠뜨리면 "바꿨는데 안 물어보는"
    /// 조용한 실패가 됩니다. 값 자체를 견주면 빠뜨릴 것이 없습니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private string _snapAura = "";
        private string _snapTrail = "";
        private string _snapMuzzle = "";
        private string _snapMelee = "";
        private string _snapOverrides = "";
        private bool _snapTaken;

        private GameObject? _confirmRoot;

        /// <summary>확인 창이 떠 있는지 (ESC 처리가 이걸 먼저 봅니다)</summary>
        private bool ConfirmOpen => _confirmRoot != null && _confirmRoot.activeSelf;

        // ── 스냅샷 ──────────────────────────────────────────────────

        /// <summary>창을 열 때 지금 값을 떠 둡니다.</summary>
        private void TakeSnapshot()
        {
            try
            {
                _snapAura = WeaponAuraProfiles.Snapshot();
                _snapTrail = BulletTrailProfiles.Snapshot();
                _snapMuzzle = MuzzleFlashProfiles.Snapshot();
                _snapMelee = MeleeSlashProfiles.Snapshot();
                _snapOverrides = WeaponOverrides.Snapshot();
                _snapTaken = true;
            }
            catch (Exception ex)
            {
                // 뜨지 못했으면 되돌릴 수도 없습니다. 그때는 묻지 않고 그냥 닫습니다 —
                // 되돌리지 못하면서 "되돌렸다"고 하는 것이 제일 나쁩니다.
                _snapTaken = false;
                UnityEngine.Debug.LogWarning($"[WeaponAura] 되돌리기용 스냅샷 실패: {ex.Message}");
            }
        }

        /// <summary>저장 뒤에는 그 값이 새 기준이 됩니다 (다시 물어보지 않도록).</summary>
        private void RefreshSnapshot()
        {
            if (_snapTaken)
                TakeSnapshot();
        }

        private bool HasUnsavedChanges()
        {
            if (!_snapTaken)
                return false;

            try
            {
                return _snapAura != WeaponAuraProfiles.Snapshot()
                       || _snapTrail != BulletTrailProfiles.Snapshot()
                       || _snapMuzzle != MuzzleFlashProfiles.Snapshot()
                       || _snapMelee != MeleeSlashProfiles.Snapshot()
                       || _snapOverrides != WeaponOverrides.Snapshot();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>창을 열기 전 상태로 되돌립니다.</summary>
        private void RevertToSnapshot()
        {
            if (!_snapTaken)
                return;

            WeaponAuraProfiles.Restore(_snapAura);
            BulletTrailProfiles.Restore(_snapTrail);
            MuzzleFlashProfiles.Restore(_snapMuzzle);
            MeleeSlashProfiles.Restore(_snapMelee);
            WeaponOverrides.Restore(_snapOverrides);

            // 되돌린 값이 든 무기에 즉시 보여야 합니다. 파티클은 구조가 바뀌었을 수 있으니
            // 살짝 고치는 것(ApplyLive)이 아니라 통째로 다시 만듭니다.
            WeaponAuraSystem.RebuildNow();
            WeaponAuraLayerSystem.RebuildNow();

            UnityEngine.Debug.Log("[WeaponAura] 저장하지 않은 변경을 되돌렸습니다.");
        }

        // ── 확인 창 ─────────────────────────────────────────────────

        /// <summary>
        /// 닫기 요청. 저장할 것이 있으면 확인 창을 띄우고, 없으면 그냥 닫습니다.
        /// </summary>
        private void RequestClose()
        {
            // 라이브러리가 떠 있으면 그것부터 닫습니다 — 위에 덮인 것부터 걷는 것이
            // 사람이 기대하는 순서입니다.
            if (_libraryRoot != null && _libraryRoot.activeSelf)
            {
                CloseWeaponLibrary();
                return;
            }

            if (LayerWindowOpen)
            {
                CloseLayerWindow();
                return;
            }

            if (ShapePickerOpen)
            {
                CloseShapePicker();
                return;
            }

            if (PasteOpen)
            {
                ClosePasteWindow();
                return;
            }

            // 도형 편집기도 위에 덮인 것이라 먼저 걷습니다.
            if (ShapeEditorOpen)
            {
                CloseShapeEditor();
                return;
            }

            if (ConfirmOpen)
            {
                CloseConfirm();
                return;
            }

            // 만들어만 두고 하나도 안 고친 전용 설정은 변경으로 치지 않습니다.
            // 이게 없으면 라이브러리에서 무기를 눌러 보기만 해도 저장하겠냐고 묻습니다.
            PruneUnmodified();

            if (!HasUnsavedChanges())
            {
                Close();
                return;
            }

            OpenConfirm();
        }

        private void OpenConfirm()
        {
            if (_confirmRoot == null)
                BuildConfirm();

            if (_confirmRoot == null)
            {
                // 확인 창을 못 만들었다고 닫히지 않으면 갇힙니다. 그냥 닫습니다.
                Close();
                return;
            }

            _confirmRoot.SetActive(true);
        }

        private void CloseConfirm()
        {
            if (_confirmRoot != null)
                _confirmRoot.SetActive(false);
        }

        private void BuildConfirm()
        {
            if (_canvasRoot == null)
                return;

            var backdrop = MakeImage("ConfirmBackdrop", _canvasRoot.transform, new Color(0f, 0f, 0f, 0.82f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _confirmRoot = backdrop.gameObject;

            var panel = MakeImage("ConfirmPanel", backdrop.transform, PanelColor);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(660f, 250f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = MakeText("Title", panel.transform, L.Confirm.Title, 26, TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(title.rectTransform, 36f);

            var body = MakeText("Body", panel.transform, L.Confirm.Body, 18, DimTextColor,
                TextAlignmentOptions.TopLeft);
            body.enableWordWrapping = true;
            SetHeight(body.rectTransform, 88f);

            var row = MakeRect("Buttons", panel.transform);
            SetHeight(row, 44f);

            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;

            MakeButton(row, L.Confirm.Save, 0f, SaveAndClose, ButtonAccentColor);
            MakeButton(row, L.Confirm.Discard, 0f, DiscardAndClose, ButtonColor);
            MakeButton(row, L.Confirm.Cancel, 0f, CloseConfirm, ButtonColor);

            ApplyFont(panel.gameObject);
        }

        private void SaveAndClose()
        {
            SaveCurrent();
            CloseConfirm();
            Close();
        }

        private void DiscardAndClose()
        {
            RevertToSnapshot();
            CloseConfirm();
            Close();
        }
    }
}
