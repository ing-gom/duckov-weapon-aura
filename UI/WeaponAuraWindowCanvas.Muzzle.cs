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
    /// "총구 화염" 탭.
    ///
    /// 기준은 탄환 잔상과 같은 <b>장전한 탄약 등급</b>입니다 — 총구에서 터지는 색과
    /// 날아가는 잔상 색이 같은 축이어야 한 흐름으로 읽힙니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private class MuzzleSliderRow
        {
            public Slider Slider = null!;
            public TextMeshProUGUI ValueText = null!;
            public string Format = "0.00";
            public Func<MuzzleFlashProfile, float> Get = null!;
            public Action<MuzzleFlashProfile, float> Set = null!;
        }

        private const int MuzzlePreviewWidth = 240;
        private const int MuzzlePreviewHeight = 120;

        /// <summary>
        /// 미리보기 발사 간격(초). 실제 연사 속도쯤으로 반복해서 보여 줍니다 —
        /// 화염은 0.05초짜리라 한 번만 터뜨리면 눈에 담기지 않고,
        /// 실제로도 연사 중에 보게 되는 모습이 이쪽입니다.
        /// </summary>
        private const float MuzzlePreviewInterval = 0.14f;

        /// <summary>
        /// 미리보기 재생 속도.
        ///
        /// 화염은 0.05~0.13초라 실시간으로 반복하면 초당 일고여덟 번 깜빡이는 점으로만 보이고,
        /// 모양이 눈에 들어오지 않습니다. 느리게 돌리면 상대적인 길이·크기 차이는 그대로면서
        /// 형태를 읽을 수 있습니다.
        /// </summary>
        private const float MuzzlePreviewSpeed = 0.25f;

        private readonly List<MuzzleSliderRow> _muzzleRows = new List<MuzzleSliderRow>();
        private readonly List<Button> _muzzleGradeButtons = new List<Button>();

        private readonly List<KeyValuePair<EffectScope, Button>> _muzzleScopeButtons =
            new List<KeyValuePair<EffectScope, Button>>();

        private MuzzleFlashPreviewStage? _muzzleStage;
        private RawImage? _muzzlePreviewImage;
        private Texture2D? _muzzlePreviewTexture;
        private Color32[]? _muzzlePreviewPixels;
        private TextMeshProUGUI? _muzzleStatusText;

        private ColorPickerControl? _muzzlePickerInner;
        private ColorPickerControl? _muzzlePickerOuter;

        private TextMeshProUGUI? _muzzleShapeLabel;
        private Button? _muzzleStretchButton;
        private Button? _muzzleEnableButton;
        private Button? _muzzleGradeToggleButton;
        private readonly List<KeyValuePair<MuzzleFlashMode, Button>> _muzzleModeButtons =
            new List<KeyValuePair<MuzzleFlashMode, Button>>();

        private int _editingMuzzleGrade;
        private bool _followAmmoMuzzle = true;
        private int _lastShownMuzzleRevision = int.MinValue;

        /// <summary>미리보기 시계. 창이 떠 있는 동안 게임은 멈춰 있으므로 스케일 없는 시간을 씁니다.</summary>
        private float _muzzlePreviewTime;

        // ── 구성 ────────────────────────────────────────────────────

        private void BuildMuzzleBody(Transform parent)
        {
            var body = MakeRect("MuzzleBody", parent);
            _muzzleBody = body.gameObject;

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

            BuildMuzzleLeftColumn(body);
            _muzzleSections = BuildSectionedColumn(body);
            BuildMuzzleControls(_muzzleSections.Basic.transform);
            BuildMuzzleAdvanced(_muzzleSections.Advanced.transform);
            _muzzleSections.Select(false);

            body.gameObject.SetActive(false);
        }

        private void BuildMuzzleLeftColumn(Transform parent)
        {
            var column = MakeImage("MuzzleLeft", parent, SectionColor).rectTransform;
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

            var frame = MakeImage("MuzzlePreviewFrame", column, new Color(0f, 0f, 0f, 0.75f)).rectTransform;
            SetHeight(frame, 268f);

            // 렌더 텍스처 비율(512×340 ≒ 3:2)에 맞춥니다. 어긋나면 이펙트가 눌려 보입니다.
            var previewGo = MakeRect("MuzzlePreview", frame);
            previewGo.anchorMin = new Vector2(0f, 0.5f);
            previewGo.anchorMax = new Vector2(1f, 0.5f);
            previewGo.pivot = new Vector2(0.5f, 0.5f);
            previewGo.offsetMin = new Vector2(14f, -120f);
            previewGo.offsetMax = new Vector2(-14f, 120f);

            _muzzlePreviewImage = previewGo.gameObject.AddComponent<RawImage>();
            _muzzlePreviewImage.raycastTarget = false;

            var note = MakeText("MuzzlePreviewNote", column, L.Muzzle.PreviewNote, 15, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(note.rectTransform, 22f);

            _muzzleStatusText = MakeText("MuzzleStatus", column, "-", 17, DimTextColor,
                TextAlignmentOptions.TopLeft);
            SetHeight(_muzzleStatusText.rectTransform, 82f);
            _muzzleStatusText.enableWordWrapping = true;

            var gradeLabel = MakeText("MuzzleGradeLabel", column, L.Muzzle.SectionGrade, 20, TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(gradeLabel.rectTransform, 30f);

            BuildMuzzleGradeGrid(column);

            var follow = MakeButton(column, L.Muzzle.FollowAmmo, 0f, () =>
            {
                _followAmmoMuzzle = true;
                SyncMuzzleFromProfile();
            }, ButtonAccentColor);

            SetHeight((RectTransform)follow.transform, 42f);
        }

        private void BuildMuzzleGradeGrid(Transform parent)
        {
            int count = MuzzleFlashProfiles.Count;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)TierColumns));

            var grid = MakeRect("MuzzleGradeGrid", parent);
            SetHeight(grid, rows * TierCellHeight + (rows - 1) * TierCellSpacing);

            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(TierCellWidth, TierCellHeight);
            layout.spacing = new Vector2(TierCellSpacing, TierCellSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = TierColumns;

            _muzzleGradeButtons.Clear();

            for (int i = 0; i < count; i++)
            {
                int index = i;
                var profile = MuzzleFlashProfiles.Get(index);
                string label = profile != null ? profile.grade.ToString() : "?";

                var button = MakeButton(grid, label, 0f, () =>
                {
                    _followAmmoMuzzle = false;
                    _editingMuzzleGrade = index;
                    SyncMuzzleFromProfile();
                }, ButtonColor);

                _muzzleGradeButtons.Add(button);
            }
        }

        private void BuildMuzzleControls(Transform parent)
        {
            _muzzleRows.Clear();

            AddSectionLabel(parent, L.Section.Display);
            BuildMuzzleDisplayRow(parent);

            AddSectionLabel(parent, L.Muzzle.SectionColorInner);
            _muzzlePickerInner = AddColorPicker(parent, color =>
            {
                var profile = CurrentMuzzleProfile();
                if (profile == null)
                    return;

                profile.colorInner = new Color(color.r, color.g, color.b, 1f);
                HighlightMuzzleGradeButton();
            });

            AddSectionLabel(parent, L.Muzzle.SectionColorOuter);
            _muzzlePickerOuter = AddColorPicker(parent, color =>
            {
                var profile = CurrentMuzzleProfile();
                if (profile == null)
                    return;

                profile.colorOuter = new Color(color.r, color.g, color.b, 1f);
                HighlightMuzzleGradeButton();
            });

            AddSectionLabel(parent, L.Muzzle.SectionLook);
            BuildMuzzleShapeRow(parent);
            BuildMuzzlePresetRow(parent);
            BuildShapeEditor(parent);

            AddSectionLabel(parent, L.Muzzle.SectionShape);

            // 게임 화염 형태를 그대로 쓰는 모드에서의 배율.
            AddMuzzleSlider(parent, L.Muzzle.FieldSizeScale, 0.2f, 5f, "0.00",
                p => p.sizeScale, (p, v) => p.sizeScale = v);

            // 상한을 0.5m로 뒀더니 끝까지 올려도 작다는 이야기가 나왔습니다.
            // 총·카메라 거리에 따라 체감이 크게 달라지는 값이라 넉넉히 열어 둡니다.
            AddMuzzleSlider(parent, L.Muzzle.FieldSize, 0f, 4f, "0.000",
                p => p.size, (p, v) => p.size = v);
            // 프리셋이 0.4초까지 쓰기 때문에 상한이 0.25면 슬라이더가 값을 잘라 버립니다.
            AddMuzzleSlider(parent, L.Muzzle.FieldDuration, 0.02f, 0.6f, "0.000",
                p => p.duration, (p, v) => p.duration = v);
            AddMuzzleSlider(parent, L.Muzzle.FieldAlpha, 0f, 1f, "0.00",
                p => p.alpha, (p, v) => p.alpha = v);
            AddMuzzleSlider(parent, L.Muzzle.FieldIntensity, 0.5f, 3f, "0.00",
                p => p.intensity, (p, v) => p.intensity = v);

        }

        /// <summary>
        /// 총구 화염의 고급 항목 — 앞으로 튀는 불꽃과 직접 그린 도형.
        /// 색과 크기만 맞추려는 사람은 여기까지 올 이유가 없습니다.
        /// </summary>
        private void BuildMuzzleAdvanced(Transform parent)
        {
            AddSectionLabel(parent, L.Muzzle.SectionSparks);
            AddMuzzleSlider(parent, L.Muzzle.FieldSparkCount, 0f, 20f, "0",
                p => p.sparkCount, (p, v) => p.sparkCount = Mathf.RoundToInt(v));
            // 속도가 아니라 도달 거리입니다 — 수명이 바뀌어도 이 거리는 그대로입니다.
            AddMuzzleSlider(parent, L.Muzzle.FieldSparkDistance, 0f, 6f, "0.00",
                p => p.sparkDistance, (p, v) => p.sparkDistance = v);
            // 하트·별은 불티보다 훨씬 커야 모양이 보입니다. 상한을 좁게 잡았다가
            // "끝까지 올려도 작다"를 세 번 들었습니다. 넉넉히 열어 둡니다.
            AddMuzzleSlider(parent, L.Muzzle.FieldSparkSize, 0.004f, 2f, "0.000",
                p => p.sparkSize, (p, v) => p.sparkSize = v);
            AddMuzzleSlider(parent, L.Muzzle.FieldSparkSpread, 0f, 90f, "0",
                p => p.sparkSpread, (p, v) => p.sparkSpread = v);
            // 중력 배율이 아니라 최종 높이(m). 바닥을 뚫거나 끝없이 솟는 조합이 나올 수 없습니다.
            AddMuzzleSlider(parent, L.Muzzle.FieldSparkRise, -2f, 2f, "0.00",
                p => p.sparkRise, (p, v) => p.sparkRise = v);
            AddMuzzleSlider(parent, L.Muzzle.FieldSparkSpin, -720f, 720f, "0",
                p => p.sparkSpin, (p, v) => p.sparkSpin = v);

            BuildMuzzleStretchRow(parent);
        }

        /// <summary>
        /// 모양 고르기. 내장 도형과 assets/vfx_textures 안의 PNG를 한 줄에 이어 붙여
        /// ◀ ▶ 로 넘깁니다. 목록이 몇 개가 될지 알 수 없어서 격자보다 이쪽이 안전합니다.
        /// </summary>
        private void BuildMuzzleShapeRow(Transform parent)
        {
            var row = MakeRect("MuzzleShapeRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            MakeButton(row, L.Muzzle.ShapePrev, 56f, () => CycleMuzzleShape(-1), ButtonColor);

            _muzzleShapeLabel = MakeText("MuzzleShapeName", row, "-", 19, TextColor,
                TextAlignmentOptions.Center);
            _muzzleShapeLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MakeButton(row, L.Muzzle.ShapeNext, 56f, () => CycleMuzzleShape(1), ButtonColor);

            // 폴더에 PNG를 새로 넣었을 때 게임을 껐다 켜지 않아도 되게.
            MakeButton(row, L.Muzzle.ShapeRescan, 56f, () =>
            {
                WeaponAuraResources.GetTextureNames(refresh: true);
                RefreshMuzzleShapeLabel();
            }, ButtonColor);
        }

        /// <summary>
        /// 프리셋. "화염 대신 하트가 뿅뿅" 같은 연출은 값 대여섯 개를 동시에 맞춰야 나옵니다
        /// (코어 끄기 · 도형 · 중력 뒤집기 · 늘이기 끄기 · 회전). 슬라이더로 하나씩 찾아가라고
        /// 하면 아무도 도달하지 못하는 조합이라 버튼으로 둡니다.
        /// </summary>
        private void BuildMuzzlePresetRow(Transform parent)
        {
            var row = MakeRect("MuzzlePresetRow", parent);
            SetHeight(row, 38f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            AddMuzzlePresetButton(row, L.Muzzle.PresetFlash, MuzzleFlashProfiles.PresetKind.Flash);
            AddMuzzlePresetButton(row, L.Muzzle.PresetHearts, MuzzleFlashProfiles.PresetKind.Hearts);
            AddMuzzlePresetButton(row, L.Muzzle.PresetStardust, MuzzleFlashProfiles.PresetKind.Stardust);
        }

        private void AddMuzzlePresetButton(Transform parent, string label,
            MuzzleFlashProfiles.PresetKind kind)
        {
            MakeButton(parent, label, 0f, () =>
            {
                var target = CurrentMuzzleProfile();
                if (target == null)
                    return;

                var preset = MuzzleFlashProfiles.CreatePreset(kind, target.name, target.grade);
                preset.enabled = target.enabled;
                target.CopyFrom(preset);

                // 도형·알갱이는 "색만 바꾸기"에서는 나가지 않습니다. 그 상태로 하트를 고르면
                // 아무 일도 일어나지 않는데, 사용자는 그 연결을 알 도리가 없습니다.
                if (MuzzleFlashSettings.Mode == MuzzleFlashMode.TintDefault)
                    MuzzleFlashSettings.SetMode(MuzzleFlashMode.Replace);

                SyncMuzzleFromProfile();
                ShowHint(string.Format(L.Muzzle.PresetApplied, target.grade, label));
            }, ButtonColor);
        }

        private void BuildMuzzleStretchRow(Transform parent)
        {
            var row = MakeRect("MuzzleStretchRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            _muzzleStretchButton = MakeButton(row, "", 0f, () =>
            {
                var profile = CurrentMuzzleProfile();
                if (profile == null)
                    return;

                profile.sparkStretch = !profile.sparkStretch;
                RefreshMuzzleDisplayRow();
            }, ButtonColor);
        }

        /// <summary>내장 도형 + 사용자 PNG를 한 줄로 이어 붙인 목록.</summary>
        private static List<string> MuzzleShapeChoices()
        {
            var choices = new List<string>();
            foreach (var shape in MuzzleFlashShapes.All)
                choices.Add(shape.ToString());

            // 직접 그린 도형을 내장 도형 바로 뒤에 둡니다.
            foreach (var drawn in CustomShapes.All)
            {
                if (drawn != null && !string.IsNullOrEmpty(drawn.name))
                    choices.Add(drawn.name);
            }

            foreach (string file in WeaponAuraResources.GetTextureNames())
            {
                if (!string.IsNullOrEmpty(file) && !choices.Contains(file))
                    choices.Add(file);
            }

            return choices;
        }

        private void CycleMuzzleShape(int delta)
        {
            var profile = CurrentMuzzleProfile();
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

            RefreshMuzzleShapeLabel();
            RefreshMuzzlePreview();

            // 같은 이유로, 모양을 고르면 그게 보이는 모드로 옮겨 줍니다.
            if (MuzzleFlashSettings.Mode == MuzzleFlashMode.TintDefault)
            {
                MuzzleFlashSettings.SetMode(MuzzleFlashMode.Replace);
                RefreshMuzzleDisplayRow();
            }
        }

        private void RefreshMuzzleShapeLabel()
        {
            if (_muzzleShapeLabel == null)
                return;

            var profile = CurrentMuzzleProfile();
            if (profile == null)
                return;

            _muzzleShapeLabel.text = string.IsNullOrEmpty(profile.textureName)
                ? LocalizedShapeName(profile.shape)
                : profile.textureName;
        }

        private static string LocalizedShapeName(MuzzleFlashShape shape)
        {
            switch (shape)
            {
                case MuzzleFlashShape.Heart: return L.Muzzle.ShapeHeart;
                case MuzzleFlashShape.Star: return L.Muzzle.ShapeStar;
                case MuzzleFlashShape.Diamond: return L.Muzzle.ShapeDiamond;
                case MuzzleFlashShape.Ring: return L.Muzzle.ShapeRing;
                case MuzzleFlashShape.Sparkle: return L.Muzzle.ShapeSparkle;
                case MuzzleFlashShape.Glow:
                default: return L.Muzzle.ShapeGlow;
            }
        }

        private void BuildMuzzleDisplayRow(Transform parent)
        {
            var enableRow = MakeRect("MuzzleEnableRow", parent);
            SetHeight(enableRow, 40f);

            var enableLayout = enableRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            enableLayout.childControlWidth = true;
            enableLayout.childControlHeight = true;
            enableLayout.childForceExpandWidth = true;

            _muzzleEnableButton = MakeButton(enableRow, "", 0f, () =>
            {
                MuzzleFlashSettings.SetEnabled(!MuzzleFlashSettings.Enabled);
                RefreshMuzzleDisplayRow();
            }, ButtonAccentColor);

            var scopeRow = MakeRect("MuzzleScopeRow", parent);
            SetHeight(scopeRow, 40f);

            var scopeLayout = scopeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            scopeLayout.spacing = 8f;
            scopeLayout.childControlWidth = true;
            scopeLayout.childControlHeight = true;
            scopeLayout.childForceExpandWidth = false;
            scopeLayout.childAlignment = TextAnchor.MiddleLeft;

            var scopeLabel = MakeText("MuzzleScopeLabel", scopeRow, L.Muzzle.Scope, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(scopeLabel.rectTransform, 100f);

            _muzzleScopeButtons.Clear();
            AddMuzzleScopeButton(scopeRow, L.Muzzle.ScopePlayer, EffectScope.PlayerOnly);
            AddMuzzleScopeButton(scopeRow, L.Muzzle.ScopeEveryone, EffectScope.Everyone);

            // 색만 바꿀지, 형태까지 갈아엎을지.
            var modeRow = MakeRect("MuzzleModeRow", parent);
            SetHeight(modeRow, 40f);

            var modeLayout = modeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            modeLayout.spacing = 8f;
            modeLayout.childControlWidth = true;
            modeLayout.childControlHeight = true;
            modeLayout.childForceExpandWidth = false;
            modeLayout.childAlignment = TextAnchor.MiddleLeft;

            var modeLabel = MakeText("MuzzleModeLabel", modeRow, L.Muzzle.Mode, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(modeLabel.rectTransform, 100f);

            _muzzleModeButtons.Clear();
            AddMuzzleModeButton(modeRow, L.Muzzle.ModeTint, MuzzleFlashMode.TintDefault);
            AddMuzzleModeButton(modeRow, L.Muzzle.ModeReplace, MuzzleFlashMode.Replace);
            AddMuzzleModeButton(modeRow, L.Muzzle.ModeOverlay, MuzzleFlashMode.Overlay);

            var gradeRow = MakeRect("MuzzleGradeToggleRow", parent);
            SetHeight(gradeRow, 40f);

            var gradeLayout = gradeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            gradeLayout.childControlWidth = true;
            gradeLayout.childControlHeight = true;
            gradeLayout.childForceExpandWidth = true;

            _muzzleGradeToggleButton = MakeButton(gradeRow, "", 0f, ToggleMuzzleGradeEnabled, ButtonAccentColor);
        }

        private void AddMuzzleModeButton(Transform parent, string label, MuzzleFlashMode mode)
        {
            var button = MakeButton(parent, label, 96f, () =>
            {
                MuzzleFlashSettings.SetMode(mode);
                RefreshMuzzleDisplayRow();
            }, ButtonColor);

            _muzzleModeButtons.Add(new KeyValuePair<MuzzleFlashMode, Button>(mode, button));
        }

        private void AddMuzzleScopeButton(Transform parent, string label, EffectScope scope)
        {
            var button = MakeButton(parent, label, 130f, () =>
            {
                MuzzleFlashSettings.SetScope(scope);
                RefreshMuzzleDisplayRow();
            }, ButtonColor);

            _muzzleScopeButtons.Add(new KeyValuePair<EffectScope, Button>(scope, button));
        }

        private void AddMuzzleSlider(Transform parent, string title, float min, float max, string format,
            Func<MuzzleFlashProfile, float> get, Action<MuzzleFlashProfile, float> set)
        {
            var rowGo = MakeRect("MuzzleRow_" + title, parent);
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

            var row = new MuzzleSliderRow
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

                var profile = CurrentMuzzleProfile();
                if (profile == null)
                    return;

                row.Set(profile, value);
                row.ValueText.text = value.ToString(row.Format);
            });

            _muzzleRows.Add(row);
        }

        // ── 값 연동 ─────────────────────────────────────────────────

        private MuzzleFlashProfile? CurrentMuzzleProfile()
        {
            return MuzzleFlashProfiles.Get(_editingMuzzleGrade);
        }

        private void SyncMuzzleFromProfile()
        {
            if (_followAmmoMuzzle && BulletTrailSystem.CurrentAmmoQuality >= 0)
                _editingMuzzleGrade = MuzzleFlashProfiles.IndexOfQuality(BulletTrailSystem.CurrentAmmoQuality);

            _editingMuzzleGrade = Mathf.Clamp(_editingMuzzleGrade, 0, Mathf.Max(0, MuzzleFlashProfiles.Count - 1));

            var profile = CurrentMuzzleProfile();
            if (profile == null)
                return;

            _suppressCallbacks = true;
            try
            {
                foreach (var row in _muzzleRows)
                {
                    float value = row.Get(profile);
                    row.Slider.SetValueWithoutNotify(value);
                    row.ValueText.text = value.ToString(row.Format);
                }

                _muzzlePickerInner?.SetColor(profile.colorInner);
                _muzzlePickerOuter?.SetColor(profile.colorOuter);
            }
            finally
            {
                _suppressCallbacks = false;
            }

            if (_headerText != null)
                _headerText.text = string.Format(L.Window.TitleMuzzle, profile.grade, profile.name);

            RefreshMuzzleShapeLabel();
            HighlightMuzzleGradeButton();
            RefreshMuzzleDisplayRow();
            RefreshMuzzleStatus(force: true);
        }

        /// <summary>총구 화염 탭이 떠 있는 동안 매 프레임.</summary>
        private void UpdateMuzzleTab()
        {
            if (_followAmmoMuzzle)
            {
                int quality = BulletTrailSystem.CurrentAmmoQuality;
                if (quality >= 0)
                {
                    int index = MuzzleFlashProfiles.IndexOfQuality(quality);
                    if (index != _editingMuzzleGrade)
                    {
                        _editingMuzzleGrade = index;
                        SyncMuzzleFromProfile();
                        return;
                    }
                }
            }

            RefreshMuzzlePreview();

            RefreshMuzzleStatus(force: false);
        }

        private void RefreshMuzzleStatus(bool force)
        {
            if (_muzzleStatusText == null)
                return;

            int quality = BulletTrailSystem.CurrentAmmoQuality;

            if (!force && BulletTrailSystem.AmmoRevision == _lastShownMuzzleRevision)
                return;

            _lastShownMuzzleRevision = BulletTrailSystem.AmmoRevision;

            var profile = CurrentMuzzleProfile();
            int editingGrade = profile != null ? profile.grade : 0;

            string ammo = quality >= 0
                ? string.Format(L.Trail.StatusAmmo, BulletTrailSystem.CurrentAmmoName) + "\n" +
                  string.Format(L.Trail.StatusGrade, quality)
                : L.Trail.StatusNoAmmo;

            string follow = _followAmmoMuzzle ? L.Muzzle.FollowState : L.Muzzle.ManualState;

            bool matchesLoaded = quality >= 0 &&
                MuzzleFlashProfiles.IndexOfQuality(quality) == _editingMuzzleGrade;

            _muzzleStatusText.text =
                ammo + "\n" +
                string.Format(L.Muzzle.StatusEditing, editingGrade, follow) + "\n" +
                (matchesLoaded ? L.Muzzle.AppliesToLoaded : L.Muzzle.PreviewOnly);
        }

        private void RefreshMuzzleDisplayRow()
        {
            bool on = MuzzleFlashSettings.Enabled;

            if (_muzzleEnableButton != null)
            {
                var label = _muzzleEnableButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = on ? L.Muzzle.Off : L.Muzzle.On;

                if (_muzzleEnableButton.targetGraphic != null)
                    _muzzleEnableButton.targetGraphic.color = on ? ButtonAccentColor : ButtonColor;
            }

            foreach (var pair in _muzzleScopeButtons)
            {
                var button = pair.Value;
                if (button == null || button.targetGraphic == null)
                    continue;

                button.targetGraphic.color =
                    MuzzleFlashSettings.Scope == pair.Key ? ButtonAccentColor : ButtonColor;
            }

            foreach (var pair in _muzzleModeButtons)
            {
                var button = pair.Value;
                if (button == null || button.targetGraphic == null)
                    continue;

                button.targetGraphic.color =
                    MuzzleFlashSettings.Mode == pair.Key ? ButtonAccentColor : ButtonColor;
            }

            var profile = CurrentMuzzleProfile();
            bool gradeOn = profile == null || profile.enabled;

            if (_muzzleGradeToggleButton != null)
            {
                var label = _muzzleGradeToggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = gradeOn ? L.Muzzle.GradeOff : L.Muzzle.GradeOn;

                if (_muzzleGradeToggleButton.targetGraphic != null)
                    _muzzleGradeToggleButton.targetGraphic.color = gradeOn ? ButtonAccentColor : ButtonColor;
            }

            if (_muzzleStretchButton != null)
            {
                bool stretch = profile != null && profile.sparkStretch;

                var label = _muzzleStretchButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = stretch ? L.Muzzle.StretchOff : L.Muzzle.StretchOn;

                if (_muzzleStretchButton.targetGraphic != null)
                    _muzzleStretchButton.targetGraphic.color = stretch ? ButtonAccentColor : ButtonColor;
            }

            if (!on)
                ShowHint(L.Muzzle.OffNotice);
            else if (!gradeOn)
                ShowHint(L.Muzzle.GradeDisabledNotice);
        }

        private void ToggleMuzzleGradeEnabled()
        {
            var profile = CurrentMuzzleProfile();
            if (profile == null)
                return;

            profile.enabled = !profile.enabled;

            RefreshMuzzleDisplayRow();
            HighlightMuzzleGradeButton();
        }

        private void HighlightMuzzleGradeButton()
        {
            for (int i = 0; i < _muzzleGradeButtons.Count; i++)
            {
                var button = _muzzleGradeButtons[i];
                if (button == null || button.targetGraphic == null)
                    continue;

                var profile = MuzzleFlashProfiles.Get(i);

                // 등급이 읽히는 곳은 바깥 색입니다 (중심은 어느 등급이든 흰빛에 가깝습니다).
                var baseColor = profile != null ? profile.colorOuter : Color.gray;

                if (profile != null && !profile.enabled)
                {
                    float grey = (baseColor.r + baseColor.g + baseColor.b) / 3f * 0.35f;
                    baseColor = new Color(grey, grey, grey);
                }

                float shade = i == _editingMuzzleGrade ? 1f : 0.55f;
                button.targetGraphic.color =
                    new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 0.95f);
            }
        }

        // ── 미리보기 ────────────────────────────────────────────────

        /// <summary>
        /// 총구에서 화염이 반복해서 터지는 모습.
        ///
        /// 한 번만 터뜨리면 0.05초라 눈에 담기지 않습니다. 실제 연사 속도쯤으로 반복하는 것이
        /// 게임에서 보게 되는 모습과도 같고, "지속 시간"을 늘렸을 때 다음 발사까지 얼마나
        /// 남아 있는지도 그대로 보입니다.
        /// </summary>
        /// <summary>
        /// 실제 이펙트를 그대로 찍어서 보여 줍니다.
        ///
        /// 예전에는 픽셀을 직접 그렸는데, 게임 화염을 "둥근 글로우"로 흉내 내는 수밖에 없어서
        /// 실물과 전혀 달랐습니다. 이제 진짜 프리팹을 무대에 세우고 카메라로 찍습니다.
        /// </summary>
        private void RefreshMuzzlePreview()
        {
            if (_muzzlePreviewImage == null)
                return;

            var profile = CurrentMuzzleProfile();
            if (profile == null)
                return;

            if (profile.enabled && MuzzleFlashSettings.Enabled)
            {
                _muzzleStage ??= new MuzzleFlashPreviewStage();
                var live = _muzzleStage.Render(profile);

                if (live != null)
                {
                    _muzzlePreviewImage.texture = live;
                    _muzzlePreviewImage.color = Color.white;
                    return;
                }
            }

            if (_muzzlePreviewTexture == null || _muzzlePreviewPixels == null)
            {
                _muzzlePreviewTexture = new Texture2D(MuzzlePreviewWidth, MuzzlePreviewHeight,
                    TextureFormat.RGBA32, false)
                {
                    name = "WeaponAura_MuzzlePreview",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                _muzzlePreviewPixels = new Color32[MuzzlePreviewWidth * MuzzlePreviewHeight];
            }

            var pixels = _muzzlePreviewPixels;
            Array.Clear(pixels, 0, pixels.Length);

            if (profile.enabled && MuzzleFlashSettings.Enabled)
                DrawMuzzleFrame(profile, pixels);

            _muzzlePreviewTexture.SetPixels32(pixels);
            _muzzlePreviewTexture.Apply(false);

            _muzzlePreviewImage.texture = _muzzlePreviewTexture;
            _muzzlePreviewImage.color = Color.white;
        }

        private void DrawMuzzleFrame(MuzzleFlashProfile profile, Color32[] pixels)
        {
            float duration = Mathf.Max(0.01f, profile.duration);

            // 지속이 발사 간격보다 길면 화염이 끊기지 않습니다 — 그것도 사실대로 보여 줍니다.
            float cycle = Mathf.Max(MuzzlePreviewInterval, duration + 0.02f);
            float t = Mathf.Repeat(_muzzlePreviewTime, cycle);
            if (t > duration)
                return;

            float life = t / duration;      // 0 = 터지는 순간, 1 = 사그라짐

            BulletTrailShading.Resolve(profile.colorInner, profile.intensity, profile.alpha,
                out Color inner, out float innerAlpha);
            BulletTrailShading.Resolve(profile.colorOuter, profile.intensity, profile.alpha,
                out Color outer, out float outerAlpha);

            float alpha = Mathf.Min(innerAlpha, outerAlpha) * (1f - life * life);

            // 총구는 왼쪽 1/4 지점. 총알은 오른쪽으로 나갑니다.
            float muzzleX = MuzzlePreviewWidth * 0.25f;
            float muzzleY = MuzzlePreviewHeight * 0.5f;

            // 2m를 미리보기 세로의 90%로 잡습니다. 그보다 크면 테두리를 넘어가는데,
            // "화면을 덮을 만큼 크다"도 보여 줘야 할 정보입니다.
            float pixelsPerMetre = MuzzlePreviewHeight * 0.9f / 2f;
            float radius = Mathf.Max(2f, profile.size * 0.5f * pixelsPerMetre)
                           * Mathf.Lerp(0.75f, 1f, Mathf.Min(1f, life * 4f))
                           * Mathf.Lerp(1f, 0.55f, life);

            var mode = MuzzleFlashSettings.Mode;

            // 게임 기본 화염이 남는 모드에서는 그것부터 그립니다. 실측으로 폭 6.5m쯤이라
            // 우리 이펙트보다 훨씬 큽니다 — 그 크기 차이가 보여야 왜 안 보이는지 알 수 있습니다.
            if (mode != MuzzleFlashMode.Replace)
            {
                const float GameFlashMetres = 6.5f;

                // 게임 화염이 남는 모드에서는 두 모드 다 우리 색·크기 배율이 입혀집니다.
                float gameRadius = GameFlashMetres * 0.5f * pixelsPerMetre
                                   * Mathf.Max(0.05f, profile.sizeScale)
                                   * Mathf.Lerp(1f, 0.5f, life);

                DrawSprite(pixels, MuzzleShapeSprite(MuzzleFlashShape.Glow),
                    muzzleX, muzzleY, gameRadius, inner, outer, alpha * 0.85f);
            }

            // 우리 이펙트는 대체 · 함께 표시에서만 나갑니다.
            if (mode == MuzzleFlashMode.TintDefault)
                return;

            // 고른 도형을 그대로 찍습니다 — 미리보기가 하트인데 게임에선 동그라미면 소용없습니다.
            var sprite = MuzzleShapeSprite(profile);

            if (profile.size > 0.005f)
                DrawSprite(pixels, sprite, muzzleX, muzzleY, radius, inner, outer, alpha);

            DrawMuzzleSparks(profile, pixels, sprite, muzzleX, muzzleY,
                pixelsPerMetre, life, duration, outer, alpha);
        }

        /// <summary>
        /// 미리보기용 도형 픽셀. 텍스처를 매 프레임 GetPixel로 훑으면 느려서 한 번만 읽어 둡니다.
        /// </summary>
        private int _muzzleSpriteSize;

        private Color32[]? MuzzleShapeSprite(MuzzleFlashProfile profile)
        {
            return SpritePixels(MuzzleFlashShapes.Resolve(profile.shape, profile.textureName));
        }

        private Color32[]? MuzzleShapeSprite(MuzzleFlashShape shape)
        {
            return SpritePixels(MuzzleFlashShapes.Get(shape));
        }

        /// <summary>
        /// 한 프레임에 게임 화염(글로우)과 우리 도형을 둘 다 그리므로 캐시가 두 장 필요합니다.
        /// 한 칸짜리 캐시로는 매 스프라이트마다 GetPixels32를 다시 부르게 됩니다.
        /// </summary>
        private readonly Dictionary<Texture2D, Color32[]> _muzzleSpriteCache =
            new Dictionary<Texture2D, Color32[]>();

        private Color32[]? SpritePixels(Texture2D? texture)
        {
            if (texture == null)
                return null;

            if (_muzzleSpriteCache.TryGetValue(texture, out var cached))
            {
                _muzzleSpriteSize = texture.width;
                return cached;
            }

            try
            {
                var pixels = texture.GetPixels32();
                _muzzleSpriteCache[texture] = pixels;
                _muzzleSpriteSize = texture.width;
                return pixels;
            }
            catch
            {
                // 읽기 불가 텍스처. 도형 없이 부드러운 원으로 대체합니다.
                return null;
            }
        }

        /// <summary>
        /// 도형 하나를 (cx, cy)에 radius 크기로 찍습니다.
        /// 텍스처는 알파에만 모양이 담겨 있어서, 색은 중심→바깥으로 섞어 입힙니다.
        /// </summary>
        private void DrawSprite(Color32[] pixels, Color32[]? sprite,
            float cx, float cy, float radius, Color inner, Color outer, float alpha)
        {
            if (radius <= 0.5f)
                return;

            int fromX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int toX = Mathf.Min(MuzzlePreviewWidth - 1, Mathf.CeilToInt(cx + radius));
            int fromY = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int toY = Mathf.Min(MuzzlePreviewHeight - 1, Mathf.CeilToInt(cy + radius));

            int size = sprite != null ? Mathf.RoundToInt(Mathf.Sqrt(sprite.Length)) : 0;

            for (int x = fromX; x <= toX; x++)
            {
                float dx = (x + 0.5f - cx) / radius;

                for (int y = fromY; y <= toY; y++)
                {
                    float dy = (y + 0.5f - cy) / radius;

                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float coverage;

                    if (sprite != null && size > 0)
                    {
                        // -1~1 을 텍스처 좌표로
                        int sx = Mathf.Clamp(Mathf.RoundToInt((dx * 0.5f + 0.5f) * (size - 1)), 0, size - 1);
                        int sy = Mathf.Clamp(Mathf.RoundToInt((dy * 0.5f + 0.5f) * (size - 1)), 0, size - 1);
                        coverage = sprite[sy * size + sx].a / 255f;
                    }
                    else
                    {
                        if (distance > 1f)
                            continue;
                        coverage = (1f - distance) * (1f - distance);
                    }

                    float a = alpha * coverage;
                    if (a <= 0.004f)
                        continue;

                    Blend(pixels, x, y, Color.Lerp(inner, outer, Mathf.Clamp01(distance * 1.3f)), a);
                }
            }
        }

        /// <summary>총구 앞으로 튀어 나가는 알갱이.</summary>
        private void DrawMuzzleSparks(MuzzleFlashProfile profile, Color32[] pixels, Color32[]? sprite,
            float cx, float cy, float pixelsPerMetre, float life, float duration,
            Color color, float alpha)
        {
            if (profile.sparkCount <= 0)
                return;

            // 거리는 시간에 비례하고(등속), 높이는 시간의 제곱에 비례합니다(중력).
            float travel = profile.sparkDistance * life * pixelsPerMetre;
            float size = Mathf.Max(1f, profile.sparkSize * 0.5f * pixelsPerMetre);

            // 미리보기는 위가 -y 라 부호를 뒤집습니다.
            float drop = -profile.sparkRise * life * life * pixelsPerMetre;

            float spreadRadians = Mathf.Clamp(profile.sparkSpread, 0f, 90f) * Mathf.Deg2Rad;

            for (int i = 0; i < profile.sparkCount; i++)
            {
                // 개수를 바꿔도 기존 알갱이 위치가 흔들리지 않도록 인덱스로 각도를 정합니다.
                float spread = (i * 0.6180339f % 1f - 0.5f) * 2f * spreadRadians;
                float distance = travel * (0.6f + (i * 0.3819f % 1f) * 0.7f);

                float x = cx + distance * Mathf.Cos(spread);
                float y = cy + distance * Mathf.Sin(spread) + drop;

                DrawSprite(pixels, sprite, x, y, size, color, color, alpha * 0.9f);
            }
        }

        /// <summary>겹치는 곳은 더 밝은 쪽을 남깁니다 (가산 합성을 흉내 냅니다).</summary>
        private static void Blend(Color32[] pixels, int x, int y, Color color, float alpha)
        {
            int index = y * MuzzlePreviewWidth + x;
            var existing = pixels[index];

            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            if (existing.a >= a)
                return;

            pixels[index] = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                a);
        }

        // ── 무작위 · 초기화 ─────────────────────────────────────────

        private void RandomizeCurrentMuzzle()
        {
            var target = CurrentMuzzleProfile();
            if (target == null)
                return;

            int seed = UnityEngine.Random.Range(0, int.MaxValue);

            var generated = MuzzleFlashProfiles.CreateRandom(seed, target.name, target.grade);
            generated.enabled = target.enabled;

            target.CopyFrom(generated);

            // 빛무리가 아닌 모양이 나왔으면 그게 보이는 모드로 옮겨 줍니다.
            // "색만 바꾸기"에서는 도형이 나가지 않아서 굴려도 색만 바뀝니다.
            bool shaped = target.shape != MuzzleFlashShape.Glow
                          || !string.IsNullOrEmpty(target.textureName);

            if (shaped && MuzzleFlashSettings.Mode == MuzzleFlashMode.TintDefault)
                MuzzleFlashSettings.SetMode(MuzzleFlashMode.Replace);

            SyncMuzzleFromProfile();
            ShowHint(string.Format(L.Muzzle.RandomApplied, target.grade, seed));
        }

        private void ResetMuzzleDefaults()
        {
            MuzzleFlashProfiles.ResetToDefaults();
            SyncMuzzleFromProfile();
            ShowHint(L.Muzzle.ResetDone);
        }

        private void DisposeMuzzlePreview()
        {
            _muzzleStage?.Dispose();
            _muzzleStage = null;

            DisposeShapeEditor();

            if (_muzzlePreviewTexture == null)
                return;

            Destroy(_muzzlePreviewTexture);
            _muzzlePreviewTexture = null;
            _muzzlePreviewPixels = null;

            if (_muzzlePreviewImage != null)
                _muzzlePreviewImage.texture = null;
        }
    }
}
