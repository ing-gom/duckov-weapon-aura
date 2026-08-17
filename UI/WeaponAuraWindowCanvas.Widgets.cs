using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeaponAura.Settings;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 기본 uGUI 부품 생성기와 값 연동. 저장소에 공용 UI 빌더가 없어서 여기 모아 둡니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        // ── 부품 만들기 ──────────────────────────────────────────────

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var rect = MakeRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string content,
            int size, Color color, TextAlignmentOptions alignment)
        {
            var rect = MakeRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private Button MakeButton(Transform parent, string label, float width, Action onClick, Color color)
        {
            var image = MakeImage("Button_" + label, parent, color);
            var rect = image.rectTransform;

            if (width > 0f)
            {
                var element = rect.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // 게임 버튼처럼 살짝 밝아지는 정도만.
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            button.onClick.AddListener(() => onClick());

            var text = MakeText("Label", rect, label, 19, TextColor, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            return button;
        }

        /// <summary>
        /// 게임의 옵션 슬라이더와 같은 모양(가는 홈 + 채움 + 손잡이)으로 만듭니다.
        /// 게임 슬라이더 프리팹은 옵션 화면이 한 번이라도 열려야 메모리에 올라오기 때문에,
        /// 확실하게 동작하도록 직접 조립합니다.
        /// </summary>
        private Slider MakeSlider(Transform parent, float min, float max)
        {
            var root = MakeRect("Slider", parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var background = MakeImage("Background", root, new Color(0f, 0f, 0f, 0.55f));
            var bgRect = background.rectTransform;
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(1f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(0f, 8f);
            bgRect.anchoredPosition = Vector2.zero;

            var fillArea = MakeRect("FillArea", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(-18f, 8f);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = MakeImage("Fill", fillArea, ButtonAccentColor * 1.4f);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.sizeDelta = new Vector2(18f, 0f);

            var handleArea = MakeRect("HandleArea", root);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.sizeDelta = new Vector2(-18f, 0f);
            handleArea.anchoredPosition = Vector2.zero;

            var handle = MakeImage("Handle", handleArea, new Color(0.92f, 0.96f, 1f, 1f));
            var handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(18f, 22f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;

            return slider;
        }

        /// <summary>
        /// 라벨이 앞에 붙은 입력칸. TMP_InputField는 자식 구조(뷰포트·텍스트)를
        /// 직접 만들어 줘야 동작합니다.
        /// </summary>
        private TMP_InputField MakeInputField(Transform parent, string label, float width,
            TMP_InputField.CharacterValidation validation)
        {
            var holder = MakeRect("Field_" + label, parent);
            SetWidth(holder, width);

            var layout = holder.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var caption = MakeText("Caption", holder, label, 17, DimTextColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(caption.rectTransform, label.Length <= 1 ? 16f : 40f);

            var box = MakeImage("Box", holder, new Color(0f, 0f, 0f, 0.5f));
            box.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var viewport = MakeRect("TextArea", box.transform);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8f, 2f);
            viewport.offsetMax = new Vector2(-8f, -2f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = MakeText("Text", viewport, "", 17, TextColor, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.characterValidation = validation;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.targetGraphic = box;
            field.customCaretColor = true;
            field.caretColor = TextColor;

            return field;
        }

        /// <summary>
        /// 세로 스크롤바. 오른쪽 끝에 붙이고, 내용이 넘칠 때만 나타납니다.
        /// (ScrollRect 혼자서는 막대를 그려 주지 않아 직접 만들어 물려 줘야 합니다)
        /// </summary>
        private Scrollbar MakeVerticalScrollbar(Transform parent, float width)
        {
            var bar = MakeImage("Scrollbar", parent, new Color(0f, 0f, 0f, 0.35f));
            var barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.sizeDelta = new Vector2(width, 0f);
            barRect.anchoredPosition = Vector2.zero;

            var slidingArea = MakeRect("SlidingArea", barRect);
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(2f, 2f);
            slidingArea.offsetMax = new Vector2(-2f, -2f);

            var handle = MakeImage("Handle", slidingArea, new Color(0.45f, 0.65f, 0.8f, 0.95f));

            // 손잡이의 앵커는 Scrollbar가 매 프레임 직접 조정합니다.
            // sizeDelta를 0으로 두지 않으면 RectTransform 기본값(100×100)이 그대로 남아
            // 막대가 옆 내용까지 덮어 버립니다.
            var handleRect = handle.rectTransform;
            handleRect.sizeDelta = Vector2.zero;
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.pivot = new Vector2(0.5f, 0.5f);

            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            return scrollbar;
        }

        // ── 레이아웃 보조 ────────────────────────────────────────────

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetHeight(RectTransform rect, float height)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>();
            if (element == null)
                element = rect.gameObject.AddComponent<LayoutElement>();

            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;
        }

        private static void SetWidth(RectTransform rect, float width)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>();
            if (element == null)
                element = rect.gameObject.AddComponent<LayoutElement>();

            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }

        // ── 값 연동 ─────────────────────────────────────────────────

        /// <summary>지금 편집 중인 티어의 프로필</summary>
        private WeaponAuraProfile? CurrentProfile()
        {
            if (_editingTier < 0)
                _editingTier = Mathf.Max(0, WeaponAuraSystem.CurrentTier);

            return WeaponAuraProfiles.Get(_editingTier);
        }

        /// <summary>슬라이더를 프로필 값으로 되돌려 채웁니다 (알림 없이).</summary>
        private void SyncFromProfile()
        {
            var profile = CurrentProfile();
            if (profile == null)
                return;

            _suppressCallbacks = true;
            try
            {
                foreach (var row in _rows)
                {
                    float value = row.Get(profile);
                    row.Slider.SetValueWithoutNotify(value);
                    row.ValueText.text = value.ToString(row.Format);
                }

                _pickerA?.SetColor(profile.colorA);
                _pickerB?.SetColor(profile.colorB);
            }
            finally
            {
                _suppressCallbacks = false;
            }

            // 사용자에게 보여 주는 숫자는 배열 순번이 아니라 이 티어가 담당하는 등급 값입니다.
            if (_headerText != null)
                _headerText.text = string.Format(L.Window.TitleWithTier, profile.minLevel, profile.name);

            HighlightTierButton();
            RefreshDisplayRow();
            RefreshRemoveButton();
            RefreshAuraTextureLabel();
        }

        // ── 켜기/끄기 · 세기 ────────────────────────────────────────

        /// <summary>
        /// 오라를 끄면 마지막으로 쓰던 세기를 기억해 뒀다가 다시 켤 때 되돌립니다.
        /// (끌 때마다 "보통"으로 초기화되면 강하게 쓰던 사람이 매번 다시 골라야 합니다)
        /// </summary>
        /// <summary>
        /// 지금 편집 중인 티어에만 오라를 켜고 끕니다.
        ///
        /// 모드 전체를 껐다 켜는 스위치가 아니라 "이 등급 무기에는 오라를 원하지 않는다"는 설정입니다.
        /// 프로필에 함께 저장되므로 아래 "저장하기"를 눌러야 다음 실행에도 유지됩니다.
        /// </summary>
        private void ToggleTierEnabled()
        {
            var profile = CurrentProfile();
            if (profile == null)
                return;

            profile.enabled = !profile.enabled;

            UnityEngine.Debug.Log(
                $"[WeaponAura] 등급 {profile.minLevel} ({profile.name}) 오라 " +
                $"{(profile.enabled ? "켜기" : "끄기")}");

            RefreshDisplayRow();
            HighlightTierButton();

            // 티어가 통째로 켜지거나 꺼지는 변화라서 값만 반영해서는 안 되고 다시 만들어야 합니다.
            // 지금 든 무기가 이 티어가 아니어도 판정을 다시 시키는 편이 안전합니다.
            WeaponAuraSystem.RebuildNow();
        }

        private void ToggleTrail()
        {
            var profile = CurrentProfile();
            if (profile == null)
                return;

            profile.trailEnabled = !profile.trailEnabled;

            // 트레일이 없던 파티클에 꼬리를 붙이는 건 구조 변경이라 재생성이 필요합니다.
            RefreshDisplayRow();
            ApplyEdit(true);
        }

        private void RefreshDisplayRow()
        {
            var profile = CurrentProfile();
            bool tierEnabled = profile == null || profile.enabled;

            if (_toggleButton != null)
            {
                // 버튼 글자는 "지금 상태"가 아니라 "누르면 일어나는 일"입니다.
                var label = _toggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = tierEnabled ? L.Display.TierOff : L.Display.TierOn;

                if (_toggleButton.targetGraphic != null)
                    _toggleButton.targetGraphic.color = tierEnabled ? ButtonAccentColor : ButtonColor;
            }

            foreach (var pair in _strengthButtons)
            {
                var button = pair.Value;
                if (button == null || button.targetGraphic == null)
                    continue;

                button.targetGraphic.color =
                    AuraSettings.AuraMode == pair.Key ? ButtonAccentColor : ButtonColor;
            }

            if (_trailButton != null)
            {
                bool trail = profile != null && profile.trailEnabled;

                var label = _trailButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = trail ? L.Field.TrailOff : L.Field.TrailOn;

                if (_trailButton.targetGraphic != null)
                    _trailButton.targetGraphic.color = trail ? ButtonAccentColor : ButtonColor;
            }

            if (!tierEnabled)
                ShowHint(L.Display.TierDisabledNotice);
        }

        private bool _suppressCallbacks;

        private void OnSliderChanged(SliderRow row, float value)
        {
            if (_suppressCallbacks)
                return;

            var profile = CurrentProfile();
            if (profile == null)
                return;

            row.Set(profile, value);
            row.ValueText.text = value.ToString(row.Format);

            ApplyEdit(row.NeedsRebuild);
        }

        /// <summary>
        /// 편집 결과를 실제 오라에 반영합니다.
        /// 지금 든 무기의 티어를 편집 중일 때만 본편이 바뀝니다 — 다른 티어를 손보는 중이면
        /// 왼쪽 미리보기에서만 확인됩니다.
        /// </summary>
        private void ApplyEdit(bool rebuild)
        {
            if (_editingTier != WeaponAuraSystem.CurrentTier)
                return;

            // 겹 수처럼 구조가 바뀌는 값은 다시 만들어야 반영됩니다.
            if (rebuild)
                WeaponAuraSystem.RebuildNow();
            else
                WeaponAuraSystem.ApplyLive();
        }

        /// <summary>선택한 티어 버튼만 밝게 표시합니다.</summary>
        private void HighlightTierButton()
        {
            for (int i = 0; i < _tierButtons.Count; i++)
            {
                var button = _tierButtons[i];
                if (button == null || button.targetGraphic == null)
                    continue;

                var profile = WeaponAuraProfiles.Get(i);
                var baseColor = profile != null ? profile.colorA : Color.gray;

                float shade = i == _editingTier ? 1f : 0.55f;

                // 꺼 둔 티어는 회색으로 눕혀서 한눈에 구분되게 합니다.
                if (profile != null && !profile.enabled)
                {
                    float grey = (baseColor.r + baseColor.g + baseColor.b) / 3f * 0.35f;
                    baseColor = new Color(grey, grey, grey);
                }

                button.targetGraphic.color =
                    new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 0.95f);
            }
        }

        // ── 티어 추가 / 삭제 ─────────────────────────────────────────

        private void AddTier()
        {
            if (_newTierField == null)
                return;

            if (!int.TryParse(_newTierField.text.Trim(), out int minLevel))
            {
                ShowHint(L.Tier.InvalidGrade);
                return;
            }

            // 입력칸에서 세 자리로 막아 두긴 했지만, 붙여넣기나 음수까지 막지는 못합니다.
            if (minLevel < 0 || minLevel > MaxTierGrade)
            {
                ShowHint(string.Format(L.Tier.OutOfRange, MaxTierGrade));
                return;
            }

            int index = WeaponAuraProfiles.AddTier(minLevel);
            if (index < 0)
            {
                ShowHint(string.Format(L.Tier.Duplicate, minLevel));
                return;
            }

            _newTierField.SetTextWithoutNotify("");

            // 추가한 티어를 바로 편집하도록 넘어갑니다.
            _followWeapon = false;
            _editingTier = index;

            FillTierGrid();
            SyncFromProfile();
            WeaponAuraSystem.RebuildNow();

            ShowHint(string.Format(L.Tier.Added, minLevel));
        }

        private void RemoveCurrentTier()
        {
            var profile = CurrentProfile();
            if (profile == null)
                return;

            if (!profile.custom)
            {
                ShowHint(L.Tier.BuiltinLocked);
                return;
            }

            int removedLevel = profile.minLevel;
            if (!WeaponAuraProfiles.RemoveTier(_editingTier))
                return;

            // 지운 자리 뒤로 인덱스가 한 칸씩 당겨집니다.
            _editingTier = Mathf.Clamp(_editingTier, 0, WeaponAuraProfiles.TierCount - 1);

            FillTierGrid();
            SyncFromProfile();
            WeaponAuraSystem.RebuildNow();

            ShowHint(string.Format(L.Tier.Removed, removedLevel));
        }

        /// <summary>기본 티어를 편집 중이면 삭제 버튼을 눌러도 소용없으므로 흐리게 둡니다.</summary>
        private void RefreshRemoveButton()
        {
            if (_removeTierButton == null)
                return;

            var profile = CurrentProfile();
            _removeTierButton.interactable = profile != null && profile.custom;
        }

        private void RefreshStatus()
        {
            if (_statusText == null)
                return;

            string follow = _followWeapon ? L.Tier.FollowState : L.Tier.ManualState;
            string scope = _editingTier == WeaponAuraSystem.CurrentTier
                ? L.Tier.AppliesToHeld
                : L.Tier.PreviewOnly;

            var editing = CurrentProfile();
            int editingGrade = editing != null ? editing.minLevel : 0;

            // 편집 대상과 반영 범위는 한 줄에 넣으면 왼쪽 열 폭을 넘겨서 잘립니다.
            _statusText.text =
                string.Format(L.Status.Weapon, WeaponAuraSystem.CurrentWeaponName) + "\n" +
                string.Format(L.Status.Grade, WeaponAuraSystem.CurrentMetaQuality) + "\n" +
                string.Format(L.Status.Editing, editingGrade, follow) + "\n" +
                scope + "\n" +
                (_preview != null && _preview.Status != "-" ? _preview.Status : string.Empty);
        }

        // ── 저장 · 프리셋 ────────────────────────────────────────────

        /// <summary>
        /// 저장은 탭과 무관하게 둘 다 씁니다.
        ///
        /// 파일이 두 개로 나뉘어 있다는 건 우리 사정이지 사용자 사정이 아닙니다.
        /// 오라를 손보고 잔상 탭에서 저장을 눌렀다고 오라 쪽이 날아가면 안 됩니다.
        /// </summary>
        private void SaveCurrent()
        {
            bool auraSaved = WeaponAuraProfiles.Save(out string auraPath);
            bool trailSaved = BulletTrailProfiles.Save(out string trailPath);
            bool muzzleSaved = MuzzleFlashProfiles.Save(out string muzzlePath);
            bool meleeSaved = MeleeSlashProfiles.Save(out string meleePath);

            if (auraSaved && trailSaved && muzzleSaved && meleeSaved)
            {
                UnityEngine.Debug.Log(
                    $"[WeaponAura] 설정을 저장했습니다: {auraPath}, {trailPath}, {muzzlePath}, {meleePath}");
                ShowHint(L.Action.Saved);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"[WeaponAura] 설정 저장에 실패했습니다 " +
                    $"(오라={auraSaved}, 탄환 잔상={trailSaved}, 총구 화염={muzzleSaved}, " +
                    $"근접 참격={meleeSaved}).");
                ShowHint(L.Action.SaveFailed);
            }
        }

        /// <summary>
        /// 편집 중인 티어를 무작위 조합으로 바꿉니다.
        /// 이름·등급 임계값·텍스처 선택은 그대로 두고 색과 움직임만 갈아 끼웁니다.
        /// </summary>
        private void RandomizeCurrent()
        {
            // 무작위·초기화는 지금 보고 있는 탭에만 걸립니다.
            // (안 보이는 탭 값이 조용히 바뀌면 되돌릴 방법이 없습니다)
            if (_tab == WindowTab.Trail)
            {
                RandomizeCurrentTrail();
                return;
            }

            if (_tab == WindowTab.Muzzle)
            {
                RandomizeCurrentMuzzle();
                return;
            }

            if (_tab == WindowTab.Melee)
            {
                RandomizeCurrentMelee();
                return;
            }

            var target = CurrentProfile();
            if (target == null)
                return;

            int seed = UnityEngine.Random.Range(0, int.MaxValue);

            var generated = WeaponAuraProfiles.CreateRandom(seed, target.name, target.minLevel);
            generated.textureName = target.textureName;
            generated.tilesX = target.tilesX;
            generated.tilesY = target.tilesY;

            target.CopyFrom(generated);

            SyncFromProfile();
            ApplyEdit(true);
            ShowHint(string.Format(L.Action.RandomApplied, target.minLevel, seed));
        }

        /// <summary>
        /// 속성 템플릿을 편집 중인 등급에 씌웁니다.
        /// 등급 기준값·이름·텍스처 선택은 유지하고 색과 움직임만 갈아 끼웁니다.
        /// </summary>
        private void ApplyPreset(WeaponAuraPresetKind kind, string label)
        {
            var target = CurrentProfile();
            if (target == null)
                return;

            var preset = WeaponAuraProfiles.CreatePreset(kind, target.name, target.minLevel);
            preset.textureName = target.textureName;
            preset.tilesX = target.tilesX;
            preset.tilesY = target.tilesY;

            // 이 등급을 꺼 뒀다면 템플릿을 바꿨다고 멋대로 켜지 않습니다.
            preset.enabled = target.enabled;
            preset.custom = target.custom;

            // 오로라·전격 프리셋은 옛 Ribbon 방식이라 무기를 감싸는 껍질을 만들지 않습니다.
            // (Build가 Sheet일 때만 CreateSheets를 부르고, ribbonShowHeads=false라 알갱이도 숨습니다)
            // 그대로 적용하면 오라가 꺼진 것처럼 보이므로 껍질 방식은 그대로 두고
            // 색과 움직임만 가져옵니다 — 템플릿이 하기로 한 일이 원래 그것입니다.
            if (preset.renderStyle != WeaponAuraRenderStyle.Sheet)
            {
                preset.renderStyle = WeaponAuraRenderStyle.Sheet;

                preset.sheetLayers = target.sheetLayers;
                preset.sheetSpread = target.sheetSpread;
                preset.sheetRoundness = target.sheetRoundness;
                preset.sheetUseWeaponMesh = target.sheetUseWeaponMesh;
                preset.sheetBoxiness = target.sheetBoxiness;
                preset.sheetRings = target.sheetRings;
                preset.sheetRingSpeed = target.sheetRingSpeed;
                preset.sheetPeriod = target.sheetPeriod;
                preset.sheetWobble = target.sheetWobble;
                preset.sheetWobbleSpeed = target.sheetWobbleSpeed;
            }

            target.CopyFrom(preset);

            SyncFromProfile();
            ApplyEdit(true);
            ShowHint(string.Format(L.Preset.Applied, target.minLevel, label));
        }

        private void ResetDefaults()
        {
            if (_tab == WindowTab.Trail)
            {
                ResetTrailDefaults();
                return;
            }

            if (_tab == WindowTab.Muzzle)
            {
                ResetMuzzleDefaults();
                return;
            }

            if (_tab == WindowTab.Melee)
            {
                ResetMeleeDefaults();
                return;
            }

            WeaponAuraProfiles.ResetToDefaults();
            AfterProfilesReplaced();
            ShowHint(L.Action.ResetDone);
        }

        private void ShowHint(string message)
        {
            if (_saveHintText != null)
                _saveHintText.text = message;
        }

        /// <summary>프로필 배열이 통째로 바뀐 뒤 UI와 실제 오라를 함께 갱신합니다.</summary>
        private void AfterProfilesReplaced()
        {
            SyncFromProfile();
            WeaponAuraSystem.RebuildNow();
        }
    }
}
