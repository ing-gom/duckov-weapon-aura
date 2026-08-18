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
    /// "근접 참격" 탭.
    ///
    /// 기준은 <b>들고 있는 근접무기의 등급</b>입니다 — 무기 오라와 같은 축이라,
    /// 무기를 감싼 색과 휘두를 때 나는 색이 한 벌로 읽힙니다.
    /// (탄환 잔상·총구 화염은 탄약 등급을 봅니다. 근접무기에는 탄약이 없습니다.)
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private class MeleeSliderRow
        {
            public Slider Slider = null!;
            public TextMeshProUGUI ValueText = null!;
            public string Format = "0.00";
            public Func<MeleeSlashProfile, float> Get = null!;
            public Action<MeleeSlashProfile, float> Set = null!;
        }

        private readonly List<MeleeSliderRow> _meleeRows = new List<MeleeSliderRow>();
        private readonly List<Button> _meleeGradeButtons = new List<Button>();

        private readonly List<KeyValuePair<EffectScope, Button>> _meleeScopeButtons =
            new List<KeyValuePair<EffectScope, Button>>();

        private readonly List<KeyValuePair<MeleeSlashMode, Button>> _meleeModeButtons =
            new List<KeyValuePair<MeleeSlashMode, Button>>();

        private MeleeSlashPreviewStage? _meleeStage;
        private RawImage? _meleePreviewImage;
        private TextMeshProUGUI? _meleeStatusText;

        private ColorPickerControl? _meleePickerSlash;
        private ColorPickerControl? _meleePickerInner;
        private ColorPickerControl? _meleePickerOuter;

        private TextMeshProUGUI? _meleeShapeLabel;
        private TextMeshProUGUI? _meleeSlashShapeLabel;
        private Button? _meleeStretchButton;
        private Button? _meleeEnableButton;
        private Button? _meleeGradeToggleButton;

        private int _editingMeleeGrade;
        private bool _followWeaponMelee = true;
        private int _lastShownMeleeRevision = int.MinValue;
        private string _lastShownMeleeNote = "";

        // ── 구성 ────────────────────────────────────────────────────

        private void BuildMeleeBody(Transform parent)
        {
            var body = MakeRect("MeleeBody", parent);
            _meleeBody = body.gameObject;

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

            BuildMeleeLeftColumn(body);
            _meleeSections = BuildSectionedColumn(body);
            BuildMeleeControls(_meleeSections.Basic.transform);
            BuildMeleeAdvanced(_meleeSections.Advanced.transform);
            _meleeSections.Select(false);

            body.gameObject.SetActive(false);
        }

        private void BuildMeleeLeftColumn(Transform parent)
        {
            var column = MakeImage("MeleeLeft", parent, SectionColor).rectTransform;
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

            var frame = MakeImage("MeleePreviewFrame", column, new Color(0f, 0f, 0f, 0.75f)).rectTransform;
            SetHeight(frame, 268f);

            // 렌더 텍스처 비율(512×340 ≒ 3:2)에 맞춥니다. 어긋나면 이펙트가 눌려 보입니다.
            var previewGo = MakeRect("MeleePreview", frame);
            previewGo.anchorMin = new Vector2(0f, 0.5f);
            previewGo.anchorMax = new Vector2(1f, 0.5f);
            previewGo.pivot = new Vector2(0.5f, 0.5f);
            previewGo.offsetMin = new Vector2(14f, -120f);
            previewGo.offsetMax = new Vector2(-14f, 120f);

            _meleePreviewImage = previewGo.gameObject.AddComponent<RawImage>();
            _meleePreviewImage.raycastTarget = false;

            var note = MakeText("MeleePreviewNote", column, L.Melee.PreviewNote, 15, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(note.rectTransform, 22f);

            _meleeStatusText = MakeText("MeleeStatus", column, "-", 17, DimTextColor,
                TextAlignmentOptions.TopLeft);
            SetHeight(_meleeStatusText.rectTransform, 82f);
            _meleeStatusText.enableWordWrapping = true;

            var gradeLabel = MakeText("MeleeGradeLabel", column, L.Melee.SectionGrade, 20, TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(gradeLabel.rectTransform, 30f);

            BuildMeleeGradeGrid(column);

            var follow = MakeButton(column, L.Melee.FollowWeapon, 0f, () =>
            {
                _followWeaponMelee = true;
                SyncMeleeFromProfile();
            }, ButtonAccentColor);

            SetHeight((RectTransform)follow.transform, 42f);
        }

        private void BuildMeleeGradeGrid(Transform parent)
        {
            int count = MeleeSlashProfiles.Count;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)TierColumns));

            var grid = MakeRect("MeleeGradeGrid", parent);
            SetHeight(grid, rows * TierCellHeight + (rows - 1) * TierCellSpacing);

            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(TierCellWidth, TierCellHeight);
            layout.spacing = new Vector2(TierCellSpacing, TierCellSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = TierColumns;

            _meleeGradeButtons.Clear();

            for (int i = 0; i < count; i++)
            {
                int index = i;
                var profile = MeleeSlashProfiles.Get(index);
                string label = profile != null ? profile.grade.ToString() : "?";

                var button = MakeButton(grid, label, 0f, () =>
                {
                    _followWeaponMelee = false;
                    _editingMeleeGrade = index;
                    SyncMeleeFromProfile();
                }, ButtonColor);

                _meleeGradeButtons.Add(button);
            }
        }

        private void BuildMeleeControls(Transform parent)
        {
            _meleeRows.Clear();

            AddSectionLabel(parent, L.Section.Display);
            BuildMeleeDisplayRow(parent);

            // 참격 호 자체의 색. 흩뿌림과 따로 둡니다 — 알갱이가 한 장뿐이라
            // "안쪽~바깥" 두 색이 성립하지 않고, 실제로 흰색만 나왔습니다.
            AddSectionLabel(parent, L.Melee.SectionColorSlash);
            _meleePickerSlash = AddColorPicker(parent, color =>
            {
                var profile = CurrentMeleeProfile();
                if (profile == null)
                    return;

                profile.slashColor = new Color(color.r, color.g, color.b, 1f);
                HighlightMeleeGradeButton();
            });

        }

        /// <summary>
        /// 근접 참격의 고급 항목 — 흩날리는 알갱이 전부와 직접 그린 도형.
        /// 참격 색과 모양만 바꾸려는 사람은 기본 쪽만 보면 됩니다.
        /// </summary>
        private void BuildMeleeAdvanced(Transform parent)
        {
            AddSectionLabel(parent, L.Melee.SectionColorInner);
            _meleePickerInner = AddColorPicker(parent, color =>
            {
                var profile = CurrentMeleeProfile();
                if (profile == null)
                    return;

                profile.colorInner = new Color(color.r, color.g, color.b, 1f);
                HighlightMeleeGradeButton();
            });

            AddSectionLabel(parent, L.Melee.SectionColorOuter);
            _meleePickerOuter = AddColorPicker(parent, color =>
            {
                var profile = CurrentMeleeProfile();
                if (profile == null)
                    return;

                profile.colorOuter = new Color(color.r, color.g, color.b, 1f);
                HighlightMeleeGradeButton();
            });

            AddSectionLabel(parent, L.Melee.SectionLook);
            BuildMeleeShapeRow(parent);
            BuildMeleePresetRow(parent);
            BuildShapeEditor(parent);

            AddSectionLabel(parent, L.Melee.SectionSlash);

            // 휘두를 때 그려지는 호의 모양 자체를 갈아 끼웁니다.
            BuildMeleeSlashShapeRow(parent);

            AddMeleeSlider(parent, L.Melee.FieldSlashAlpha, 0f, 1f, "0.00",
                p => p.slashAlpha, (p, v) => p.slashAlpha = v);
            AddMeleeSlider(parent, L.Melee.FieldSlashIntensity, 0.5f, 3f, "0.00",
                p => p.slashIntensity, (p, v) => p.slashIntensity = v);

            AddSectionLabel(parent, L.Melee.SectionSparks);
            AddMeleeSlider(parent, L.Melee.FieldAlpha, 0f, 1f, "0.00",
                p => p.alpha, (p, v) => p.alpha = v);
            AddMeleeSlider(parent, L.Melee.FieldIntensity, 0.5f, 3f, "0.00",
                p => p.intensity, (p, v) => p.intensity = v);
            AddMeleeSlider(parent, L.Melee.FieldSparkCount, 0f, 40f, "0",
                p => p.sparkCount, (p, v) => p.sparkCount = Mathf.RoundToInt(v));
            // 하트·별은 파편보다 훨씬 커야 모양이 보입니다. 넉넉히 열어 둡니다.
            AddMeleeSlider(parent, L.Melee.FieldSparkSize, 0.02f, 2f, "0.000",
                p => p.sparkSize, (p, v) => p.sparkSize = v);
            // 참격 궤적 위에서 생기므로, 이건 "호에서 얼마나 벗어나는지"입니다.
            // 크게 올리면 그 순간 참격과 무관한 폭발이 됩니다.
            AddMeleeSlider(parent, L.Melee.FieldSparkDistance, 0f, 3f, "0.00",
                p => p.sparkDistance, (p, v) => p.sparkDistance = v);
            // 알갱이를 얹을 고리의 반지름 배율. 참격 그림이 판 안에서 차지하는 비율은
            // 텍스처 나름이라, 알갱이가 호에서 떠 보이면 이걸로 당기거나 밀면 됩니다.
            AddMeleeSlider(parent, L.Melee.FieldSparkRing, 0f, 1.5f, "0.00",
                p => p.sparkRing, (p, v) => p.sparkRing = v);
            // 호가 판 위 어느 쪽에 그려져 있는지는 텍스처 나름입니다. 비껴 있으면 돌려 맞춥니다.
            AddMeleeSlider(parent, L.Melee.FieldSlashFacing, -180f, 180f, "0",
                p => p.slashFacing, (p, v) => p.slashFacing = v);
            // 호가 그려지는 동안 나눠 뿌리는 시간. 0에 가까우면 시작점에 뭉칩니다.
            AddMeleeSlider(parent, L.Melee.FieldSparkWindow, 0.01f, 0.6f, "0.00",
                p => p.sparkEmitWindow, (p, v) => p.sparkEmitWindow = v);
            // 0이면 참격 표면에 수직으로만, 1이면 제멋대로 흩어집니다.
            AddMeleeSlider(parent, L.Melee.FieldSparkScatter, 0f, 1f, "0.00",
                p => p.sparkScatter, (p, v) => p.sparkScatter = v);
            // 부채꼴 전체 폭. 참격을 지우는 `흩뿌림만` 모드에서만 쓰입니다
            // (남겨 두는 모드에서는 참격 호 자체가 뿌려지는 자리라 부채꼴이 필요 없습니다).
            AddMeleeSlider(parent, L.Melee.FieldSparkArc, 0f, 180f, "0",
                p => p.sparkArc, (p, v) => p.sparkArc = v);
            // 중력 배율이 아니라 최종 높이(m). 바닥을 뚫거나 끝없이 솟는 조합이 나올 수 없습니다.
            AddMeleeSlider(parent, L.Melee.FieldSparkRise, -2f, 2f, "0.00",
                p => p.sparkRise, (p, v) => p.sparkRise = v);
            AddMeleeSlider(parent, L.Melee.FieldSparkSpin, -720f, 720f, "0",
                p => p.sparkSpin, (p, v) => p.sparkSpin = v);
            AddMeleeSlider(parent, L.Melee.FieldSparkDuration, 0.05f, 1.5f, "0.00",
                p => p.sparkDuration, (p, v) => p.sparkDuration = v);

            BuildMeleeStretchRow(parent);
        }

        /// <summary>
        /// 흩뿌림 모양 고르기. 총구 화염과 같은 목록(내장 도형 + 직접 그린 도형 +
        /// assets/vfx_textures의 PNG)을 ◀ ▶ 로 넘깁니다.
        /// </summary>
        private void BuildMeleeShapeRow(Transform parent)
        {
            var row = MakeRect("MeleeShapeRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            MakeButton(row, L.Muzzle.ShapePrev, 56f, () => CycleMeleeShape(-1), ButtonColor);

            _meleeShapeLabel = MakeText("MeleeShapeName", row, "-", 19, TextColor,
                TextAlignmentOptions.Center);
            _meleeShapeLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MakeButton(row, L.Muzzle.ShapeNext, 56f, () => CycleMeleeShape(1), ButtonColor);

            // 폴더에 PNG를 새로 넣었을 때 게임을 껐다 켜지 않아도 되게.
            MakeButton(row, L.Muzzle.ShapeRescan, 56f, () =>
            {
                WeaponAuraResources.GetTextureNames(refresh: true);
                RefreshMeleeShapeLabel();
            }, ButtonColor);
        }

        /// <summary>
        /// 참격 호의 모양 고르기.
        ///
        /// 게임 참격은 메시로 그려지는 파티클이라 그 메시를 갈아 끼우면 모양이 바뀝니다.
        /// 원본이 있던 평면·크기에 맞춰 만들기 때문에 무기가 달라도 자리를 벗어나지 않습니다.
        /// </summary>
        private void BuildMeleeSlashShapeRow(Transform parent)
        {
            var row = MakeRect("MeleeSlashShapeRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var label = MakeText("MeleeSlashShapeLabel", row, L.Melee.SlashShape, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(label.rectTransform, 100f);

            MakeButton(row, L.Muzzle.ShapePrev, 56f, () => CycleSlashShape(-1), ButtonColor);

            _meleeSlashShapeLabel = MakeText("MeleeSlashShapeName", row, "-", 19, TextColor,
                TextAlignmentOptions.Center);
            _meleeSlashShapeLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MakeButton(row, L.Muzzle.ShapeNext, 56f, () => CycleSlashShape(1), ButtonColor);
        }

        /// <summary>
        /// 참격 판에 씌울 그림 목록. 맨 앞이 "게임 기본"(안 건드림)이고,
        /// 그 뒤로 총구 화염과 같은 목록(내장 도형 · 직접 그린 도형 · PNG)이 이어집니다.
        /// </summary>
        private static List<string> SlashTextureChoices()
        {
            var choices = new List<string> { "" };
            choices.AddRange(MuzzleShapeChoices());
            return choices;
        }

        private void CycleSlashShape(int delta)
        {
            var profile = CurrentMeleeProfile();
            if (profile == null)
                return;

            var choices = SlashTextureChoices();
            int index = choices.IndexOf(profile.slashTexture ?? "");
            if (index < 0)
                index = 0;

            profile.slashTexture = choices[(int)Mathf.Repeat(index + delta, choices.Count)];

            RefreshSlashShapeLabel();

            // 모양은 게임 참격을 지우는 모드에서는 볼 수가 없습니다.
            if (MeleeSlashSettings.Mode == MeleeSlashMode.Replace)
            {
                MeleeSlashSettings.SetMode(MeleeSlashMode.Overlay);
                RefreshMeleeDisplayRow();
            }
        }

        private void RefreshSlashShapeLabel()
        {
            if (_meleeSlashShapeLabel == null)
                return;

            var profile = CurrentMeleeProfile();
            if (profile == null)
                return;

            string picked = profile.slashTexture ?? "";

            if (string.IsNullOrEmpty(picked))
            {
                _meleeSlashShapeLabel.text = L.Melee.SlashGame;
                return;
            }

            _meleeSlashShapeLabel.text =
                Enum.TryParse(picked, out MuzzleFlashShape shape) &&
                Array.IndexOf(MuzzleFlashShapes.All, shape) >= 0
                    ? LocalizedShapeName(shape)
                    : picked;
        }

        private void BuildMeleePresetRow(Transform parent)
        {
            var row = MakeRect("MeleePresetRow", parent);
            SetHeight(row, 38f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            AddMeleePresetButton(row, L.Melee.PresetSlash, MeleeSlashProfiles.PresetKind.Slash);
            AddMeleePresetButton(row, L.Melee.PresetEmber, MeleeSlashProfiles.PresetKind.Ember);
            AddMeleePresetButton(row, L.Melee.PresetPetal, MeleeSlashProfiles.PresetKind.Petal);
        }

        private void AddMeleePresetButton(Transform parent, string label,
            MeleeSlashProfiles.PresetKind kind)
        {
            MakeButton(parent, label, 0f, () =>
            {
                var target = CurrentMeleeProfile();
                if (target == null)
                    return;

                var preset = MeleeSlashProfiles.CreatePreset(kind, target.name, target.grade);
                preset.enabled = target.enabled;
                target.CopyFrom(preset);

                // 흩뿌림은 "색만 바꾸기"에서는 나가지 않습니다. 그 상태로 꽃잎을 고르면
                // 아무 일도 일어나지 않는데, 사용자는 그 연결을 알 도리가 없습니다.
                if (MeleeSlashSettings.Mode == MeleeSlashMode.TintDefault)
                    MeleeSlashSettings.SetMode(MeleeSlashMode.Overlay);

                SyncMeleeFromProfile();
                ShowHint(string.Format(L.Melee.PresetApplied, target.grade, label));
            }, ButtonColor);
        }

        private void BuildMeleeStretchRow(Transform parent)
        {
            var row = MakeRect("MeleeStretchRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            _meleeStretchButton = MakeButton(row, "", 0f, () =>
            {
                var profile = CurrentMeleeProfile();
                if (profile == null)
                    return;

                profile.sparkStretch = !profile.sparkStretch;
                RefreshMeleeDisplayRow();
            }, ButtonColor);
        }

        private void CycleMeleeShape(int delta)
        {
            var profile = CurrentMeleeProfile();
            if (profile == null)
                return;

            var choices = MuzzleShapeChoices();
            if (choices.Count == 0)
                return;

            string current = string.IsNullOrEmpty(profile.textureName)
                ? profile.shape.ToString()
                : profile.textureName;

            int index = choices.IndexOf(current);
            if (index < 0)
                index = 0;

            index = (int)Mathf.Repeat(index + delta, choices.Count);
            string picked = choices[index];

            // 내장 도형 이름이면 도형으로, 아니면 파일 이름으로 봅니다.
            if (Enum.TryParse(picked, out MuzzleFlashShape shape) &&
                Array.IndexOf(MuzzleFlashShapes.All, shape) >= 0)
            {
                profile.shape = shape;
                profile.textureName = "";
            }
            else
            {
                profile.textureName = picked;
            }

            RefreshMeleeShapeLabel();

            // 같은 이유로, 모양을 고르면 그게 보이는 모드로 옮겨 줍니다.
            if (MeleeSlashSettings.Mode == MeleeSlashMode.TintDefault)
            {
                MeleeSlashSettings.SetMode(MeleeSlashMode.Overlay);
                RefreshMeleeDisplayRow();
            }
        }

        private void RefreshMeleeShapeLabel()
        {
            if (_meleeShapeLabel == null)
                return;

            var profile = CurrentMeleeProfile();
            if (profile == null)
                return;

            _meleeShapeLabel.text = string.IsNullOrEmpty(profile.textureName)
                ? LocalizedShapeName(profile.shape)
                : profile.textureName;
        }

        private void BuildMeleeDisplayRow(Transform parent)
        {
            var enableRow = MakeRect("MeleeEnableRow", parent);
            SetHeight(enableRow, 40f);

            var enableLayout = enableRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            enableLayout.childControlWidth = true;
            enableLayout.childControlHeight = true;
            enableLayout.childForceExpandWidth = true;

            _meleeEnableButton = MakeButton(enableRow, "", 0f, () =>
            {
                MeleeSlashSettings.SetEnabled(!MeleeSlashSettings.Enabled);
                RefreshMeleeDisplayRow();
            }, ButtonAccentColor);

            var scopeRow = MakeRect("MeleeScopeRow", parent);
            SetHeight(scopeRow, 40f);

            var scopeLayout = scopeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            scopeLayout.spacing = 8f;
            scopeLayout.childControlWidth = true;
            scopeLayout.childControlHeight = true;
            scopeLayout.childForceExpandWidth = false;
            scopeLayout.childAlignment = TextAnchor.MiddleLeft;

            var scopeLabel = MakeText("MeleeScopeLabel", scopeRow, L.Melee.Scope, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(scopeLabel.rectTransform, 100f);

            _meleeScopeButtons.Clear();
            AddMeleeScopeButton(scopeRow, L.Melee.ScopePlayer, EffectScope.PlayerOnly);
            AddMeleeScopeButton(scopeRow, L.Melee.ScopeEveryone, EffectScope.Everyone);

            // 색만 바꿀지, 흩뿌림을 더할지, 참격을 통째로 갈아엎을지.
            var modeRow = MakeRect("MeleeModeRow", parent);
            SetHeight(modeRow, 40f);

            var modeLayout = modeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            modeLayout.spacing = 8f;
            modeLayout.childControlWidth = true;
            modeLayout.childControlHeight = true;
            modeLayout.childForceExpandWidth = false;
            modeLayout.childAlignment = TextAnchor.MiddleLeft;

            var modeLabel = MakeText("MeleeModeLabel", modeRow, L.Melee.Mode, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(modeLabel.rectTransform, 100f);

            _meleeModeButtons.Clear();
            AddMeleeModeButton(modeRow, L.Melee.ModeTint, MeleeSlashMode.TintDefault);
            AddMeleeModeButton(modeRow, L.Melee.ModeOverlay, MeleeSlashMode.Overlay);
            AddMeleeModeButton(modeRow, L.Melee.ModeReplace, MeleeSlashMode.Replace);

            var gradeRow = MakeRect("MeleeGradeToggleRow", parent);
            SetHeight(gradeRow, 40f);

            var gradeLayout = gradeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            gradeLayout.childControlWidth = true;
            gradeLayout.childControlHeight = true;
            gradeLayout.childForceExpandWidth = true;

            _meleeGradeToggleButton = MakeButton(gradeRow, "", 0f, ToggleMeleeGradeEnabled, ButtonAccentColor);
        }

        private void AddMeleeModeButton(Transform parent, string label, MeleeSlashMode mode)
        {
            var button = MakeButton(parent, label, 96f, () =>
            {
                MeleeSlashSettings.SetMode(mode);
                RefreshMeleeDisplayRow();
            }, ButtonColor);

            _meleeModeButtons.Add(new KeyValuePair<MeleeSlashMode, Button>(mode, button));
        }

        private void AddMeleeScopeButton(Transform parent, string label, EffectScope scope)
        {
            var button = MakeButton(parent, label, 130f, () =>
            {
                MeleeSlashSettings.SetScope(scope);
                RefreshMeleeDisplayRow();
            }, ButtonColor);

            _meleeScopeButtons.Add(new KeyValuePair<EffectScope, Button>(scope, button));
        }

        private void AddMeleeSlider(Transform parent, string title, float min, float max, string format,
            Func<MeleeSlashProfile, float> get, Action<MeleeSlashProfile, float> set)
        {
            var rowGo = MakeRect("MeleeRow_" + title, parent);
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
            slider.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var valueText = MakeText("Value", rowGo, "0", 19, DimTextColor, TextAlignmentOptions.MidlineRight);
            SetWidth(valueText.rectTransform, 90f);

            var row = new MeleeSliderRow
            {
                Slider = slider,
                ValueText = valueText,
                Format = format,
                Get = get,
                Set = set,
            };

            slider.onValueChanged.AddListener(value =>
            {
                if (_suppressCallbacks)
                    return;

                var profile = CurrentMeleeProfile();
                if (profile == null)
                    return;

                row.Set(profile, value);
                row.ValueText.text = value.ToString(row.Format);
            });

            _meleeRows.Add(row);
        }

        // ── 값 연동 ─────────────────────────────────────────────────

        private MeleeSlashProfile? CurrentMeleeProfile()
        {
            return MeleeSlashProfiles.Get(_editingMeleeGrade);
        }

        private void SyncMeleeFromProfile()
        {
            if (_followWeaponMelee && MeleeSlashSystem.CurrentWeaponQuality >= 0)
                _editingMeleeGrade = MeleeSlashProfiles.IndexOfQuality(MeleeSlashSystem.CurrentWeaponQuality);

            _editingMeleeGrade = Mathf.Clamp(_editingMeleeGrade, 0, Mathf.Max(0, MeleeSlashProfiles.Count - 1));

            var profile = CurrentMeleeProfile();
            if (profile == null)
                return;

            _suppressCallbacks = true;
            try
            {
                foreach (var row in _meleeRows)
                {
                    float value = row.Get(profile);
                    row.Slider.SetValueWithoutNotify(value);
                    row.ValueText.text = value.ToString(row.Format);
                }

                _meleePickerSlash?.SetColor(profile.slashColor);
                _meleePickerInner?.SetColor(profile.colorInner);
                _meleePickerOuter?.SetColor(profile.colorOuter);
            }
            finally
            {
                _suppressCallbacks = false;
            }

            if (_headerText != null)
                _headerText.text = string.Format(L.Window.TitleMelee, profile.grade, profile.name);

            RefreshMeleeShapeLabel();
            RefreshSlashShapeLabel();
            HighlightMeleeGradeButton();
            RefreshMeleeDisplayRow();
            RefreshMeleeStatus(force: true);
        }

        /// <summary>근접 참격 탭이 떠 있는 동안 매 프레임.</summary>
        private void UpdateMeleeTab()
        {
            if (_followWeaponMelee)
            {
                int quality = MeleeSlashSystem.CurrentWeaponQuality;
                if (quality >= 0)
                {
                    int index = MeleeSlashProfiles.IndexOfQuality(quality);
                    if (index != _editingMeleeGrade)
                    {
                        _editingMeleeGrade = index;
                        SyncMeleeFromProfile();
                        return;
                    }
                }
            }

            RefreshMeleePreview();
            RefreshMeleeStatus(force: false);
        }

        /// <summary>
        /// 실제 이펙트를 그대로 찍어서 보여 줍니다 — 게임 참격 프리팹을 세우고, 런타임과 같은
        /// 함수로 색을 입히고, 같은 함수로 흩뿌림을 터뜨립니다.
        /// </summary>
        private void RefreshMeleePreview()
        {
            if (_meleePreviewImage == null)
                return;

            var profile = CurrentMeleeProfile();

            if (profile == null || !profile.enabled || !MeleeSlashSettings.Enabled)
            {
                // 꺼 둔 등급을 밝게 보여 주면 켜져 있는 것으로 읽힙니다.
                _meleePreviewImage.color = new Color(1f, 1f, 1f, 0.15f);
                return;
            }

            _meleeStage ??= new MeleeSlashPreviewStage();
            var live = _meleeStage.Render(profile);

            _meleePreviewImage.texture = live;
            _meleePreviewImage.color = live != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
        }

        private void RefreshMeleeStatus(bool force)
        {
            if (_meleeStatusText == null)
                return;

            // 무대 안내 문구는 무기와 무관하게 바뀝니다(참격 프리팹을 못 구했을 때 등).
            // 그래서 무기 리비전만 보고 건너뛰면 그 문구가 화면에 못 올라옵니다.
            string stageNote = _meleeStage != null ? _meleeStage.Status : "";

            if (!force && MeleeSlashSystem.WeaponRevision == _lastShownMeleeRevision
                && string.Equals(stageNote, _lastShownMeleeNote, StringComparison.Ordinal))
                return;

            _lastShownMeleeRevision = MeleeSlashSystem.WeaponRevision;
            _lastShownMeleeNote = stageNote;

            int quality = MeleeSlashSystem.CurrentWeaponQuality;

            var profile = CurrentMeleeProfile();
            int editingGrade = profile != null ? profile.grade : 0;

            string weapon = quality >= 0
                ? string.Format(L.Melee.StatusWeapon, MeleeSlashSystem.CurrentWeaponName) + "\n" +
                  string.Format(L.Melee.StatusGrade, quality)
                : L.Melee.StatusNoWeapon;

            string follow = _followWeaponMelee ? L.Melee.FollowState : L.Melee.ManualState;

            bool matchesHeld = quality >= 0 &&
                MeleeSlashProfiles.IndexOfQuality(quality) == _editingMeleeGrade;

            _meleeStatusText.text =
                weapon + "\n" +
                string.Format(L.Melee.StatusEditing, editingGrade, follow) + "\n" +
                (matchesHeld ? L.Melee.AppliesToHeld : L.Melee.PreviewOnly);
        }

        private void RefreshMeleeDisplayRow()
        {
            bool on = MeleeSlashSettings.Enabled;

            if (_meleeEnableButton != null)
            {
                var label = _meleeEnableButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = on ? L.Melee.Off : L.Melee.On;

                if (_meleeEnableButton.targetGraphic != null)
                    _meleeEnableButton.targetGraphic.color = on ? ButtonAccentColor : ButtonColor;
            }

            foreach (var pair in _meleeScopeButtons)
            {
                var button = pair.Value;
                if (button == null || button.targetGraphic == null)
                    continue;

                button.targetGraphic.color =
                    MeleeSlashSettings.Scope == pair.Key ? ButtonAccentColor : ButtonColor;
            }

            foreach (var pair in _meleeModeButtons)
            {
                var button = pair.Value;
                if (button == null || button.targetGraphic == null)
                    continue;

                button.targetGraphic.color =
                    MeleeSlashSettings.Mode == pair.Key ? ButtonAccentColor : ButtonColor;
            }

            var profile = CurrentMeleeProfile();
            bool gradeOn = profile == null || profile.enabled;

            if (_meleeGradeToggleButton != null)
            {
                var label = _meleeGradeToggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = gradeOn ? L.Melee.GradeOff : L.Melee.GradeOn;

                if (_meleeGradeToggleButton.targetGraphic != null)
                    _meleeGradeToggleButton.targetGraphic.color = gradeOn ? ButtonAccentColor : ButtonColor;
            }

            if (_meleeStretchButton != null)
            {
                bool stretch = profile != null && profile.sparkStretch;

                var label = _meleeStretchButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = stretch ? L.Muzzle.StretchOff : L.Muzzle.StretchOn;

                if (_meleeStretchButton.targetGraphic != null)
                    _meleeStretchButton.targetGraphic.color = stretch ? ButtonAccentColor : ButtonColor;
            }

            if (!on)
                ShowHint(L.Melee.OffNotice);
            else if (!gradeOn)
                ShowHint(L.Melee.GradeDisabledNotice);
        }

        private void ToggleMeleeGradeEnabled()
        {
            var profile = CurrentMeleeProfile();
            if (profile == null)
                return;

            profile.enabled = !profile.enabled;

            RefreshMeleeDisplayRow();
            HighlightMeleeGradeButton();
        }

        private void HighlightMeleeGradeButton()
        {
            for (int i = 0; i < _meleeGradeButtons.Count; i++)
            {
                var button = _meleeGradeButtons[i];
                if (button == null || button.targetGraphic == null)
                    continue;

                var profile = MeleeSlashProfiles.Get(i);

                // 참격 색이 이 등급의 대표 색입니다 — 실제로 눈에 띄는 것이 그것입니다.
                var baseColor = profile != null ? profile.slashColor : Color.gray;

                if (profile != null && !profile.enabled)
                {
                    float grey = (baseColor.r + baseColor.g + baseColor.b) / 3f * 0.35f;
                    baseColor = new Color(grey, grey, grey);
                }

                float shade = i == _editingMeleeGrade ? 1f : 0.55f;
                button.targetGraphic.color =
                    new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 0.95f);
            }
        }

        // ── 무작위 · 초기화 ─────────────────────────────────────────

        private void RandomizeCurrentMelee()
        {
            var target = CurrentMeleeProfile();
            if (target == null)
                return;

            int seed = UnityEngine.Random.Range(0, int.MaxValue);

            var generated = MeleeSlashProfiles.CreateRandom(seed, target.name, target.grade);
            generated.enabled = target.enabled;

            target.CopyFrom(generated);

            // 굴린 결과의 절반은 흩뿌림 쪽 값입니다. "색만 바꾸기"에서는 그게 나가지 않아서
            // 굴려도 색만 바뀐 것처럼 보입니다.
            if (MeleeSlashSettings.Mode == MeleeSlashMode.TintDefault)
                MeleeSlashSettings.SetMode(MeleeSlashMode.Overlay);

            SyncMeleeFromProfile();
            ShowHint(string.Format(L.Melee.RandomApplied, target.grade, seed));
        }

        private void ResetMeleeDefaults()
        {
            MeleeSlashProfiles.ResetToDefaults();
            SyncMeleeFromProfile();
            ShowHint(L.Melee.ResetDone);
        }

        private void DisposeMeleePreview()
        {
            _meleeStage?.Dispose();
            _meleeStage = null;

            if (_meleePreviewImage != null)
                _meleePreviewImage.texture = null;
        }
    }
}
