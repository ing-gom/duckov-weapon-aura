using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeaponAura.Settings;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 창의 실제 구성. 게임에는 재사용할 만한 "창 프리팹"이 따로 없어서
    /// (다른 모드들도 전부 그렇게 합니다) 게임 폰트·색·레이아웃 규칙을 따라 직접 조립합니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        /// <summary>슬라이더 한 줄. 프로필의 필드 하나와 양방향으로 묶입니다.</summary>
        private class SliderRow
        {
            public Slider Slider = null!;
            public TextMeshProUGUI ValueText = null!;
            public string Format = "0.00";
            public Func<WeaponAuraProfile, float> Get = null!;
            public Action<WeaponAuraProfile, float> Set = null!;

            /// <summary>구조가 바뀌는 값(겹 수 등)은 파티클을 다시 만들어야 합니다.</summary>
            public bool NeedsRebuild;
        }

        private bool _built;

        private void BuildUI()
        {
            if (_built || _canvasRoot == null)
                return;

            EnsureFont();

            var backdrop = MakeImage("Backdrop", _canvasRoot.transform, BackdropColor);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;   // 뒤쪽 UI 클릭 차단

            var panel = MakeImage("Panel", backdrop.transform, PanelColor);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, 0f);
            _panel = panel.gameObject;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 높이는 내용에 맞춰 잡습니다. 고정값을 쓰면 티어 격자·상태 문구가 늘어났을 때
            // 배경이 짧아져서 아래 버튼 줄이 패널 밖으로 삐져나옵니다.
            var panelFitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHeader(panel.transform);
            BuildBody(panel.transform);
            BuildFooter(panel.transform);

            ApplyFont(panel.gameObject);
            _built = true;
        }

        // ── 머리말 ──────────────────────────────────────────────────

        private void BuildHeader(Transform parent)
        {
            var header = MakeRect("Header", parent);
            SetHeight(header, 44f);

            var row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.spacing = 12f;

            _headerText = MakeText("Title", header, L.Window.Title, 30, TextColor, TextAlignmentOptions.MidlineLeft);
            var flexible = _headerText.gameObject.AddComponent<LayoutElement>();
            flexible.flexibleWidth = 1f;

            MakeButton(header, L.Window.Close, 120f, Hide, ButtonColor);
        }

        // ── 본문 ────────────────────────────────────────────────────

        private void BuildBody(Transform parent)
        {
            var body = MakeRect("Body", parent);

            // 왼쪽 열(미리보기 360 + 확대 + 상태 + 등급 격자 + 추가/삭제 + 따라가기)이
            // 들어가는 높이입니다. 이보다 작으면 아래 항목이 패널 밖으로 밀립니다.
            var bodyElement = body.gameObject.AddComponent<LayoutElement>();
            bodyElement.preferredHeight = BodyHeight;
            bodyElement.minHeight = BodyHeight;
            bodyElement.flexibleHeight = 0f;

            var row = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 18f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            BuildLeftColumn(body);
            BuildRightColumn(body);
        }

        private void BuildLeftColumn(Transform parent)
        {
            var column = MakeImage("Left", parent, SectionColor).rectTransform;
            var element = column.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 420f;
            element.flexibleWidth = 0f;

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 실제 손에 든 무기를 전용 카메라로 찍어서 보여줍니다.
            var frame = MakeImage("PreviewFrame", column, new Color(0f, 0f, 0f, 0.65f)).rectTransform;
            SetHeight(frame, 360f);

            var previewGo = MakeRect("Preview", frame);
            Stretch(previewGo);
            _previewImage = previewGo.gameObject.AddComponent<RawImage>();
            _previewImage.color = new Color(1f, 1f, 1f, 0.1f);

            // 드래그로 돌려 볼 수 있게 합니다 (자동 회전은 드래그하면 멈춥니다).
            _previewImage.raycastTarget = true;
            var drag = previewGo.gameObject.AddComponent<PointerDragArea>();

            Vector2 last = Vector2.zero;
            drag.OnPressed = position => last = position;
            drag.OnPicked = position =>
            {
                if (_preview != null)
                {
                    var delta = position - last;
                    _preview.AutoRotate = false;
                    _preview.Rotate(-delta.x * 360f, -delta.y * 180f);
                }

                last = position;
            };

            BuildZoomRow(column);

            _statusText = MakeText("Status", column, "-", 17, DimTextColor, TextAlignmentOptions.TopLeft);
            SetHeight(_statusText.rectTransform, 124f);

            // 무기 이름이 길면(예: "*Skyfall* TS-128 스마트형 저격소총") 열 밖으로 흘러나갑니다.
            // 줄바꿈을 켜서 안에서 접히게 합니다.
            _statusText.enableWordWrapping = true;

            var tierLabel = MakeText("TierLabel", column, L.Tier.SectionLabel, 20, TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(tierLabel.rectTransform, 30f);

            BuildTierGrid(column);

            var follow = MakeButton(column, L.Tier.FollowWeapon, 0f, () =>
            {
                _followWeapon = true;
                int tier = WeaponAuraSystem.CurrentTier;
                if (tier >= 0)
                    _editingTier = tier;
                SyncFromProfile();
            }, ButtonAccentColor);

            SetHeight((RectTransform)follow.transform, 42f);
        }

        /// <summary>
        /// 미리보기 확대 조절. 프로필 값이 아니라 보기 설정이라서 저장하지 않습니다.
        /// </summary>
        private void BuildZoomRow(Transform parent)
        {
            var row = MakeRect("ZoomRow", parent);
            SetHeight(row, 34f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var label = MakeText("Label", row, L.Preview.Zoom, 18, TextColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(label.rectTransform, 56f);

            var slider = MakeSlider(row, 0.4f, 2.2f);
            slider.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            slider.SetValueWithoutNotify(1f);
            slider.onValueChanged.AddListener(value =>
            {
                if (_preview != null)
                    _preview.Zoom = value;
            });

            MakeButton(row, L.Preview.ResetView, 90f, () => _preview?.ResetView(), ButtonColor);
        }

        /// <summary>
        /// 등급 버튼 격자.
        ///
        /// 티어를 추가하면 줄이 계속 늘어나 창을 밀어내므로, 두 줄까지만 보여 주고
        /// 그보다 많아지면 격자 안에서 스크롤합니다.
        /// </summary>
        private void BuildTierGrid(Transform parent)
        {
            var scrollGo = MakeRect("TierScroll", parent);
            SetHeight(scrollGo, TierVisibleRows * TierCellHeight + (TierVisibleRows - 1) * TierCellSpacing);

            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewport = MakeImage("Viewport", scrollGo, new Color(0f, 0f, 0f, 0f)).rectTransform;
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var grid = MakeRect("TierGrid", viewport);
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.sizeDelta = Vector2.zero;
            scroll.content = grid;

            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(TierCellWidth, TierCellHeight);
            layout.spacing = new Vector2(TierCellSpacing, TierCellSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = TierColumns;

            // 줄 수에 맞춰 내용 높이가 자동으로 잡혀야 스크롤 범위가 맞습니다.
            var fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = MakeVerticalScrollbar(scrollGo, ScrollbarWidth);
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 2f;

            _tierGrid = grid;
            FillTierGrid();

            BuildTierEditRow(parent);
        }

        /// <summary>
        /// 티어 버튼을 다시 만듭니다. 티어를 추가·삭제하면 개수와 순서가 바뀌므로
        /// 색만 갱신하는 걸로는 부족합니다.
        /// </summary>
        private void FillTierGrid()
        {
            if (_tierGrid == null)
                return;

            // Destroy는 프레임 끝에 처리되므로 새로 만든 버튼과 한 프레임 겹쳐 보입니다.
            for (int i = _tierGrid.childCount - 1; i >= 0; i--)
                DestroyImmediate(_tierGrid.GetChild(i).gameObject);

            _tierButtons.Clear();

            for (int i = 0; i < WeaponAuraProfiles.TierCount; i++)
            {
                int tier = i;
                var profile = WeaponAuraProfiles.Get(tier);
                var color = profile != null ? profile.colorA : Color.gray;

                // 버튼에 적는 숫자는 순번이 아니라 이 티어가 담당하는 등급 값입니다.
                // (+ 로 9나 999 같은 값을 넣었을 때 그대로 보여야 합니다)
                string label = profile != null ? profile.minLevel.ToString() : "?";

                var button = MakeButton(_tierGrid, label, 0f,
                    () =>
                    {
                        _followWeapon = false;
                        _editingTier = tier;
                        SyncFromProfile();
                    },
                    new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.95f));

                _tierButtons.Add(button);
            }

            if (_font != null)
                ApplyFont(_tierGrid.gameObject);
        }

        /// <summary>등급 값을 직접 넣어 티어를 추가하거나, 추가한 티어를 지웁니다.</summary>
        private void BuildTierEditRow(Transform parent)
        {
            var row = MakeRect("TierEditRow", parent);
            SetHeight(row, 38f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            _newTierField = MakeInputField(row, L.Tier.GradeField, 150f,
                TMP_InputField.CharacterValidation.Integer);

            // 999가 상한이라 네 자리 이상은 아예 못 넣게 막습니다.
            _newTierField.characterLimit = 3;

            MakeButton(row, L.Tier.Add, 130f, AddTier, ButtonAccentColor);
            _removeTierButton = MakeButton(row, L.Tier.Remove, 120f, RemoveCurrentTier, ButtonColor);
        }

        private void BuildRightColumn(Transform parent)
        {
            var column = MakeImage("Right", parent, SectionColor).rectTransform;
            var element = column.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;

            var scrollGo = MakeRect("Scroll", column);
            Stretch(scrollGo);
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = MakeImage("Viewport", scrollGo, new Color(0f, 0f, 0f, 0f)).rectTransform;
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            // 내용이 넘칠 때만 오른쪽에 막대가 나타나고, 그만큼 뷰포트가 줄어듭니다.
            var scrollbar = MakeVerticalScrollbar(scrollGo, ScrollbarWidth);
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 4f;

            var content = MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);
            scroll.content = content;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildControls(content);
        }

        // ── 실제 조절 항목 ───────────────────────────────────────────

        private void BuildControls(Transform parent)
        {
            _rows.Clear();

            AddSectionLabel(parent, L.Section.Display);
            BuildDisplayRow(parent);

            AddSectionLabel(parent, L.Section.ColorInner);
            _pickerA = AddColorPicker(parent, color =>
            {
                var profile = CurrentProfile();
                if (profile == null)
                    return;

                // 알파는 아래 투명도 슬라이더가 따로 관리합니다.
                profile.colorA = new Color(color.r, color.g, color.b, profile.colorA.a);
                ApplyEdit(false);
            });

            AddSectionLabel(parent, L.Section.ColorOuter);
            _pickerB = AddColorPicker(parent, color =>
            {
                var profile = CurrentProfile();
                if (profile == null)
                    return;

                profile.colorB = new Color(color.r, color.g, color.b, profile.colorB.a);
                ApplyEdit(false);
            });

            AddSectionLabel(parent, L.Section.Shape);
            AddSlider(parent, L.Field.Alpha, 0f, 1f, "0.00",
                p => p.alpha, (p, v) => p.alpha = v);
            AddSlider(parent, L.Field.Intensity, 0.5f, 3f, "0.00",
                p => p.colorIntensity, (p, v) => p.colorIntensity = v);
            AddSlider(parent, L.Field.Layers, 1f, 8f, "0",
                p => p.sheetLayers, (p, v) => p.sheetLayers = Mathf.RoundToInt(v), rebuild: true);
            AddSlider(parent, L.Field.Spread, 0f, 0.6f, "0.000",
                p => p.sheetSpread, (p, v) => p.sheetSpread = v);

            AddSectionLabel(parent, L.Section.Waves);
            AddSlider(parent, L.Field.WaveCount, 0f, 12f, "0.0",
                p => p.sheetRings, (p, v) => p.sheetRings = v);
            AddSlider(parent, L.Field.WaveSpeed, 0f, 3f, "0.00",
                p => p.sheetRingSpeed, (p, v) => p.sheetRingSpeed = v);
            AddSlider(parent, L.Field.Period, 0.5f, 6f, "0.00",
                p => p.sheetPeriod, (p, v) => p.sheetPeriod = v);
            AddSlider(parent, L.Field.Wobble, 0f, 0.5f, "0.00",
                p => p.sheetWobble, (p, v) => p.sheetWobble = v);

            AddSectionLabel(parent, L.Section.Presets);
            BuildPresetGrid(parent);

            AddSectionLabel(parent, L.Section.Particles);
            BuildTrailRow(parent);
            AddSlider(parent, L.Field.EmissionRate, 0f, 80f, "0",
                p => p.emissionRate, (p, v) => p.emissionRate = v);
            AddSlider(parent, L.Field.ParticleSize, 0.005f, 0.12f, "0.000",
                p => p.startSize, (p, v) => p.startSize = v);
            AddSlider(parent, L.Field.ParticleLifetime, 0.2f, 3f, "0.00",
                p => p.lifetime, (p, v) => p.lifetime = v);

            // 잔상은 파티클마다 꼬리를 붙이는 방식이라, 켜져 있을 때만 의미가 있습니다.
            AddSlider(parent, L.Field.TrailLength, 0.05f, 1f, "0.00",
                p => p.trailLifetime, (p, v) => p.trailLifetime = v, rebuild: true);
            AddSlider(parent, L.Field.TrailWidth, 0.05f, 1.5f, "0.00",
                p => p.trailWidth, (p, v) => p.trailWidth = v, rebuild: true);
        }

        /// <summary>
        /// 속성 템플릿 12종. 누르면 편집 중인 등급의 색과 움직임이 통째로 바뀝니다.
        /// (등급 기준값·이름·텍스처 선택은 그대로 둡니다)
        /// </summary>
        private void BuildPresetGrid(Transform parent)
        {
            var presets = new (WeaponAuraPresetKind Kind, string Label)[]
            {
                (WeaponAuraPresetKind.Aurora, L.Preset.Aurora),
                (WeaponAuraPresetKind.Fire, L.Preset.Fire),
                (WeaponAuraPresetKind.Frost, L.Preset.Frost),
                (WeaponAuraPresetKind.Toxic, L.Preset.Toxic),
                (WeaponAuraPresetKind.Void, L.Preset.Void),
                (WeaponAuraPresetKind.Shock, L.Preset.Shock),
                (WeaponAuraPresetKind.Holy, L.Preset.Holy),
                (WeaponAuraPresetKind.Blood, L.Preset.Blood),
                (WeaponAuraPresetKind.Arcane, L.Preset.Arcane),
                (WeaponAuraPresetKind.Plasma, L.Preset.Plasma),
                (WeaponAuraPresetKind.Nature, L.Preset.Nature),
                (WeaponAuraPresetKind.Shadow, L.Preset.Shadow),
            };

            const int columns = 4;
            const float cellHeight = 36f;
            const float spacing = 6f;

            int rows = Mathf.CeilToInt(presets.Length / (float)columns);

            var grid = MakeRect("PresetGrid", parent);
            SetHeight(grid, rows * cellHeight + (rows - 1) * spacing);

            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(140f, cellHeight);
            layout.spacing = new Vector2(spacing, spacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            foreach (var preset in presets)
            {
                var kind = preset.Kind;
                string label = preset.Label;
                MakeButton(grid, label, 0f, () => ApplyPreset(kind, label), ButtonColor);
            }
        }

        /// <summary>
        /// 잔상(파티클 꼬리) 켜기/끄기.
        /// 트레일은 파티클 시스템 구조를 바꾸므로 값만 반영해서는 안 되고 다시 만들어야 합니다.
        /// </summary>
        private void BuildTrailRow(Transform parent)
        {
            var row = MakeRect("TrailRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            _trailButton = MakeButton(row, "", 0f, ToggleTrail, ButtonColor);
        }

        /// <summary>
        /// 이 티어에 오라를 적용할지 + 전체 세기.
        ///
        /// 켜기/끄기는 <b>편집 중인 티어에만</b> 걸립니다 (프로필에 저장 → "저장하기" 필요).
        /// 세기는 모드 설정이라 누르는 즉시 저장됩니다.
        /// </summary>
        private void BuildDisplayRow(Transform parent)
        {
            // 한 줄에 몰아넣으면 언어에 따라 라벨이 길어져 버튼 위로 글자가 겹칩니다.
            // (영어에서 "Disable aura for this tier"가 "Strength"를 덮었습니다)
            var toggleRow = MakeRect("TierToggleRow", parent);
            SetHeight(toggleRow, 40f);

            var toggleLayout = toggleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            toggleLayout.childControlWidth = true;
            toggleLayout.childControlHeight = true;
            toggleLayout.childForceExpandWidth = true;
            toggleLayout.childAlignment = TextAnchor.MiddleLeft;

            _toggleButton = MakeButton(toggleRow, "", 0f, ToggleTierEnabled, ButtonAccentColor);

            var row = MakeRect("StrengthRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var strengthLabel = MakeText("StrengthLabel", row, L.Display.Strength, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(strengthLabel.rectTransform, 100f);

            _strengthButtons.Clear();
            AddStrengthButton(row, L.Display.Subtle, WeaponAuraMode.Subtle);
            AddStrengthButton(row, L.Display.Normal, WeaponAuraMode.Normal);
            AddStrengthButton(row, L.Display.Intense, WeaponAuraMode.Intense);
        }

        private void AddStrengthButton(Transform parent, string label, WeaponAuraMode mode)
        {
            var button = MakeButton(parent, label, 100f, () =>
            {
                AuraSettings.SetMode(mode);
                RefreshDisplayRow();
            }, ButtonColor);

            _strengthButtons.Add(new KeyValuePair<WeaponAuraMode, Button>(mode, button));
        }

        /// <summary>
        /// 색 고르기 한 벌: 채도·명도 사각형 + 색조 막대 + 견본, 그리고 HEX·R·G·B 입력.
        /// </summary>
        private ColorPickerControl AddColorPicker(Transform parent, Action<Color> onChanged)
        {
            // 위: 사각형 · 색조 · 견본
            var top = MakeRect("PickerTop", parent);
            SetHeight(top, 140f);

            var topLayout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 10f;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;

            var square = MakeImage("SaturationValue", top, Color.white);
            var squareElement = square.gameObject.AddComponent<LayoutElement>();
            squareElement.preferredWidth = 200f;

            // 지금 고른 지점을 표시하는 작은 고리
            var cursor = MakeImage("Cursor", square.transform, new Color(1f, 1f, 1f, 0.9f));
            var cursorRect = cursor.rectTransform;
            cursorRect.sizeDelta = new Vector2(10f, 10f);
            cursorRect.pivot = new Vector2(0.5f, 0.5f);
            cursor.raycastTarget = false;

            var hue = MakeImage("Hue", top, Color.white);
            var hueElement = hue.gameObject.AddComponent<LayoutElement>();
            hueElement.preferredWidth = 28f;

            var swatch = MakeImage("Swatch", top, Color.white);
            var swatchElement = swatch.gameObject.AddComponent<LayoutElement>();
            swatchElement.flexibleWidth = 1f;
            swatch.raycastTarget = false;

            // 아래: HEX / R / G / B 입력
            var fields = MakeRect("PickerFields", parent);
            SetHeight(fields, 38f);

            var fieldLayout = fields.gameObject.AddComponent<HorizontalLayoutGroup>();
            fieldLayout.spacing = 8f;
            fieldLayout.childControlWidth = true;
            fieldLayout.childControlHeight = true;
            fieldLayout.childForceExpandWidth = false;
            fieldLayout.childAlignment = TextAnchor.MiddleLeft;

            var hexField = MakeInputField(fields, "HEX", 150f, TMP_InputField.CharacterValidation.None);
            var rField = MakeInputField(fields, "R", 90f, TMP_InputField.CharacterValidation.Integer);
            var gField = MakeInputField(fields, "G", 90f, TMP_InputField.CharacterValidation.Integer);
            var bField = MakeInputField(fields, "B", 90f, TMP_InputField.CharacterValidation.Integer);

            var picker = new ColorPickerControl(square, cursor, hue, swatch,
                hexField, rField, gField, bField);
            picker.OnChanged = onChanged;
            return picker;
        }

        private void AddSectionLabel(Transform parent, string title)
        {
            var label = MakeText("Section", parent, title, 21, ButtonAccentColor * 1.6f,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(label.rectTransform, 34f);
        }

        /// <summary>제목 · 슬라이더 · 값이 한 줄에 들어가는 게임 옵션 스타일 행.</summary>
        private void AddSlider(Transform parent, string title, float min, float max, string format,
            Func<WeaponAuraProfile, float> get, Action<WeaponAuraProfile, float> set,
            bool rebuild = false)
        {
            var rowGo = MakeRect("Row_" + title, parent);
            SetHeight(rowGo, 36f);

            var layout = rowGo.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var label = MakeText("Label", rowGo, title, 19, TextColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(label.rectTransform, 130f);

            var slider = MakeSlider(rowGo, min, max);
            var sliderElement = slider.gameObject.AddComponent<LayoutElement>();
            sliderElement.flexibleWidth = 1f;

            var valueText = MakeText("Value", rowGo, "0", 19, DimTextColor, TextAlignmentOptions.MidlineRight);
            SetWidth(valueText.rectTransform, 90f);

            var row = new SliderRow
            {
                Slider = slider,
                ValueText = valueText,
                Format = format,
                Get = get,
                Set = set,
                NeedsRebuild = rebuild,
            };

            slider.onValueChanged.AddListener(value => OnSliderChanged(row, value));
            _rows.Add(row);
        }

        // ── 꼬리말 (저장 · 프리셋) ────────────────────────────────────

        private void BuildFooter(Transform parent)
        {
            var save = MakeRect("SaveRow", parent);
            SetHeight(save, 46f);

            var saveLayout = save.gameObject.AddComponent<HorizontalLayoutGroup>();
            saveLayout.spacing = 10f;
            saveLayout.childControlWidth = true;
            saveLayout.childControlHeight = true;
            saveLayout.childForceExpandWidth = false;

            MakeButton(save, L.Action.Save, 150f, SaveCurrent, ButtonAccentColor);
            MakeButton(save, L.Action.Random, 150f, RandomizeCurrent, ButtonColor);
            MakeButton(save, L.Action.ResetDefaults, 190f, ResetDefaults, ButtonColor);

            var spacer = MakeRect("Spacer", save);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _saveHintText = MakeText("SaveHint", save, "", 17, DimTextColor,
                TextAlignmentOptions.MidlineRight);
            SetWidth(_saveHintText.rectTransform, 420f);
        }
    }
}
