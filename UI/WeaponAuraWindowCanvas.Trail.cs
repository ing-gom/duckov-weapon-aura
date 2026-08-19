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

        /// <summary>
        /// 굵기(m)를 픽셀로 바꾸는 환산.
        ///
        /// 예전에는 "가장 굵은 값" 대비 비율로 그렸습니다. 그러면 머리가 가장 굵을 때
        /// <b>머리 굵기 슬라이더를 움직여도 미리보기가 그대로였습니다</b> — 자기 자신으로
        /// 나누니까요. 게임에서는 분명히 달라지는데 미리보기만 안 변하니 어긋나 보입니다.
        ///
        /// 그래서 하나의 고정 환산을 꼬리·머리·자국이 함께 씁니다. 서로의 굵기 비율이
        /// 실제와 같아지고, 슬라이더도 움직인 만큼 반영됩니다.
        ///
        /// 값 자체는 실제 축척이 아니라 "잘 보이는 정도"로 잡았습니다. 총알은 초당 수십
        /// 미터를 날아가서 실제 축척으로 그리면 꼬리가 화면 밖으로 나가고 머리는 점이 됩니다.
        /// </summary>
        private const float TrailPreviewPixelsPerMeter = 210f;

        /// <summary>띠 밖으로 삐져나가지 않도록 하는 굵기 상한(px).</summary>
        private const float TrailPreviewMaxHalfWidth = TrailPreviewHeight * 0.48f;

        private static float WidthToPixels(float meters)
            => Mathf.Clamp(meters * 0.5f * TrailPreviewPixelsPerMeter, 0.5f, TrailPreviewMaxHalfWidth);

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
        private Button? _trailHideVanillaButton;
        private Button? _trailHeadShapeButton;
        private Button? _trailHeadColorModeButton;
        private ColorPickerControl? _trailPickerHeadColor;
        private CanvasGroup? _trailHeadGroup;
        private TextMeshProUGUI? _trailHeadNotice;
        private Button? _trailHeadEnableButton;

        private Button? _trailGlowVisibleButton;
        private Button? _trailGlowColorModeButton;
        private ColorPickerControl? _trailPickerGlowColor;
        private CanvasGroup? _trailGlowGroup;
        private TextMeshProUGUI? _trailGlowNotice;
        private Button? _trailGlowEnableButton;

        private Button? _trailStyleButton;
        private Button? _trailStampShapeButton;
        private CanvasGroup? _trailStampGroup;
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

            // 방식이 먼저입니다 — 아래 항목 중 무엇이 쓰이는지가 여기서 갈립니다.
            BuildTrailStyleRow(parent);

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

            // 머리는 "원본 궤적 숨기기"를 켰을 때만 그려집니다. 그 사실을 모르면
            // 슬라이더를 아무리 움직여도 화면이 안 바뀌는 것으로 보입니다.
            AddSectionLabel(parent, L.Trail.SectionHead);

            // 안내문과 [켜기] 버튼은 상자 <b>밖</b>에 둡니다. 상자는 조건이 맞지 않으면
            // 입력을 막는데, 그 안에 켜는 버튼을 두면 눌러서 켤 수가 없습니다.
            // 켜는 토글 자체는 기본 탭에 있어서, 여기서 바로 켜지 못하면 탭을 오가야 합니다.
            BuildTrailHeadNoticeRow(parent);

            // 머리 항목은 통째로 한 상자에 담습니다. "원본 궤적 숨기기"가 꺼져 있으면
            // 상자 전체를 흐리게 하고 입력을 막습니다 — 슬라이더가 멀쩡해 보이는데
            // 아무 일도 안 일어나는 게 지금까지의 가장 큰 혼란 원인이었습니다.
            var head = MakeRect("TrailHeadSection", parent);

            var headLayout = head.gameObject.AddComponent<VerticalLayoutGroup>();
            headLayout.spacing = 8f;
            headLayout.childControlWidth = true;
            headLayout.childControlHeight = true;
            headLayout.childForceExpandWidth = true;
            headLayout.childForceExpandHeight = false;

            var headFitter = head.gameObject.AddComponent<ContentSizeFitter>();
            headFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _trailHeadGroup = head.gameObject.AddComponent<CanvasGroup>();

            BuildTrailHeadShapeRow(head);

            // 원본 총알이 0.19m라 위쪽을 넉넉히 열어 둡니다. 0.2에서 잘라 두면
            // 원본과 같은 굵기가 슬라이더 끝에 걸립니다.
            AddTrailSlider(head, L.Trail.FieldHeadWidth, 0f, 0.5f, "0.000",
                p => p.headWidth, (p, v) => p.headWidth = v);
            AddTrailSlider(head, L.Trail.FieldHeadAspect, 0.5f, 6f, "0.0",
                p => p.headAspect, (p, v) => p.headAspect = v);
            AddTrailSlider(head, L.Trail.FieldHeadIntensity, 0.5f, 3f, "0.00",
                p => p.headIntensity, (p, v) => p.headIntensity = v);

            BuildTrailHeadColorRow(head);

            _trailPickerHeadColor = AddColorPicker(head, color =>
            {
                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                profile.headColor = new Color(color.r, color.g, color.b, 1f);

                // 색을 직접 골랐다는 건 따로 쓰겠다는 뜻입니다. 따라가기가 켜져 있으면
                // 방금 고른 색이 화면에 반영되지 않아 고장난 것처럼 보입니다.
                profile.headFollowTrailColor = false;

                RefreshTrailDisplayRow();
                RefreshTrailPreview();
            });

            BuildTrailStampSection(parent);
            BuildTrailGlowSection(parent);

            // 도형 편집기. 여기서 그린 그림이 위의 머리·자국 모양 목록에 바로 올라옵니다.
            BuildShapeEditor(parent);
            BuildTrailShapeTargetRow(parent);
        }

        /// <summary>
        /// 방금 그린(또는 지금 고른) 도형을 어느 자리에 쓸지 고르는 줄.
        ///
        /// 잔상 탭에는 도형을 쓰는 자리가 둘입니다 — 머리와 자국. 저장할 때 지금 켜진
        /// 쪽에 자동으로 물려 주지만, 그것만으로는 <b>다른 쪽에 쓸 방법이 없습니다</b>.
        /// 선 방식으로 두고 자국 도형을 미리 정해 두는 것도 안 됐습니다.
        /// 그래서 두 자리를 여기서 직접 고를 수 있게 둡니다.
        /// </summary>
        private void BuildTrailShapeTargetRow(Transform parent)
        {
            var row = MakeRect("TrailShapeTargetRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            MakeButton(row, L.Trail.ApplyShapeToHead, 0f,
                () => ApplyDrawnShapeTo(head: true), ButtonColor);

            MakeButton(row, L.Trail.ApplyShapeToStamp, 0f,
                () => ApplyDrawnShapeTo(head: false), ButtonColor);
        }

        /// <summary>편집기에 적혀 있는 이름의 도형을 머리 또는 자국에 물려 줍니다.</summary>
        private void ApplyDrawnShapeTo(bool head)
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            string name = ShapeName().Trim();

            // 저장되지 않은 이름을 넣으면 도형을 못 찾아 내장 도형으로 돌아갑니다.
            // 그 상태를 조용히 두면 "적용했는데 안 바뀐다"가 됩니다.
            if (string.IsNullOrEmpty(name) || CustomShapes.Find(name) == null)
            {
                ShowHint(L.Muzzle.ShapeNotDrawn);
                return;
            }

            if (head)
            {
                profile.headTextureName = name;

                // 머리는 원본 궤적을 숨겨야 그려집니다. 적용해 놓고 아무것도 안 보이면
                // 그린 것이 잘못된 줄 알게 됩니다.
                Settings.BulletTrailSettings.SetEnabled(true);
                Settings.BulletTrailSettings.SetHideVanillaTrail(true);
            }
            else
            {
                profile.stampTextureName = name;
                profile.style = BulletTrailStyle.Stamp;
            }

            SyncTrailFromProfile();
            ShowHint(string.Format(L.Muzzle.ShapeLoaded, name));
        }

        /// <summary>
        /// 도형 편집기가 "지금 이 탭이 쓰는 도형"을 물을 때 답하는 값.
        ///
        /// 잔상 탭에는 도형을 쓰는 자리가 둘(머리·자국)입니다. 지금 켜져 있는 쪽을
        /// 답합니다 — 자국 방식이면 자국, 아니면 머리.
        /// </summary>
        private string CurrentTrailShapeTexture()
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return "";

            return profile.style == BulletTrailStyle.Stamp
                ? profile.stampTextureName
                : profile.headTextureName;
        }

        /// <summary>
        /// 저장 직후 자동 적용 — 지금 켜져 있는 자리에 물려 줍니다.
        /// 다른 자리에 쓰고 싶으면 아래 <see cref="BuildTrailShapeTargetRow"/>의 버튼을 씁니다.
        /// </summary>
        private void UseTrailShape(string name)
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            if (profile.style == BulletTrailStyle.Stamp)
            {
                profile.stampTextureName = name;
            }
            else
            {
                profile.headTextureName = name;

                // 머리는 원본 궤적을 숨겨야 그려집니다. 도형을 그려 놓고 아무것도
                // 안 보이면 그린 것이 잘못된 줄 알게 됩니다.
                Settings.BulletTrailSettings.SetEnabled(true);
                Settings.BulletTrailSettings.SetHideVanillaTrail(true);
            }

            SyncTrailFromProfile();
        }

        /// <summary>선 / 자국 방식 선택 한 줄.</summary>
        private void BuildTrailStyleRow(Transform parent)
        {
            var row = MakeRect("TrailStyleRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            _trailStyleButton = MakeButton(row, "", 0f, () =>
            {
                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                profile.style = profile.style == BulletTrailStyle.Line
                    ? BulletTrailStyle.Stamp
                    : BulletTrailStyle.Line;

                RefreshTrailDisplayRow();
                RefreshTrailPreview();
            }, ButtonColor);
        }

        /// <summary>
        /// 자국 항목 — 자국 방식일 때만 씁니다.
        ///
        /// 선 방식이면 상자째 흐려집니다. 머리·발광체와 같은 규칙입니다.
        /// </summary>
        private void BuildTrailStampSection(Transform parent)
        {
            AddSectionLabel(parent, L.Trail.SectionStamp);

            var stamp = MakeRect("TrailStampSection", parent);

            var layout = stamp.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = stamp.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _trailStampGroup = stamp.gameObject.AddComponent<CanvasGroup>();

            var shapeRow = MakeRect("TrailStampShapeRow", stamp);
            SetHeight(shapeRow, 40f);

            var shapeLayout = shapeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            shapeLayout.spacing = 8f;
            shapeLayout.childControlWidth = true;
            shapeLayout.childControlHeight = true;
            shapeLayout.childForceExpandWidth = false;
            shapeLayout.childAlignment = TextAnchor.MiddleLeft;

            var label = MakeText("TrailStampShapeLabel", shapeRow, L.Trail.FieldStampShape, 18,
                DimTextColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(label.rectTransform, 130f);
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 18f;

            // 도형 선택은 잠그지 않습니다. 지금 그려지지 않는 자리라도 모양은 미리
            // 정해 둘 수 있어야 합니다 — 상자가 입력을 막으면 "모양을 고르려면 먼저
            // 켜야 한다"는 순서가 생겨 버립니다.
            // ignoreParentGroups는 부모 상자의 입력 차단만 무시합니다(흐리기는 그대로라
            // 지금 적용되지 않는다는 표시는 남습니다).
            var stampShapeGroup = shapeRow.gameObject.AddComponent<CanvasGroup>();
            stampShapeGroup.ignoreParentGroups = true;
            stampShapeGroup.interactable = true;
            stampShapeGroup.blocksRaycasts = true;
            MakeButton(shapeRow, "\u25C0", 44f, () => CycleTrailStampShape(-1), ButtonColor);

            _trailStampShapeButton = MakeButton(shapeRow, "", 0f, () => CycleTrailStampShape(1), ButtonColor);
            _trailStampShapeButton.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MakeButton(shapeRow, "\u25B6", 44f, () => CycleTrailStampShape(1), ButtonColor);

            AddTrailSlider(stamp, L.Trail.FieldStampRate, 1f, 20f, "0.0",
                p => p.stampRate, (p, v) => p.stampRate = v);
            AddTrailSlider(stamp, L.Trail.FieldStampSize, 0.02f, 0.6f, "0.000",
                p => p.stampSize, (p, v) => p.stampSize = v);
            AddTrailSlider(stamp, L.Trail.FieldStampLife, 0.05f, 2f, "0.00",
                p => p.stampLife, (p, v) => p.stampLife = v);
        }

        private void CycleTrailStampShape(int delta)
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            var choices = TrailHeadShapeChoices();
            if (choices.Count == 0)
                return;

            string current = string.IsNullOrEmpty(profile.stampTextureName)
                ? profile.stampShape.ToString()
                : profile.stampTextureName;

            int index = choices.IndexOf(current);
            if (index < 0)
                index = 0;

            string picked = choices[(int)Mathf.Repeat(index + delta, choices.Count)];

            if (Enum.TryParse(picked, out BulletHeadShape shape) &&
                Array.IndexOf(BulletHeadShapes.All, shape) >= 0)
            {
                profile.stampShape = shape;
                profile.stampTextureName = "";
            }
            else
            {
                profile.stampTextureName = picked;
            }

            RefreshTrailDisplayRow();
            RefreshTrailPreview();
        }

        /// <summary>방식 버튼과 자국 상자를 지금 프로필에 맞춥니다.</summary>
        private void RefreshTrailStyleRows(BulletTrailProfile? profile)
        {
            bool stamp = profile != null && profile.style == BulletTrailStyle.Stamp;

            if (_trailStyleButton != null)
            {
                var label = _trailStyleButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = stamp ? L.Trail.StyleStamp : L.Trail.StyleLine;

                if (_trailStyleButton.targetGraphic != null)
                    _trailStyleButton.targetGraphic.color = stamp ? ButtonAccentColor : ButtonColor;
            }

            if (_trailStampGroup != null)
            {
                _trailStampGroup.alpha = stamp ? 1f : 0.4f;
                _trailStampGroup.interactable = stamp;
                _trailStampGroup.blocksRaycasts = stamp;
            }

            if (_trailStampShapeButton != null)
            {
                var label = _trailStampShapeButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = profile != null && !string.IsNullOrEmpty(profile.stampTextureName)
                        ? profile.stampTextureName
                        : LocalizedHeadShapeName(profile != null ? profile.stampShape : BulletHeadShape.Diamond);
                }
            }
        }

        /// <summary>
        /// 총알 발광체 — 게임 원본 빛을 등급별로 조절합니다.
        ///
        /// 머리와 같은 짜임입니다. 조건이 안 맞으면 상자째 흐려지고, 켜는 버튼은
        /// 입력이 막히지 않도록 상자 밖에 둡니다.
        /// </summary>
        private void BuildTrailGlowSection(Transform parent)
        {
            AddSectionLabel(parent, L.Trail.SectionGlow);

            var noticeRow = MakeRect("TrailGlowNoticeRow", parent);
            SetHeight(noticeRow, 34f);

            var noticeLayout = noticeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            noticeLayout.spacing = 8f;
            noticeLayout.childControlWidth = true;
            noticeLayout.childControlHeight = true;
            noticeLayout.childForceExpandWidth = false;
            noticeLayout.childAlignment = TextAnchor.MiddleLeft;

            _trailGlowNotice = MakeText("TrailGlowNotice", noticeRow, "", 16, WarnTextColor,
                TextAlignmentOptions.MidlineLeft);

            var noticeElement = _trailGlowNotice.gameObject.AddComponent<LayoutElement>();
            noticeElement.flexibleWidth = 1f;

            _trailGlowEnableButton = MakeButton(noticeRow, L.Trail.GlowTurnOn, 110f, () =>
            {
                BulletTrailSettings.SetEnabled(true);
                BulletTrailSettings.SetCustomizeGlow(true);
                RefreshTrailDisplayRow();
            }, ButtonAccentColor);

            var glow = MakeRect("TrailGlowSection", parent);

            var glowLayout = glow.gameObject.AddComponent<VerticalLayoutGroup>();
            glowLayout.spacing = 8f;
            glowLayout.childControlWidth = true;
            glowLayout.childControlHeight = true;
            glowLayout.childForceExpandWidth = true;
            glowLayout.childForceExpandHeight = false;

            var glowFitter = glow.gameObject.AddComponent<ContentSizeFitter>();
            glowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _trailGlowGroup = glow.gameObject.AddComponent<CanvasGroup>();

            var visibleRow = MakeRect("TrailGlowVisibleRow", glow);
            SetHeight(visibleRow, 40f);

            var visibleLayout = visibleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            visibleLayout.childControlWidth = true;
            visibleLayout.childControlHeight = true;
            visibleLayout.childForceExpandWidth = true;

            _trailGlowVisibleButton = MakeButton(visibleRow, "", 0f, () =>
            {
                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                profile.glowVisible = !profile.glowVisible;
                RefreshTrailDisplayRow();
            }, ButtonColor);

            // 배율입니다 — 1이 곧 "원본 그대로". 원본 크기·밝기는 총알 프리팹마다 달라서
            // 절대값으로는 어떤 총에서 알맞은지 정할 수가 없습니다.
            AddTrailSlider(glow, L.Trail.FieldGlowScale, 0f, 3f, "0.00",
                p => p.glowScale, (p, v) => p.glowScale = v);
            AddTrailSlider(glow, L.Trail.FieldGlowIntensity, 0f, 3f, "0.00",
                p => p.glowIntensity, (p, v) => p.glowIntensity = v);

            var colorRow = MakeRect("TrailGlowColorRow", glow);
            SetHeight(colorRow, 40f);

            var colorLayout = colorRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            colorLayout.childControlWidth = true;
            colorLayout.childControlHeight = true;
            colorLayout.childForceExpandWidth = true;

            _trailGlowColorModeButton = MakeButton(colorRow, "", 0f, CycleTrailGlowColorMode, ButtonColor);

            _trailPickerGlowColor = AddColorPicker(glow, color =>
            {
                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                profile.glowColor = new Color(color.r, color.g, color.b, 1f);

                // 색을 직접 골랐다는 건 그 색을 쓰겠다는 뜻입니다. 모드가 원본이나
                // 잔상 따라가기에 머물러 있으면 방금 고른 색이 반영되지 않습니다.
                profile.glowColorMode = BulletGlowColorMode.Custom;

                RefreshTrailDisplayRow();
            });
        }

        private void CycleTrailGlowColorMode()
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            if (profile.glowColorMode == BulletGlowColorMode.Vanilla)
                profile.glowColorMode = BulletGlowColorMode.FollowTrail;
            else if (profile.glowColorMode == BulletGlowColorMode.FollowTrail)
                profile.glowColorMode = BulletGlowColorMode.Custom;
            else
                profile.glowColorMode = BulletGlowColorMode.Vanilla;

            RefreshTrailDisplayRow();
        }

        /// <summary>발광체 항목을 지금 프로필·옵션 상태에 맞춥니다.</summary>
        private void RefreshTrailGlowRows(BulletTrailProfile? profile)
        {
            bool active = BulletTrailSettings.Enabled && BulletTrailSettings.CustomizeGlow;

            if (_trailGlowGroup != null)
            {
                _trailGlowGroup.alpha = active ? 1f : 0.4f;
                _trailGlowGroup.interactable = active;
                _trailGlowGroup.blocksRaycasts = active;
            }

            if (_trailGlowNotice != null)
            {
                _trailGlowNotice.text = active ? L.Trail.GlowActive : L.Trail.GlowInactive;
                _trailGlowNotice.color = active ? DimTextColor : WarnTextColor;
            }

            if (_trailGlowEnableButton != null)
                _trailGlowEnableButton.gameObject.SetActive(!active);

            bool visible = profile == null || profile.glowVisible;

            if (_trailGlowVisibleButton != null)
            {
                var label = _trailGlowVisibleButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = visible ? L.Trail.GlowHide : L.Trail.GlowShow;

                if (_trailGlowVisibleButton.targetGraphic != null)
                    _trailGlowVisibleButton.targetGraphic.color = visible ? ButtonAccentColor : ButtonColor;
            }

            var mode = profile != null ? profile.glowColorMode : BulletGlowColorMode.Vanilla;

            if (_trailGlowColorModeButton != null)
            {
                var label = _trailGlowColorModeButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = LocalizedGlowColorMode(mode);

                if (_trailGlowColorModeButton.targetGraphic != null)
                    _trailGlowColorModeButton.targetGraphic.color =
                        mode == BulletGlowColorMode.Vanilla ? ButtonColor : ButtonAccentColor;
            }

            if (_trailPickerGlowColor != null && profile != null)
                _trailPickerGlowColor.SetColor(profile.glowColor);
        }

        private static string LocalizedGlowColorMode(BulletGlowColorMode mode)
        {
            switch (mode)
            {
                case BulletGlowColorMode.FollowTrail: return L.Trail.GlowColorFollowTrail;
                case BulletGlowColorMode.Custom: return L.Trail.GlowColorCustom;
                case BulletGlowColorMode.Vanilla:
                default: return L.Trail.GlowColorVanilla;
            }
        }

        /// <summary>머리 도형 이름 · 색 모드 버튼 · 색 피커를 지금 프로필에 맞춥니다.</summary>
        private void RefreshTrailHeadRows(BulletTrailProfile? profile)
        {
            // 머리가 지금 실제로 그려지는 조건. 여기가 거짓이면 아래 항목을 만져도
            // 화면이 바뀌지 않으므로, 그 사실을 흐리기 + 안내문으로 분명히 보여 줍니다.
            bool drawn = BulletTrailSettings.Enabled && BulletTrailSettings.HideVanillaTrail;

            if (_trailHeadGroup != null)
            {
                _trailHeadGroup.alpha = drawn ? 1f : 0.4f;
                _trailHeadGroup.interactable = drawn;
                _trailHeadGroup.blocksRaycasts = drawn;
            }

            if (_trailHeadNotice != null)
            {
                _trailHeadNotice.text = drawn ? L.Trail.HeadActive : L.Trail.HeadInactive;
                _trailHeadNotice.color = drawn ? DimTextColor : WarnTextColor;
            }

            // 이미 켜져 있으면 버튼은 할 일이 없습니다. 남겨 두면 눌러도 아무 일이
            // 없는 버튼이 되어 오히려 헷갈립니다.
            if (_trailHeadEnableButton != null)
                _trailHeadEnableButton.gameObject.SetActive(!drawn);

            if (_trailHeadShapeButton != null)
            {
                var label = _trailHeadShapeButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    // 사용자 도형은 이름을 그대로 보여 줍니다 — 번역할 대상이 아닙니다.
                    label.text = profile != null && !string.IsNullOrEmpty(profile.headTextureName)
                        ? profile.headTextureName
                        : LocalizedHeadShapeName(profile != null ? profile.headShape : BulletHeadShape.Capsule);
                }
            }

            bool follow = profile == null || profile.headFollowTrailColor;

            if (_trailHeadColorModeButton != null)
            {
                var label = _trailHeadColorModeButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = follow ? L.Trail.HeadColorSeparate : L.Trail.HeadColorFollow;

                if (_trailHeadColorModeButton.targetGraphic != null)
                    _trailHeadColorModeButton.targetGraphic.color = follow ? ButtonColor : ButtonAccentColor;
            }

            // 따라가는 동안에도 피커는 남겨 둡니다 — 지금 어떤 색이 나가는지 보이는 편이
            // 낫고, 여기서 색을 고르면 곧바로 따로 쓰기로 넘어갑니다.
            if (_trailPickerHeadColor != null && profile != null)
                _trailPickerHeadColor.SetColor(profile.ResolveHeadColor());
        }

        private static string LocalizedHeadShapeName(BulletHeadShape shape)
        {
            switch (shape)
            {
                case BulletHeadShape.Dot: return L.Trail.HeadShapeDot;
                case BulletHeadShape.Diamond: return L.Trail.HeadShapeDiamond;
                case BulletHeadShape.Arrow: return L.Trail.HeadShapeArrow;
                case BulletHeadShape.Ring: return L.Trail.HeadShapeRing;
                case BulletHeadShape.Spark: return L.Trail.HeadShapeSpark;
                case BulletHeadShape.Capsule:
                default: return L.Trail.HeadShapeCapsule;
            }
        }

        /// <summary>
        /// 머리가 지금 그려지는지 알려 주고, 아니라면 그 자리에서 켤 수 있게 합니다.
        /// </summary>
        private void BuildTrailHeadNoticeRow(Transform parent)
        {
            var row = MakeRect("TrailHeadNoticeRow", parent);
            SetHeight(row, 34f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            _trailHeadNotice = MakeText("TrailHeadNotice", row, "", 16, WarnTextColor,
                TextAlignmentOptions.MidlineLeft);

            var noticeElement = _trailHeadNotice.gameObject.AddComponent<LayoutElement>();
            noticeElement.flexibleWidth = 1f;

            _trailHeadEnableButton = MakeButton(row, L.Trail.HeadTurnOn, 110f, () =>
            {
                BulletTrailSettings.SetEnabled(true);
                BulletTrailSettings.SetHideVanillaTrail(true);
                RefreshTrailDisplayRow();
                RefreshTrailPreview();
            }, ButtonAccentColor);
        }

        /// <summary>머리 모양 — 이전/다음으로 넘기는 한 줄.</summary>
        private void BuildTrailHeadShapeRow(Transform parent)
        {
            var row = MakeRect("TrailHeadShapeRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // 슬라이더 행의 라벨 칸과 같은 폭이라야 왼쪽 줄이 맞습니다.
            var label = MakeText("TrailHeadShapeLabel", row, L.Trail.FieldHeadShape, 18, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetWidth(label.rectTransform, 130f);
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 18f;

            // 도형 선택은 잠그지 않습니다. 지금 그려지지 않는 자리라도 모양은 미리
            // 정해 둘 수 있어야 합니다 — 상자가 입력을 막으면 "모양을 고르려면 먼저
            // 켜야 한다"는 순서가 생겨 버립니다.
            // ignoreParentGroups는 부모 상자의 입력 차단만 무시합니다(흐리기는 그대로라
            // 지금 적용되지 않는다는 표시는 남습니다).
            var headShapeGroup = row.gameObject.AddComponent<CanvasGroup>();
            headShapeGroup.ignoreParentGroups = true;
            headShapeGroup.interactable = true;
            headShapeGroup.blocksRaycasts = true;
            MakeButton(row, "◀", 44f, () => CycleTrailHeadShape(-1), ButtonColor);

            _trailHeadShapeButton = MakeButton(row, "", 0f, () => CycleTrailHeadShape(1), ButtonColor);
            var element = _trailHeadShapeButton.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;

            MakeButton(row, "▶", 44f, () => CycleTrailHeadShape(1), ButtonColor);
        }

        /// <summary>내장 도형 + 직접 그린 도형 + 사용자 PNG를 한 줄로 이어 붙인 목록.</summary>
        private static List<string> TrailHeadShapeChoices()
        {
            var choices = new List<string>();
            foreach (var shape in BulletHeadShapes.All)
                choices.Add(shape.ToString());

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

        private void CycleTrailHeadShape(int delta)
        {
            var profile = CurrentTrailProfile();
            if (profile == null)
                return;

            var choices = TrailHeadShapeChoices();
            if (choices.Count == 0)
                return;

            string current = string.IsNullOrEmpty(profile.headTextureName)
                ? profile.headShape.ToString()
                : profile.headTextureName;

            int index = choices.IndexOf(current);
            if (index < 0)
                index = 0;

            string picked = choices[(int)Mathf.Repeat(index + delta, choices.Count)];

            // 내장 도형 이름이면 도형으로, 아니면 파일·그림 이름으로 봅니다.
            if (Enum.TryParse(picked, out BulletHeadShape shape) &&
                Array.IndexOf(BulletHeadShapes.All, shape) >= 0)
            {
                profile.headShape = shape;
                profile.headTextureName = "";
            }
            else
            {
                profile.headTextureName = picked;
            }

            RefreshTrailDisplayRow();
            RefreshTrailPreview();
        }

        /// <summary>머리 색을 꼬리에서 가져올지 따로 고를지.</summary>
        private void BuildTrailHeadColorRow(Transform parent)
        {
            var row = MakeRect("TrailHeadColorRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            _trailHeadColorModeButton = MakeButton(row, "", 0f, () =>
            {
                var profile = CurrentTrailProfile();
                if (profile == null)
                    return;

                profile.headFollowTrailColor = !profile.headFollowTrailColor;
                RefreshTrailDisplayRow();
                RefreshTrailPreview();
            }, ButtonColor);
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

            // 원본 궤적 숨기기는 잔상 자체를 켠 상태에서만 의미가 있어서 바로 아래 둡니다.
            var vanillaRow = MakeRect("TrailHideVanillaRow", parent);
            SetHeight(vanillaRow, 40f);

            var vanillaLayout = vanillaRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            vanillaLayout.childControlWidth = true;
            vanillaLayout.childControlHeight = true;
            vanillaLayout.childForceExpandWidth = true;

            _trailHideVanillaButton = MakeButton(vanillaRow, "", 0f, () =>
            {
                BulletTrailSettings.SetHideVanillaTrail(!BulletTrailSettings.HideVanillaTrail);
                RefreshTrailDisplayRow();
            }, ButtonColor);

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

            // 라벨 칸은 130px 고정입니다. 넘치는 글자는 TMP가 그대로 밖으로 그려서
            // 슬라이더를 덮어 버립니다(번역이 길어지면 언제든 다시 생깁니다).
            // 줄바꿈 대신 글자 크기를 줄여 한 줄에 맞춥니다 — 행 높이가 36px 고정이라
            // 두 줄이 되면 아래위가 잘립니다.
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 19f;

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

            if (_trailHideVanillaButton != null)
            {
                bool hide = BulletTrailSettings.HideVanillaTrail;

                var label = _trailHideVanillaButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = hide ? L.Trail.HideVanillaOff : L.Trail.HideVanillaOn;

                if (_trailHideVanillaButton.targetGraphic != null)
                    _trailHideVanillaButton.targetGraphic.color = hide ? ButtonAccentColor : ButtonColor;

                // 잔상을 끈 상태에서는 원본을 숨기지 않습니다(총알이 아예 안 보이게 됩니다).
                // 그래서 버튼도 같이 잠급니다.
                _trailHideVanillaButton.interactable = on;
            }

            var profile = CurrentTrailProfile();
            bool gradeOn = profile == null || profile.enabled;

            RefreshTrailStyleRows(profile);
            RefreshTrailHeadRows(profile);
            RefreshTrailGlowRows(profile);

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

            // 발광체는 총알을 감싸는 빛이라 꼬리·머리보다 먼저(=아래에) 깔아야 합니다.
            // 나중에 그리면 궤적을 덮어 버립니다.
            DrawGlowHalo(profile, pixels, headX);

            // 모드가 머리를 그리는 건 원본 궤적을 숨겼을 때뿐입니다. 그렇지 않으면
            // 머리 자리에 있는 건 게임 원본 총알이라, 머리 값을 반영하면 거짓말이 됩니다.
            bool drawHead = BulletTrailSettings.HideVanillaTrail && profile.headWidth > 0.0001f;

            float half = TrailPreviewHeight * 0.5f;

            // 실제 잔상과 똑같은 식으로 색·알파를 냅니다. 여기서 밝기를 색에 그냥 곱하면
            // 미리보기만 하얗게 뜨고, 게임에서 보이는 색과 어긋납니다.
            BulletTrailShading.Resolve(profile.colorStart, profile.intensity, profile.alpha,
                out Color headColor, out float headAlpha);
            BulletTrailShading.Resolve(profile.colorEnd, profile.intensity, profile.alpha,
                out Color tailColor, out float tailAlpha);

            float baseAlpha = Mathf.Min(headAlpha, tailAlpha);

            if (profile.style == BulletTrailStyle.Stamp)
            {
                DrawTrailStamps(profile, pixels, headX, headColor, tailColor, baseAlpha);

                if (drawHead)
                    DrawModHead(profile, pixels, headX);
                else
                    DrawTrailHead(profile, pixels, headX, headColor, baseAlpha);

                return;
            }

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
                float bandHalf = WidthToPixels(Mathf.Lerp(profile.startWidth, profile.endWidth, t));

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

            if (drawHead)
                DrawModHead(profile, pixels, headX);
            else
                DrawTrailHead(profile, pixels, headX, headColor, baseAlpha);
        }

        /// <summary>
        /// 자국 방식 미리보기 — 지나간 자리에 도형을 일정 간격으로 찍습니다.
        ///
        /// 실제와 같은 규칙입니다. 간격은 <b>거리</b> 기준(1m당 개수)이고, 총알에서
        /// 멀어질수록(=오래됐을수록) 색이 꼬리 쪽으로 넘어가며 흐려집니다.
        ///
        /// 미터를 픽셀로 바꿀 기준이 필요합니다. 잔상 길이를 초로 잡을 때 쓰는 환산
        /// (화면 폭 = <see cref="TrailPreviewSpan"/>초)을 그대로 빌려 씁니다.
        /// </summary>
        private void DrawTrailStamps(BulletTrailProfile profile, Color32[] pixels, float headX,
            Color headColor, Color tailColor, float baseAlpha)
        {
            // 자국이 남아 있는 구간(px) = 지속시간 × 총알 속도
            float spanPx = TrailPreviewWidth * (Mathf.Max(0.05f, profile.stampLife) / TrailPreviewSpan);

            // 간격·크기 모두 꼬리·머리와 같은 환산을 씁니다. 여기만 다른 자를 쓰면
            // 자국이 머리보다 크거나 작아 보이는 것이 실제와 뒤집힙니다.
            float stepPx = Mathf.Max(2f,
                TrailPreviewPixelsPerMeter / Mathf.Max(0.1f, profile.stampRate));

            float radius = Mathf.Max(1.5f, WidthToPixels(profile.stampSize));

            var shape = BulletHeadShapes.Resolve(profile.stampShape, profile.stampTextureName);
            Color32[]? shapePixels = SpritePixels(shape);
            int shapeW = shape != null ? shape.width : 0;
            int shapeH = shape != null ? shape.height : 0;

            float half = TrailPreviewHeight * 0.5f;

            for (float offset = 0f; offset <= spanPx; offset += stepPx)
            {
                float cx = headX - offset;
                if (cx + radius < 0f)
                    break;
                if (cx - radius > TrailPreviewWidth)
                    continue;

                // 0 = 방금 찍힘, 1 = 사라지기 직전
                float age = spanPx > 0.0001f ? offset / spanPx : 0f;

                Color color = Color.Lerp(headColor, tailColor, age);
                float alpha = baseAlpha * TrailAlphaAt(age);
                if (alpha <= 0.004f)
                    continue;

                byte r = ToByte(color.r);
                byte g = ToByte(color.g);
                byte b = ToByte(color.b);

                int fromX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
                int toX = Mathf.Min(TrailPreviewWidth - 1, Mathf.CeilToInt(cx + radius));
                int fromY = Mathf.Max(0, Mathf.FloorToInt(half - radius));
                int toY = Mathf.Min(TrailPreviewHeight - 1, Mathf.CeilToInt(half + radius));

                for (int x = fromX; x <= toX; x++)
                {
                    float u = (x + 0.5f - (cx - radius)) / (radius * 2f);

                    for (int y = fromY; y <= toY; y++)
                    {
                        float v = (y + 0.5f - (half - radius)) / (radius * 2f);

                        float coverage;
                        if (shapePixels != null && shapeW > 0 && shapeH > 0)
                        {
                            int sx = Mathf.Clamp(Mathf.FloorToInt(u * shapeW), 0, shapeW - 1);
                            int sy = Mathf.Clamp(Mathf.FloorToInt(v * shapeH), 0, shapeH - 1);
                            coverage = shapePixels[sy * shapeW + sx].a / 255f;
                        }
                        else
                        {
                            float dx = (u - 0.5f) * 2f;
                            float dy = (v - 0.5f) * 2f;
                            coverage = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        }

                        float a = alpha * coverage;
                        if (a <= 0.004f)
                            continue;

                        int index = y * TrailPreviewWidth + x;
                        if (pixels[index].a >= ToByte(a))
                            continue;

                        pixels[index] = new Color32(r, g, b, ToByte(a));
                    }
                }
            }
        }

        /// <summary>
        /// 미리보기에서 크기 배율 1일 때의 발광체 반지름(px).
        ///
        /// 실제 발광체 크기는 총알 프리팹의 스케일이라 픽셀로 환산할 방법이 없습니다.
        /// 그래서 "배율 1 = 띠 높이를 거의 채우는 헤일로"로 기준을 정해 두고,
        /// 슬라이더는 그 대비로 커지고 작아지는 것만 보여 줍니다.
        /// </summary>
        private const float GlowPreviewRadius = TrailPreviewHeight * 0.45f;

        /// <summary>
        /// 총알을 감싸는 빛.
        ///
        /// 색은 런타임과 같은 <see cref="BulletGlowController.BuildColor"/>로 냅니다.
        /// 원본 색은 지금 든 총의 총알 프리팹에서 직접 읽어 오므로, "원본 그대로" 모드도
        /// 제 색으로 보입니다.
        ///
        /// 발광체 색은 HDR(채널이 1을 넘음)이라 화면에 그대로 찍을 수 없습니다.
        /// 색조는 정규화해서 쓰고, 원본 대비 세기는 진하기로 옮깁니다 — 밝기를 올리면
        /// 색이 흰색으로 뭉개지는 대신 헤일로가 진해집니다.
        /// </summary>
        private void DrawGlowHalo(BulletTrailProfile profile, Color32[] pixels, float headX)
        {
            if (!BulletTrailSettings.CustomizeGlow || !profile.glowVisible)
                return;

            float radius = GlowPreviewRadius * Mathf.Max(0f, profile.glowScale);
            if (radius < 1f)
                return;

            var vanilla = BulletGlowController.SampleVanillaColor();
            var hdr = BulletGlowController.BuildColor(vanilla, profile);

            float peak = BulletGlowController.Peak(hdr);
            if (peak <= 0.0001f)
                return;

            float vanillaPeak = BulletGlowController.Peak(vanilla);
            if (vanillaPeak <= 0.0001f)
                vanillaPeak = peak;

            // 원본 세기를 1로 본 상대 밝기. 기본값(배율 1)에서 1이 됩니다.
            float relative = Mathf.Clamp01(peak / vanillaPeak);

            byte r = ToByte(hdr.r / peak);
            byte g = ToByte(hdr.g / peak);
            byte b = ToByte(hdr.b / peak);

            float half = TrailPreviewHeight * 0.5f;

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
                    if (d >= 1f)
                        continue;

                    // 가운데가 밝고 가장자리로 부드럽게 사라지는 감쇠.
                    float falloff = 1f - d;
                    float a = relative * falloff * falloff * 0.65f;
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
        /// 모드가 그리는 총알 머리 — 실제 런타임과 같은 모양(짧고 굵은 단색 대시)입니다.
        ///
        /// 색·밝기는 <see cref="BulletTrailShading"/>으로, 런타임의
        /// <c>BuildHeadGradient</c>와 같은 식을 씁니다. 여기서 어긋나면 미리보기에서 고른
        /// 머리가 게임에서 다르게 나옵니다.
        /// </summary>
        private void DrawModHead(BulletTrailProfile profile, Color32[] pixels, float headX)
        {
            // 실제 머리는 정점 색이 아니라 HDR 발광으로 밝기를 냅니다. 화면에는 1을 넘는
            // 색을 찍을 수 없으니, 발광이 셀수록 흰색 쪽으로 옮겨 담습니다 — 게임에서
            // 하얗게 뜨는 머리가 미리보기에서도 하얗게 떠야 "밝기를 낮춰야겠다"가 보입니다.
            var baseColor = profile.ResolveHeadColor();

            float peak = Mathf.Max(baseColor.r, Mathf.Max(baseColor.g, baseColor.b));
            if (peak > 0.0001f)
                baseColor = new Color(baseColor.r / peak, baseColor.g / peak, baseColor.b / peak, 1f);

            float gain = BulletTrailSystem.HeadEmissionGain(profile.headIntensity);
            Color color = Color.Lerp(baseColor, Color.white, Mathf.Clamp01((gain - 1f) / 6f));

            // 판은 도형 알파가 그대로 실루엣이 됩니다. 투명도는 도형이 정합니다.
            const float alpha = 1f;

            float half = TrailPreviewHeight * 0.5f;
            float bandHalf = WidthToPixels(profile.headWidth);

            // 머리는 가로세로비가 고정된 판입니다. 굵기(px)에 비율을 곱하면
            // 실제와 같은 모양이 나옵니다 — 미리보기에서만 늘어나면 안 됩니다.
            float headPx = Mathf.Max(2f, bandHalf * 2f * Mathf.Max(0.2f, profile.headAspect));

            byte r = ToByte(color.r);
            byte g = ToByte(color.g);
            byte b = ToByte(color.b);

            int fromX = Mathf.Max(0, Mathf.FloorToInt(headX - headPx));
            int toX = Mathf.Min(TrailPreviewWidth - 1, Mathf.CeilToInt(headX));

            int fromY = Mathf.Max(0, Mathf.FloorToInt(half - bandHalf));
            int toY = Mathf.Min(TrailPreviewHeight - 1, Mathf.CeilToInt(half + bandHalf));

            // 실제 머리는 도형 텍스처가 늘어나 그려집니다. 미리보기도 같은 텍스처를
            // 같은 방향(가로=진행, 세로=굵기)으로 읽어야 고른 모양이 그대로 보입니다.
            var shape = BulletHeadShapes.Resolve(profile.headShape, profile.headTextureName);
            Color32[]? shapePixels = SpritePixels(shape);

            // 사용자 PNG는 정사각형이 아닐 수 있어서 가로·세로를 따로 씁니다.
            int shapeW = shape != null ? shape.width : 0;
            int shapeH = shape != null ? shape.height : 0;

            for (int x = fromX; x <= toX; x++)
            {
                // 0 = 꼬리 쪽 끝, 1 = 총알 끝
                float u = headPx <= 0f ? 1f : Mathf.Clamp01(1f - (headX - x) / headPx);

                for (int y = fromY; y <= toY; y++)
                {
                    float v = (y + 0.5f - half) / bandHalf;      // -1 ~ 1

                    float coverage;
                    if (shapePixels != null && shapeW > 0 && shapeH > 0)
                    {
                        int sx = Mathf.Clamp(Mathf.FloorToInt(u * shapeW), 0, shapeW - 1);
                        int sy = Mathf.Clamp(Mathf.FloorToInt((v * 0.5f + 0.5f) * shapeH), 0, shapeH - 1);
                        coverage = shapePixels[sy * shapeW + sx].a / 255f;
                    }
                    else
                    {
                        coverage = Mathf.Clamp01(1f - Mathf.Abs(v));
                        coverage *= coverage;
                    }

                    float a = alpha * coverage;
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
        /// 게임 원본 총알 자리 표시.
        ///
        /// 원본 궤적을 그대로 두는 동안에는 총알이 게임 것이라 모드가 손댈 수 없습니다.
        /// 그래도 뭔가 움직이는 걸 보여 줘야 꼬리 길이가 읽히므로 점 하나만 찍습니다.
        /// </summary>
        private void DrawTrailHead(BulletTrailProfile profile, Color32[] pixels,
            float headX, Color headColor, float baseAlpha)
        {
            if (headX < 0f || headX >= TrailPreviewWidth)
                return;

            float half = TrailPreviewHeight * 0.5f;
            float radius = Mathf.Max(2.5f, WidthToPixels(profile.startWidth));

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
