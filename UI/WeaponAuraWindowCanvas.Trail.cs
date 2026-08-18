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
    /// "탄환 잔상" 탭.
    ///
    /// 무기 오라 탭과 구성은 같지만(왼쪽 미리보기·등급 선택, 오른쪽 조절 항목)
    /// 다루는 값이 다릅니다 — 여기서 고르는 등급은 무기가 아니라 <b>장착한 탄약</b>의 등급입니다.
    /// 미리보기는 실제 3D 무대 대신 꼬리를 그대로 그린 띠 이미지를 씁니다.
    /// 총알은 화면을 스쳐 지나가서 무대에 세워 놓고 볼 수가 없습니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        /// <summary>잔상 슬라이더 한 줄. 잔상 프로필의 필드 하나와 묶입니다.</summary>
        private class TrailSliderRow
        {
            public Slider Slider = null!;
            public TextMeshProUGUI ValueText = null!;
            public string Format = "0.00";
            public Func<BulletTrailProfile, float> Get = null!;
            public Action<BulletTrailProfile, float> Set = null!;
        }

        /// <summary>잔상 미리보기 이미지 크기</summary>
        private const int TrailPreviewWidth = 320;
        private const int TrailPreviewHeight = 64;

        /// <summary>미리보기 총알이 가로를 한 번 가로지르는 데 걸리는 시간(초).</summary>
        private const float TrailPreviewSpan = 0.9f;

        /// <summary>한 바퀴가 끝나고 다시 출발하기 전 쉬는 시간(초).</summary>
        private const float TrailPreviewGap = 0.35f;

        private readonly List<TrailSliderRow> _trailRows = new List<TrailSliderRow>();
        private readonly List<Button> _trailGradeButtons = new List<Button>();

        private readonly List<KeyValuePair<EffectScope, Button>> _trailScopeButtons =
            new List<KeyValuePair<EffectScope, Button>>();

        private RawImage? _trailPreviewImage;
        private Texture2D? _trailPreviewTexture;
        private Color32[]? _trailPreviewPixels;
        private TextMeshProUGUI? _trailStatusText;

        private ColorPickerControl? _trailPickerHead;
        private ColorPickerControl? _trailPickerTail;

        private Button? _trailEnableButton;
        private Button? _trailGradeToggleButton;
        private Button? _trailGlowButton;

        /// <summary>편집 중인 탄환 등급의 인덱스</summary>
        private int _editingTrailGrade;

        /// <summary>지금 장착한 탄환의 등급을 따라갈지</summary>
        private bool _followAmmo = true;

        /// <summary>마지막으로 상태 줄에 반영한 탄약 (매 프레임 문자열을 다시 만들지 않기 위해)</summary>
        private int _lastShownAmmoRevision = int.MinValue;

        /// <summary>
        /// 미리보기 총알이 출발한 뒤 흐른 시간(초).
        ///
        /// 스케일이 걸리지 않은 시간을 씁니다 — 창이 떠 있는 동안 게임은 시간이 멈춘 상태라
        /// <c>Time.deltaTime</c>은 0이고, 그러면 미리보기가 한 장에서 멈춰 버립니다.
        /// </summary>
        private float _trailPreviewTime;

        // ── 구성 ────────────────────────────────────────────────────

        private void BuildTrailBody(Transform parent)
        {
            var body = MakeRect("TrailBody", parent);
            _trailBody = body.gameObject;

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

            BuildTrailLeftColumn(body);
            _trailSections = BuildSectionedColumn(body);
            BuildTrailControls(_trailSections.Basic.transform);
            BuildTrailAdvanced(_trailSections.Advanced.transform);
            _trailSections.Select(false);

            // 처음에는 무기 오라 탭이 보입니다. SelectTab이 다시 정리합니다.
            body.gameObject.SetActive(false);
        }

        private void BuildTrailLeftColumn(Transform parent)
        {
            var column = MakeImage("TrailLeft", parent, SectionColor).rectTransform;
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

            var frame = MakeImage("TrailPreviewFrame", column, new Color(0f, 0f, 0f, 0.75f)).rectTransform;
            SetHeight(frame, 150f);

            // 표시 비율을 텍스처 비율(320×64 = 5:1)에 맞춥니다.
            // 어긋나면 늘어난 만큼 꼬리가 실제보다 굵거나 얇아 보입니다.
            var previewGo = MakeRect("TrailPreview", frame);
            previewGo.anchorMin = new Vector2(0f, 0.5f);
            previewGo.anchorMax = new Vector2(1f, 0.5f);
            previewGo.pivot = new Vector2(0.5f, 0.5f);
            previewGo.offsetMin = new Vector2(14f, -36f);
            previewGo.offsetMax = new Vector2(-14f, 36f);

            _trailPreviewImage = previewGo.gameObject.AddComponent<RawImage>();
            _trailPreviewImage.raycastTarget = false;

            _trailStatusText = MakeText("TrailStatus", column, "-", 17, DimTextColor,
                TextAlignmentOptions.TopLeft);
            SetHeight(_trailStatusText.rectTransform, 124f);
            _trailStatusText.enableWordWrapping = true;

            var gradeLabel = MakeText("TrailGradeLabel", column, L.Trail.SectionGrade, 20, TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(gradeLabel.rectTransform, 30f);

            BuildTrailGradeGrid(column);

            var follow = MakeButton(column, L.Trail.FollowAmmo, 0f, () =>
            {
                _followAmmo = true;
                SyncTrailFromProfile();
            }, ButtonAccentColor);

            SetHeight((RectTransform)follow.transform, 42f);
        }

        /// <summary>
        /// 탄환 등급 버튼 격자. 등급은 게임이 굴리는 1~7 고정이라
        /// 무기 오라 쪽처럼 추가·삭제하지 않고 한 번만 만듭니다.
        /// </summary>
        private void BuildTrailGradeGrid(Transform parent)
        {
            int count = BulletTrailProfiles.Count;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)TierColumns));

            var grid = MakeRect("TrailGradeGrid", parent);
            SetHeight(grid, rows * TierCellHeight + (rows - 1) * TierCellSpacing);

            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(TierCellWidth, TierCellHeight);
            layout.spacing = new Vector2(TierCellSpacing, TierCellSpacing);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = TierColumns;

            _trailGradeButtons.Clear();

            for (int i = 0; i < count; i++)
            {
                int index = i;
                var profile = BulletTrailProfiles.Get(index);
                string label = profile != null ? profile.grade.ToString() : "?";

                var button = MakeButton(grid, label, 0f, () =>
                {
                    _followAmmo = false;
                    _editingTrailGrade = index;
                    SyncTrailFromProfile();
                }, ButtonColor);

                _trailGradeButtons.Add(button);
            }
        }

        private void BuildTrailControls(Transform parent)
        {
            _trailRows.Clear();

            AddSectionLabel(parent, L.Section.Display);
            BuildTrailDisplayRow(parent);

            AddSectionLabel(parent, L.Trail.SectionColorHead);
            _trailPickerHead = AddColorPicker(parent, color =>
            {
                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                profile.colorStart = new Color(color.r, color.g, color.b, 1f);
                RefreshTrailPreview();
            });

            AddSectionLabel(parent, L.Trail.SectionColorTail);
            _trailPickerTail = AddColorPicker(parent, color =>
            {
                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                profile.colorEnd = new Color(color.r, color.g, color.b, 1f);
                RefreshTrailPreview();
            });

            AddSectionLabel(parent, L.Trail.SectionShape);
            AddTrailSlider(parent, L.Trail.FieldLength, 0.03f, 0.8f, "0.00",
                p => p.length, (p, v) => p.length = v);
            AddTrailSlider(parent, L.Trail.FieldStartWidth, 0.005f, 0.2f, "0.000",
                p => p.startWidth, (p, v) => p.startWidth = v);
        }

        /// <summary>
        /// 탄환 잔상의 고급 항목 — 꼬리 굵기 · 투명도 · 밝기 · 발광.
        /// 색과 길이만 맞추려는 사람은 기본 쪽만 보면 됩니다.
        /// </summary>
        private void BuildTrailAdvanced(Transform parent)
        {
            AddSectionLabel(parent, L.Trail.SectionShape);
            AddTrailSlider(parent, L.Trail.FieldEndWidth, 0f, 0.1f, "0.000",
                p => p.endWidth, (p, v) => p.endWidth = v);
            AddTrailSlider(parent, L.Trail.FieldAlpha, 0f, 1f, "0.00",
                p => p.alpha, (p, v) => p.alpha = v);
            AddTrailSlider(parent, L.Trail.FieldIntensity, 0.5f, 3f, "0.00",
                p => p.intensity, (p, v) => p.intensity = v);

            BuildTrailGlowRow(parent);
        }

        /// <summary>전체 켜기/끄기 · 적용 대상 · 이 등급 켜기/끄기</summary>
        private void BuildTrailDisplayRow(Transform parent)
        {
            var enableRow = MakeRect("TrailEnableRow", parent);
            SetHeight(enableRow, 40f);

            var enableLayout = enableRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            enableLayout.childControlWidth = true;
            enableLayout.childControlHeight = true;
            enableLayout.childForceExpandWidth = true;

            _trailEnableButton = MakeButton(enableRow, "", 0f, () =>
            {
                BulletTrailSettings.SetEnabled(!BulletTrailSettings.Enabled);
                RefreshTrailDisplayRow();
            }, ButtonAccentColor);

            var scopeRow = MakeRect("TrailScopeRow", parent);
            SetHeight(scopeRow, 40f);

            var scopeLayout = scopeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            scopeLayout.spacing = 8f;
            scopeLayout.childControlWidth = true;
            scopeLayout.childControlHeight = true;
            scopeLayout.childForceExpandWidth = false;
            scopeLayout.childAlignment = TextAnchor.MiddleLeft;

            var scopeLabel = MakeText("TrailScopeLabel", scopeRow, L.Trail.Scope, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(scopeLabel.rectTransform, 100f);

            _trailScopeButtons.Clear();
            AddTrailScopeButton(scopeRow, L.Trail.ScopePlayer, EffectScope.PlayerOnly);
            AddTrailScopeButton(scopeRow, L.Trail.ScopeEveryone, EffectScope.Everyone);

            var gradeRow = MakeRect("TrailGradeToggleRow", parent);
            SetHeight(gradeRow, 40f);

            var gradeLayout = gradeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            gradeLayout.childControlWidth = true;
            gradeLayout.childControlHeight = true;
            gradeLayout.childForceExpandWidth = true;

            _trailGradeToggleButton = MakeButton(gradeRow, "", 0f, ToggleTrailGradeEnabled, ButtonAccentColor);
        }

        private void AddTrailScopeButton(Transform parent, string label, EffectScope scope)
        {
            var button = MakeButton(parent, label, 130f, () =>
            {
                BulletTrailSettings.SetScope(scope);
                RefreshTrailDisplayRow();
            }, ButtonColor);

            _trailScopeButtons.Add(new KeyValuePair<EffectScope, Button>(scope, button));
        }

        private void BuildTrailGlowRow(Transform parent)
        {
            var row = MakeRect("TrailGlowRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            _trailGlowButton = MakeButton(row, "", 0f, ToggleTrailGlow, ButtonColor);
        }

        private void AddTrailSlider(Transform parent, string title, float min, float max, string format,
            Func<BulletTrailProfile, float> get, Action<BulletTrailProfile, float> set)
        {
            var rowGo = MakeRect("TrailRow_" + title, parent);
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

            var row = new TrailSliderRow
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

                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                row.Set(profile, value);
                row.ValueText.text = value.ToString(row.Format);
                RefreshTrailPreview();
            });

            _trailRows.Add(row);
        }

        // ── 값 연동 ─────────────────────────────────────────────────

        private BulletTrailProfile? CurrentTrailProfile()
        {
            return BulletTrailProfiles.Get(_editingTrailGrade);
        }

        /// <summary>잔상 탭의 모든 항목을 편집 중인 등급의 값으로 채웁니다.</summary>
        private void SyncTrailFromProfile()
        {
            // 장착 탄환을 따라가는 중이면 편집 대상부터 맞춥니다.
            if (_followAmmo && BulletTrailSystem.CurrentAmmoQuality >= 0)
                _editingTrailGrade = BulletTrailProfiles.IndexOfQuality(BulletTrailSystem.CurrentAmmoQuality);

            _editingTrailGrade = Mathf.Clamp(_editingTrailGrade, 0, Mathf.Max(0, BulletTrailProfiles.Count - 1));

            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            _suppressCallbacks = true;
            try
            {
                foreach (var row in _trailRows)
                {
                    float value = row.Get(profile);
                    row.Slider.SetValueWithoutNotify(value);
                    row.ValueText.text = value.ToString(row.Format);
                }

                _trailPickerHead?.SetColor(profile.colorStart);
                _trailPickerTail?.SetColor(profile.colorEnd);
            }
            finally
            {
                _suppressCallbacks = false;
            }

            if (_headerText != null)
                _headerText.text = string.Format(L.Window.TitleTrail, profile.grade, profile.name);

            HighlightTrailGradeButton();
            RefreshTrailDisplayRow();
            RefreshTrailPreview();
            RefreshTrailStatus(force: true);
        }

        /// <summary>잔상 탭이 떠 있는 동안 매 프레임 — 미리보기 총알을 굴리고 상태 줄을 챙깁니다.</summary>
        private void UpdateTrailTab()
        {
            if (_followAmmo)
            {
                int quality = BulletTrailSystem.CurrentAmmoQuality;
                if (quality >= 0)
                {
                    int index = BulletTrailProfiles.IndexOfQuality(quality);
                    if (index != _editingTrailGrade)
                    {
                        _editingTrailGrade = index;
                        SyncTrailFromProfile();
                        return;
                    }
                }
            }

            // 창이 떠 있는 동안 게임은 시간이 멈춰 있으므로 deltaTime은 0입니다.
            _trailPreviewTime += Time.unscaledDeltaTime;
            RefreshTrailPreview();

            RefreshTrailStatus(force: false);
        }

        private void RefreshTrailStatus(bool force)
        {
            if (_trailStatusText == null)
                return;

            int quality = BulletTrailSystem.CurrentAmmoQuality;

            // 탄약이 그대로면 같은 문자열을 매 프레임 다시 만들 이유가 없습니다.
            if (!force && BulletTrailSystem.AmmoRevision == _lastShownAmmoRevision)
                return;

            _lastShownAmmoRevision = BulletTrailSystem.AmmoRevision;

            var profile = CurrentTrailProfile();
            int editingGrade = profile != null ? profile.grade : 0;

            string ammo = quality >= 0
                ? string.Format(L.Trail.StatusAmmo, BulletTrailSystem.CurrentAmmoName) + "\n" +
                  string.Format(L.Trail.StatusGrade, quality)
                : L.Trail.StatusNoAmmo;

            string follow = _followAmmo ? L.Trail.FollowState : L.Trail.ManualState;

            bool matchesLoaded = quality >= 0 &&
                BulletTrailProfiles.IndexOfQuality(quality) == _editingTrailGrade;

            _trailStatusText.text =
                ammo + "\n" +
                string.Format(L.Trail.StatusEditing, editingGrade, follow) + "\n" +
                (matchesLoaded ? L.Trail.AppliesToLoaded : L.Trail.PreviewOnly);
        }

        private void RefreshTrailDisplayRow()
        {
            bool on = BulletTrailSettings.Enabled;

            if (_trailEnableButton != null)
            {
                // 버튼 글자는 "지금 상태"가 아니라 "누르면 일어나는 일"입니다.
                var label = _trailEnableButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = on ? L.Trail.Off : L.Trail.On;

                if (_trailEnableButton.targetGraphic != null)
                    _trailEnableButton.targetGraphic.color = on ? ButtonAccentColor : ButtonColor;
            }

            foreach (var pair in _trailScopeButtons)
            {
                var button = pair.Value;
                if (button == null || button.targetGraphic == null)
                    continue;

                button.targetGraphic.color =
                    BulletTrailSettings.Scope == pair.Key ? ButtonAccentColor : ButtonColor;
            }

            var profile = CurrentTrailProfile();
            bool gradeOn = profile == null || profile.enabled;

            if (_trailGradeToggleButton != null)
            {
                var label = _trailGradeToggleButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = gradeOn ? L.Trail.GradeOff : L.Trail.GradeOn;

                if (_trailGradeToggleButton.targetGraphic != null)
                    _trailGradeToggleButton.targetGraphic.color = gradeOn ? ButtonAccentColor : ButtonColor;
            }

            if (_trailGlowButton != null)
            {
                bool glow = profile != null && profile.additive;

                var label = _trailGlowButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = glow ? L.Trail.GlowOff : L.Trail.GlowOn;

                if (_trailGlowButton.targetGraphic != null)
                    _trailGlowButton.targetGraphic.color = glow ? ButtonAccentColor : ButtonColor;
            }

            if (!on)
                ShowHint(L.Trail.OffNotice);
            else if (!gradeOn)
                ShowHint(L.Trail.GradeDisabledNotice);
        }

        private void ToggleTrailGradeEnabled()
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            profile.enabled = !profile.enabled;

            UnityEngine.Debug.Log(
                $"[WeaponAura] 탄환 등급 {profile.grade} ({profile.name}) 잔상 " +
                $"{(profile.enabled ? "켜기" : "끄기")}");

            RefreshTrailDisplayRow();
            HighlightTrailGradeButton();
            RefreshTrailPreview();
        }

        private void ToggleTrailGlow()
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            profile.additive = !profile.additive;

            RefreshTrailDisplayRow();
            RefreshTrailPreview();
        }

        /// <summary>선택한 등급 버튼만 밝게, 꺼 둔 등급은 회색으로.</summary>
        private void HighlightTrailGradeButton()
        {
            for (int i = 0; i < _trailGradeButtons.Count; i++)
            {
                var button = _trailGradeButtons[i];
                if (button == null || button.targetGraphic == null)
                    continue;

                var profile = BulletTrailProfiles.Get(i);
                var baseColor = profile != null ? profile.colorStart : Color.gray;

                if (profile != null && !profile.enabled)
                {
                    float grey = (baseColor.r + baseColor.g + baseColor.b) / 3f * 0.35f;
                    baseColor = new Color(grey, grey, grey);
                }

                float shade = i == _editingTrailGrade ? 1f : 0.55f;
                button.targetGraphic.color =
                    new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 0.95f);
            }
        }

        // ── 미리보기 ────────────────────────────────────────────────

        /// <summary>
        /// 총알 한 발이 왼쪽에서 오른쪽으로 날아가고, 그 뒤로 지금 설정한 꼬리가 따라붙습니다.
        ///
        /// 정지된 그라디언트 막대로는 "길이"가 무슨 뜻인지 알 수 없습니다. 실제 잔상은
        /// 총알이 움직여서 생기는 것이라, 움직이는 걸 보여 줘야 슬라이더가 무슨 일을 하는지 읽힙니다.
        ///
        /// 시간 축을 실제와 맞춰 뒀습니다 — 총알이 가로를 <see cref="TrailPreviewSpan"/>초에
        /// 가로지르므로, "길이 0.3초"는 화면 폭의 3분의 1만큼 꼬리가 남는다는 뜻이 됩니다.
        /// </summary>
        private void RefreshTrailPreview()
        {
            if (_trailPreviewImage == null)
                return;

            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            if (!EnsureTrailPreviewTexture())
                return;

            var pixels = _trailPreviewPixels!;
            Array.Clear(pixels, 0, pixels.Length);

            // 꺼 둔 등급은 실제로 아무것도 안 나가므로 미리보기도 비워 둡니다.
            if (profile.enabled && BulletTrailSettings.Enabled)
                DrawTrailFrame(profile, pixels);

            _trailPreviewTexture!.SetPixels32(pixels);
            _trailPreviewTexture.Apply(false);

            _trailPreviewImage.texture = _trailPreviewTexture;
            _trailPreviewImage.color = Color.white;
        }

        private bool EnsureTrailPreviewTexture()
        {
            if (_trailPreviewTexture != null && _trailPreviewPixels != null)
                return true;

            _trailPreviewTexture = new Texture2D(TrailPreviewWidth, TrailPreviewHeight,
                TextureFormat.RGBA32, false)
            {
                name = "WeaponAura_TrailPreview",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            _trailPreviewPixels = new Color32[TrailPreviewWidth * TrailPreviewHeight];
            return true;
        }

        /// <summary>지금 시각의 총알 위치로 한 장을 그립니다.</summary>
        private void DrawTrailFrame(BulletTrailProfile profile, Color32[] pixels)
        {
            float length = Mathf.Max(0.02f, profile.length);

            // 꼬리 길이(px) = 꼬리 수명 × 총알 속도. 총알 속도는 "폭 / Span"입니다.
            float trailPx = Mathf.Max(4f, TrailPreviewWidth * (length / TrailPreviewSpan));

            // 한 바퀴: 총알이 화면을 건너가고(Span) 꼬리가 다 빠져나간 뒤(length) 잠깐 쉽니다.
            float travel = TrailPreviewSpan + length;
            float phase = Mathf.Repeat(_trailPreviewTime, travel + TrailPreviewGap);
            float headX = phase / travel * (TrailPreviewWidth + trailPx);

            float maxWidth = Mathf.Max(profile.startWidth, profile.endWidth, 0.001f);
            float half = TrailPreviewHeight * 0.5f;

            // 실제 잔상과 똑같은 식으로 색·알파를 냅니다. 여기서 밝기를 색에 그냥 곱하면
            // 미리보기만 하얗게 뜨고, 게임에서 보이는 색과 어긋납니다.
            BulletTrailShading.Resolve(profile.colorStart, profile.intensity, profile.alpha,
                out Color headColor, out float headAlpha);
            BulletTrailShading.Resolve(profile.colorEnd, profile.intensity, profile.alpha,
                out Color tailColor, out float tailAlpha);

            float baseAlpha = Mathf.Min(headAlpha, tailAlpha);

            int fromX = Mathf.Max(0, Mathf.FloorToInt(headX - trailPx));
            int toX = Mathf.Min(TrailPreviewWidth - 1, Mathf.CeilToInt(headX));

            for (int x = fromX; x <= toX; x++)
            {
                // 머리에서 얼마나 뒤처져 있는지 (0 = 총알, 1 = 꼬리 끝)
                float t = (headX - x) / trailPx;
                if (t < 0f || t > 1f)
                    continue;

                Color color = Color.Lerp(headColor, tailColor, t);
                float alpha = baseAlpha * TrailAlphaAt(t);
                float bandHalf = Mathf.Max(0.5f,
                    Mathf.Lerp(profile.startWidth, profile.endWidth, t) / maxWidth * (TrailPreviewHeight * 0.42f));

                byte r = ToByte(color.r);
                byte g = ToByte(color.g);
                byte b = ToByte(color.b);

                int fromY = Mathf.Max(0, Mathf.FloorToInt(half - bandHalf));
                int toY = Mathf.Min(TrailPreviewHeight - 1, Mathf.CeilToInt(half + bandHalf));

                for (int y = fromY; y <= toY; y++)
                {
                    float coverage = Mathf.Clamp01(1f - Mathf.Abs(y + 0.5f - half) / bandHalf);
                    float a = alpha * coverage * coverage;
                    if (a <= 0.004f)
                        continue;

                    pixels[y * TrailPreviewWidth + x] = new Color32(r, g, b, ToByte(a));
                }
            }

            DrawTrailHead(profile, pixels, headX, headColor, baseAlpha);
        }

        /// <summary>총알 자체. 이게 없으면 꼬리만 미끄러져서 무엇이 움직이는지 안 보입니다.</summary>
        private void DrawTrailHead(BulletTrailProfile profile, Color32[] pixels,
            float headX, Color headColor, float baseAlpha)
        {
            if (headX < 0f || headX >= TrailPreviewWidth)
                return;

            float half = TrailPreviewHeight * 0.5f;
            float radius = Mathf.Max(2.5f,
                profile.startWidth / Mathf.Max(profile.startWidth, profile.endWidth, 0.001f)
                * (TrailPreviewHeight * 0.42f));

            // 머리만 살짝 밝게 둡니다. 흰색을 많이 섞으면 잔상 색이 무슨 색인지 안 보입니다.
            Color core = Color.Lerp(headColor, Color.white, 0.25f);
            byte r = ToByte(core.r);
            byte g = ToByte(core.g);
            byte b = ToByte(core.b);

            int fromX = Mathf.Max(0, Mathf.FloorToInt(headX - radius));
            int toX = Mathf.Min(TrailPreviewWidth - 1, Mathf.CeilToInt(headX + radius));
            int fromY = Mathf.Max(0, Mathf.FloorToInt(half - radius));
            int toY = Mathf.Min(TrailPreviewHeight - 1, Mathf.CeilToInt(half + radius));

            for (int x = fromX; x <= toX; x++)
            {
                for (int y = fromY; y <= toY; y++)
                {
                    float dx = (x + 0.5f - headX) / radius;
                    float dy = (y + 0.5f - half) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f)
                        continue;

                    float a = Mathf.Max(baseAlpha, 0.85f) * (1f - d) * (1f - d);
                    if (a <= 0.004f)
                        continue;

                    int index = y * TrailPreviewWidth + x;
                    if (pixels[index].a >= ToByte(a))
                        continue;

                    pixels[index] = new Color32(r, g, b, ToByte(a));
                }
            }
        }

        /// <summary>
        /// 꼬리의 투명도 곡선. <c>BulletTrailSystem</c>이 실제 TrailRenderer에 넣는
        /// 그라디언트(0에서 1, 0.45에서 0.55, 끝에서 0)와 같은 모양이어야
        /// 미리보기와 실제가 어긋나지 않습니다.
        /// </summary>
        private static float TrailAlphaAt(float t)
        {
            return t <= 0.45f
                ? Mathf.Lerp(1f, 0.55f, t / 0.45f)
                : Mathf.Lerp(0.55f, 0f, (t - 0.45f) / 0.55f);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        // ── 저장 · 무작위 · 초기화 (꼬리말 버튼에서 호출) ───────────

        /// <summary>
        /// 편집 중인 등급을 무작위 조합으로 바꿉니다.
        /// 색뿐 아니라 길이·굵기·발광까지 함께 굴립니다.
        /// 등급 값·이름과 이 등급을 꺼 뒀는지는 그대로 둡니다.
        /// </summary>
        private void RandomizeCurrentTrail()
        {
            var target = CurrentTrailProfile();
            if (target == null)
                return;

            int seed = UnityEngine.Random.Range(0, int.MaxValue);

            var generated = BulletTrailProfiles.CreateRandom(seed, target.name, target.grade);

            // 꺼 둔 등급을 무작위로 켜지 않습니다 — 끈 데는 이유가 있습니다.
            generated.enabled = target.enabled;

            target.CopyFrom(generated);

            SyncTrailFromProfile();
            ShowHint(string.Format(L.Trail.RandomApplied, target.grade, seed));
        }

        private void ResetTrailDefaults()
        {
            BulletTrailProfiles.ResetToDefaults();
            SyncTrailFromProfile();
            ShowHint(L.Trail.ResetDone);
        }

        /// <summary>창을 닫을 때 미리보기 텍스처를 정리합니다.</summary>
        private void DisposeTrailPreview()
        {
            if (_trailPreviewTexture == null)
                return;

            Destroy(_trailPreviewTexture);
            _trailPreviewTexture = null;
            _trailPreviewPixels = null;

            if (_trailPreviewImage != null)
                _trailPreviewImage.texture = null;
        }
    }
}
