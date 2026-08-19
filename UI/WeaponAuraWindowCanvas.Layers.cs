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
    /// 무기 오라 탭의 세 번째 칸 — <b>겹</b>.
    ///
    /// 본체 오라는 무기 실루엣을 감싸는 한 벌이라 "총구에서만 불티" 같은 것을 만들 수
    /// 없습니다. 겹은 그 위에 얹는 단순한 이미터로, 각자 <b>뿜는 자리</b>를 고릅니다 —
    /// 무기 전체 / 총구 / 본체 / 총열. 자리가 다른 겹을 함께 쌓으면 조합이 나옵니다.
    ///
    /// 별도 탭으로 두지 않고 오라 안에 넣은 이유 — 둘 다 "무기 주위에 뿜는 것"이라
    /// 탭이 갈리면 어디서 뭘 만졌는지 기억해야 합니다. 저장·전용 설정·되돌리기도
    /// 오라 프로필 하나로 묶여서 규칙이 하나로 줄어듭니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private int _editingLayer;

        private readonly List<Button> _layerButtons = new List<Button>();

        private RectTransform? _layerRow;
        private Button? _layerEnableButton;
        private Button? _removeLayerButton;
        private TextMeshProUGUI? _layerTextureLabel;
        private TextMeshProUGUI? _layerStatus;
        private ColorPickerControl? _layerPicker;
        private ColorPickerControl? _layerEndPicker;
        private GameObject? _layerEndColorRows;
        private Button? _useColorEndButton;
        private Button? _stretchButton;
        private Button? _arcSpreadButton;
        private Button? _gizmoButton;

        private readonly List<KeyValuePair<WeaponParticleDirection, Button>> _directionButtons =
            new List<KeyValuePair<WeaponParticleDirection, Button>>();

        private readonly List<KeyValuePair<WeaponParticleAnchor, Button>> _anchorButtons =
            new List<KeyValuePair<WeaponParticleAnchor, Button>>();

        /// <summary>겹 슬라이더 한 줄. 다른 줄과 달리 <b>겹</b>에 묶입니다.</summary>
        private sealed class LayerSliderRow
        {
            public Slider Slider = null!;
            public TextMeshProUGUI ValueText = null!;
            public string Format = "0.00";
            public Func<WeaponEffectLayer, float> Get = null!;

            /// <summary>구조가 바뀌는 값은 붙어 있는 것을 다시 만들어야 합니다.</summary>
            public bool NeedsRebuild;
        }

        private readonly List<LayerSliderRow> _layerSliders = new List<LayerSliderRow>();

        /// <summary>
        /// 지금 레이어를 만질 대상.
        ///
        /// 탭마다 자기 레이어를 갖습니다 — 오라는 무기에 계속 붙고, 총구·참격은 쏘거나
        /// 휘두르는 순간에만 나옵니다. 편집 화면은 하나뿐이므로 대상만 갈아 끼웁니다.
        ///
        /// 잔상 탭에는 레이어가 없습니다. 붙는 대상이 날아가는 총알 하나하나라 연사하면
        /// 이미터가 순식간에 수십 개가 되고, 수명·상한 관리가 나머지와 완전히 다릅니다.
        /// </summary>
        private ILayerHost? CurrentLayerHost()
        {
            switch (_tab)
            {
                case WindowTab.Muzzle:
                    return CurrentMuzzleProfile();

                case WindowTab.Melee:
                    return CurrentMeleeProfile();

                case WindowTab.Trail:
                    return null;

                default:
                    return CurrentProfile();
            }
        }

        private WeaponEffectLayer? CurrentLayer()
        {
            var host = CurrentLayerHost();
            if (host == null)
                return null;

            if (_editingLayer >= host.Layers.Length)
                _editingLayer = Mathf.Max(0, host.Layers.Length - 1);

            return host.GetLayer(_editingLayer);
        }

        // ── 창 ──────────────────────────────────────────────────────

        private GameObject? _layerRoot;
        private Button? _layerButton;
        private TextMeshProUGUI? _layerTitle;

        private bool LayerWindowOpen => _layerRoot != null && _layerRoot.activeSelf;

        /// <summary>
        /// 지금 탭의 레이어를 엽니다.
        ///
        /// 탭 안에 넣지 않고 창으로 띄우는 이유 — 세 탭이 같은 편집기를 씁니다. 탭마다 한
        /// 벌씩 두면 한쪽만 고치는 순간 셋이 서로 달라지고, 옵션이 스무 줄이 넘어서 탭
        /// 안에 넣으면 그 탭 본래 옵션이 스크롤 저 아래로 밀립니다.
        /// </summary>
        private void OpenLayerWindow()
        {
            if (CurrentLayerHost() == null)
            {
                ShowHint(L.Particles.NoLayers);
                return;
            }

            if (_layerRoot == null)
                BuildLayerWindow();

            if (_layerRoot == null)
                return;

            _layerRoot.SetActive(true);

            _editingLayer = 0;
            RebuildLayerButtons();
            SyncLayerFromProfile();
        }

        private void CloseLayerWindow()
        {
            if (_layerRoot != null)
                _layerRoot.SetActive(false);
        }

        /// <summary>탭이 바뀌면 닫습니다 — 열어 둔 채 대상만 갈리면 무엇을 만지는지 모릅니다.</summary>
        private void SyncLayerButton()
        {
            if (_layerButton != null)
                _layerButton.gameObject.SetActive(CurrentLayerHost() != null);

            CloseLayerWindow();
        }

        private void BuildLayerWindow()
        {
            if (_canvasRoot == null)
                return;

            var backdrop = MakeImage("LayerBackdrop", _canvasRoot.transform, BackdropColor);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _layerRoot = backdrop.gameObject;

            var panel = MakeImage("LayerPanel", backdrop.transform, PanelColor);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(760f, 760f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = MakeRect("Header", panel.transform);
            SetHeight(header, 40f);

            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 12f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;

            _layerTitle = MakeText("Title", header, L.Particles.Layers, 26, TextColor,
                TextAlignmentOptions.MidlineLeft);
            _layerTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MakeButton(header, L.Library.Close, 110f, CloseLayerWindow, ButtonColor);

            var scrollGo = MakeRect("Scroll", panel.transform);
            scrollGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 640f;

            BuildLayerControls(BuildScrollBody(scrollGo));

            ApplyFont(panel.gameObject);
        }

        // ── 구성 ────────────────────────────────────────────────────

        private void BuildLayerControls(Transform parent)
        {
            _layerStatus = MakeText("LayerStatus", parent, "-", 17, DimTextColor,
                TextAlignmentOptions.TopLeft);
            SetHeight(_layerStatus.rectTransform, 44f);
            _layerStatus.enableWordWrapping = true;

            BuildLayerStrip(parent);

            var buttons = MakeRect("LayerButtons", parent);
            SetHeight(buttons, 40f);

            var buttonLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 8f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;

            MakeButton(buttons, L.Particles.AddLayer, 0f, AddAuraLayer, ButtonAccentColor);
            _removeLayerButton = MakeButton(buttons, L.Particles.RemoveLayer, 0f,
                RemoveAuraLayer, ButtonColor);

            var toggleRow = MakeRect("LayerToggleRow", parent);
            SetHeight(toggleRow, 40f);

            var toggleLayout = toggleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            toggleLayout.spacing = 8f;
            toggleLayout.childControlWidth = true;
            toggleLayout.childControlHeight = true;
            toggleLayout.childForceExpandWidth = true;

            _layerEnableButton = MakeButton(toggleRow, L.Particles.LayerOn, 0f,
                ToggleLayerEnabled, ButtonAccentColor);

            // 모양 고르기는 다른 탭과 <b>같은 줄 모양</b>이어야 합니다.
            // 겹만 버튼 하나로 순환하게 뒀더니 "여기만 왜 다르지"가 됐습니다.
            var section1 = AddSectionLabel(parent, L.Particles.Shape);
            BuildLayerTextureRow(section1);

            // 뿜는 자리 — 겹마다 다르게 고를 수 있는 것이 이 기능의 핵심입니다.
            var section2 = AddSectionLabel(parent, L.Particles.Anchor, false);

            var anchorRow = MakeRect("AnchorRow", section2);
            SetHeight(anchorRow, 38f);

            var anchorLayout = anchorRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            anchorLayout.spacing = 6f;
            anchorLayout.childControlWidth = true;
            anchorLayout.childControlHeight = true;
            anchorLayout.childForceExpandWidth = true;

            _anchorButtons.Clear();
            AddAnchorButton(anchorRow, L.Particles.AnchorWhole, WeaponParticleAnchor.Whole);
            AddAnchorButton(anchorRow, L.Particles.AnchorMuzzle, WeaponParticleAnchor.Muzzle);
            AddAnchorButton(anchorRow, L.Particles.AnchorBody, WeaponParticleAnchor.Body);
            AddAnchorButton(anchorRow, L.Particles.AnchorBarrel, WeaponParticleAnchor.Barrel);

            var section3 = AddSectionLabel(parent, L.Particles.Basics);
            _layerPicker = AddColorPicker(section3, color =>
            {
                var layer = CurrentLayer();
                if (layer == null)
                    return;

                layer.color = color;
                ApplyLayerEdit(false);
            });

            AddLayerSlider(section3, L.Particles.FieldIntensity, 0.2f, 8f, "0.0",
                l => l.intensity, (l, v) => l.intensity = v);

            AddLayerSlider(section3, L.Particles.FieldSize, 0.005f, 0.5f, "0.000",
                l => l.size, (l, v) => l.size = v);

            AddLayerSlider(section3, L.Particles.FieldRate, 0f, 120f, "0",
                l => l.rate, (l, v) => l.rate = v);

            AddLayerSlider(section3, L.Particles.FieldLifetime, 0.05f, 3f, "0.00",
                l => l.lifetime, (l, v) => l.lifetime = v);

            AddLayerSlider(section3, L.Particles.FieldSpeed, 0f, 3f, "0.00",
                l => l.speed, (l, v) => l.speed = v);

            AddLayerSlider(section3, L.Particles.FieldSpread, 0f, 1.5f, "0.00",
                l => l.spread, (l, v) => l.spread = v);

            AddLayerSlider(section3, L.Particles.FieldRise, -2f, 2f, "0.00",
                l => l.rise, (l, v) => l.rise = v);

            // 자리 미세 조정 — 세 축을 다 엽니다.
            //
            // 예전에는 앞뒤(Z)만 있었습니다. 총구 옆이나 위로 조금 옮기는 것조차 안 됐고,
            // 무엇보다 <b>어디서 뿜는지 보이지 않아서</b> 숫자를 옮기고 결과를 보고 다시
            // 옮기는 식이었습니다. 아래 표식 버튼이 그 부분을 맡습니다.
            var section4 = AddSectionLabel(parent, L.Particles.Offset, false);

            _gizmoButton = MakeButton(section4, L.Particles.ShowGizmo, 0f, ToggleGizmos, ButtonColor);
            SetHeight((RectTransform)_gizmoButton.transform, 38f);

            AddLayerSlider(section4, L.Particles.OffsetX, -0.6f, 0.6f, "0.00",
                l => l.offset.x, (l, v) => l.offset = new Vector3(v, l.offset.y, l.offset.z),
                needsRebuild: true);

            AddLayerSlider(section4, L.Particles.OffsetY, -0.6f, 0.6f, "0.00",
                l => l.offset.y, (l, v) => l.offset = new Vector3(l.offset.x, v, l.offset.z),
                needsRebuild: true);

            AddLayerSlider(section4, L.Particles.OffsetZ, -0.6f, 0.6f, "0.00",
                l => l.offset.z, (l, v) => l.offset = new Vector3(l.offset.x, l.offset.y, v),
                needsRebuild: true);

            var sectionPulse = AddSectionLabel(parent, L.Particles.PulseSection, false);

            AddLayerSlider(sectionPulse, L.Particles.FieldPulse, 0f, 1f, "0.00",
                l => l.pulseAmount, (l, v) => l.pulseAmount = v);

            AddLayerSlider(sectionPulse, L.Particles.FieldPulseSpeed, 0.2f, 5f, "0.00",
                l => l.pulsePeriod, (l, v) => l.pulsePeriod = v);

            BuildDirectionControls(parent);
            BuildLifecycleControls(parent);
            BuildLookControls(parent);
        }

        /// <summary>
        /// 뿜는 방향.
        ///
        /// "사방"은 구(무기 전체는 상자)에서 퍼지는 기존 동작이고, 나머지는 원뿔입니다.
        /// 총구에서 물이 떨어지는 연출은 <b>아래</b> + 좁은 벌어짐 + 음수 떠오름입니다 —
        /// 방향 없이 떠오름만 음수로 주면 사방으로 튀면서 가라앉아 "물이 폭발"이 됩니다.
        /// </summary>
        private void BuildDirectionControls(Transform parent)
        {
            var section5 = AddSectionLabel(parent, L.Particles.Direction, false);

            // 참격에만 있는 선택지라 근접 탭에서만 보입니다. 총구·오라에는 따라갈 호가
            // 없어서, 보여 줘 봐야 눌러도 아무 일이 안 일어납니다.
            _arcSpreadButton = MakeButton(section5, L.Particles.ArcSpread, 0f, () =>
            {
                var layer = CurrentLayer();
                if (layer == null)
                    return;

                layer.arcSpread = !layer.arcSpread;

                // 뿜는 자리와 셰이프가 통째로 달라지므로 다시 만듭니다.
                ApplyLayerEdit(true);
                SyncLayerFromProfile();
            }, ButtonColor);

            SetHeight((RectTransform)_arcSpreadButton.transform, 38f);

            var row = MakeRect("DirectionRow", section5);
            SetHeight(row, 38f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            _directionButtons.Clear();
            AddDirectionButton(row, L.Particles.DirSphere, WeaponParticleDirection.Sphere);
            AddDirectionButton(row, L.Particles.DirForward, WeaponParticleDirection.Forward);
            AddDirectionButton(row, L.Particles.DirBackward, WeaponParticleDirection.Backward);
            AddDirectionButton(row, L.Particles.DirUp, WeaponParticleDirection.Up);
            AddDirectionButton(row, L.Particles.DirDown, WeaponParticleDirection.Down);

            AddLayerSlider(section5, L.Particles.FieldCone, 0f, 89f, "0",
                l => l.coneAngle, (l, v) => l.coneAngle = v);
        }

        private void AddDirectionButton(Transform parent, string label, WeaponParticleDirection direction)
        {
            var button = MakeButton(parent, label, 0f, () =>
            {
                var layer = CurrentLayer();
                if (layer == null || layer.direction == direction)
                    return;

                layer.direction = direction;
                ApplyLayerEdit(true);
                SyncLayerFromProfile();
            }, ButtonColor);

            _directionButtons.Add(new KeyValuePair<WeaponParticleDirection, Button>(direction, button));
        }

        /// <summary>
        /// 수명에 따른 변화.
        ///
        /// 이 묶음이 없으면 무엇을 골라도 "같은 색 알갱이가 뿅 나타났다 뿅 사라지는"
        /// 그림이 됩니다. 불꽃은 식어가며 색이 바뀌고 연기는 커지며 옅어집니다.
        /// </summary>
        private void BuildLifecycleControls(Transform parent)
        {
            var section6 = AddSectionLabel(parent, L.Particles.Lifecycle, false);

            AddLayerSlider(section6, L.Particles.FieldAlphaStart, 0f, 1f, "0.00",
                l => l.alphaStart, (l, v) => l.alphaStart = v);

            AddLayerSlider(section6, L.Particles.FieldAlphaEnd, 0f, 1f, "0.00",
                l => l.alphaEnd, (l, v) => l.alphaEnd = v);

            AddLayerSlider(section6, L.Particles.FieldSizeStart, 0.05f, 3f, "0.00",
                l => l.sizeStart, (l, v) => l.sizeStart = v);

            AddLayerSlider(section6, L.Particles.FieldSizeEnd, 0.05f, 3f, "0.00",
                l => l.sizeEnd, (l, v) => l.sizeEnd = v);

            _useColorEndButton = MakeButton(section6, L.Particles.UseColorEnd, 0f, () =>
            {
                var layer = CurrentLayer();
                if (layer == null)
                    return;

                layer.useColorEnd = !layer.useColorEnd;
                ApplyLayerEdit(false);
                SyncLayerFromProfile();
            }, ButtonColor);

            SetHeight((RectTransform)_useColorEndButton.transform, 38f);

            // 끝 색 선택기는 쓸 때만 보입니다 — 안 쓰는 큰 위젯이 늘 펼쳐져 있으면
            // 그 아래 항목까지 스크롤로 밀려납니다.
            _layerEndColorRows = MakeStack("LayerEndColor", section6);
            var section7 = AddSectionLabel(_layerEndColorRows.transform, L.Particles.ColorEnd, true);

            _layerEndPicker = AddColorPicker(section7, color =>
            {
                var layer = CurrentLayer();
                if (layer == null)
                    return;

                layer.colorEnd = color;
                ApplyLayerEdit(false);
            });
        }

        private void BuildLookControls(Transform parent)
        {
            var section8 = AddSectionLabel(parent, L.Particles.Look, false);

            _stretchButton = MakeButton(section8, L.Particles.Stretch, 0f, () =>
            {
                var layer = CurrentLayer();
                if (layer == null)
                    return;

                layer.stretch = !layer.stretch;

                // 빌보드와 늘이기는 렌더 방식이 달라 다시 만드는 편이 확실합니다.
                ApplyLayerEdit(true);
                SyncLayerFromProfile();
            }, ButtonColor);

            SetHeight((RectTransform)_stretchButton.transform, 38f);

            AddLayerSlider(section8, L.Particles.FieldStretch, 0.2f, 8f, "0.0",
                l => l.stretchScale, (l, v) => l.stretchScale = v);

            AddLayerSlider(section8, L.Particles.FieldNoise, 0f, 2f, "0.00",
                l => l.noise, (l, v) => l.noise = v);

            AddLayerSlider(section8, L.Particles.FieldSpin, -360f, 360f, "0",
                l => l.spin, (l, v) => l.spin = v);
        }

        /// <summary>겹 선택 버튼 줄. 겹이 추가·삭제될 때마다 다시 만듭니다.</summary>
        private void BuildLayerStrip(Transform parent)
        {
            var row = MakeRect("LayerStrip", parent);
            SetHeight(row, 40f);
            _layerRow = row;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            RebuildLayerButtons();
        }

        private void RebuildLayerButtons()
        {
            if (_layerRow == null)
                return;

            foreach (var button in _layerButtons)
            {
                if (button != null)
                    UnityEngine.Object.DestroyImmediate(button.gameObject);
            }

            _layerButtons.Clear();

            var host = CurrentLayerHost();
            int count = host?.Layers.Length ?? 0;

            for (int i = 0; i < count; i++)
            {
                int index = i;
                var layer = host!.Layers[i];

                string label = layer != null && !layer.enabled
                    ? $"<color=#888888>{i + 1}</color>"
                    : (i + 1).ToString();

                var button = MakeButton(_layerRow, label, 0f, () =>
                {
                    _editingLayer = index;
                    SyncLayerFromProfile();
                }, ButtonColor);

                _layerButtons.Add(button);
            }

            if (_font != null)
                ApplyFont(_layerRow.gameObject);
        }

        private void AddAnchorButton(Transform parent, string label, WeaponParticleAnchor anchor)
        {
            var button = MakeButton(parent, label, 0f, () =>
            {
                var layer = CurrentLayer();
                if (layer == null || layer.anchor == anchor)
                    return;

                layer.anchor = anchor;

                // 붙는 자리가 바뀌면 오브젝트를 다른 부모 밑에 다시 만들어야 합니다.
                ApplyLayerEdit(true);
                SyncLayerFromProfile();
            }, ButtonColor);

            _anchorButtons.Add(new KeyValuePair<WeaponParticleAnchor, Button>(anchor, button));
        }

        private void AddLayerSlider(Transform parent, string label, float min, float max, string format,
            Func<WeaponEffectLayer, float> get, Action<WeaponEffectLayer, float> set,
            bool needsRebuild = false)
        {
            var row = new LayerSliderRow
            {
                Format = format,
                Get = get,
                NeedsRebuild = needsRebuild,
            };

            // 다른 줄을 만드는 AddSlider는 WeaponAuraProfile 본체에 묶여 있어서 못 씁니다.
            // 줄 구성은 같게 맞춰 두어야 칸을 옮겨도 같은 화면으로 읽힙니다.
            var rowGo = MakeRect("LayerRow_" + label, parent);
            SetHeight(rowGo, 36f);

            var layout = rowGo.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var caption = MakeText("Label", rowGo, label, 19, TextColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(caption.rectTransform, 130f);

            var slider = MakeSlider(rowGo, min, max);
            slider.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var valueText = MakeText("Value", rowGo, "0", 19, DimTextColor, TextAlignmentOptions.MidlineRight);
            SetWidth(valueText.rectTransform, 90f);

            row.Slider = slider;
            row.ValueText = valueText;

            slider.onValueChanged.AddListener(value =>
            {
                var layer = CurrentLayer();
                if (layer == null)
                    return;

                set(layer, value);
                valueText.text = value.ToString(format);
                ApplyLayerEdit(row.NeedsRebuild);
            });

            _layerSliders.Add(row);
        }

        // ── 조작 ────────────────────────────────────────────────────

        /// <summary>표식 켜고 끄기. 켜면 무대를 다시 세워 표식을 붙입니다.</summary>
        private void ToggleGizmos()
        {
            if (_preview == null)
                return;

            _preview.ShowGizmos = !_preview.ShowGizmos;
            _preview.RequestRebuild();

            SyncLayerFromProfile();
        }

        private void ToggleLayerEnabled()
        {
            var layer = CurrentLayer();
            if (layer == null)
                return;

            layer.enabled = !layer.enabled;
            ApplyLayerEdit(true);
            RebuildLayerButtons();
            SyncLayerFromProfile();
        }

        /// <summary>
        /// 모양 고르기 줄 — 다른 탭과 같은 구성(◀ 이름 ▶ ↻).
        ///
        /// 목록도 <b>같은 것</b>을 씁니다(<see cref="MuzzleShapeChoices"/>): 내장 도형,
        /// 직접 그린 도형, assets/vfx_textures의 PNG. 하나를 그려 네 곳에 돌려 쓸 수
        /// 있어야 하는데, 겹만 따로 목록을 만들면 그 약속이 깨집니다.
        ///
        /// 처음에는 여기에 총알 머리 도형(Capsule·Arrow…)도 넣었습니다. <b>잘못이었습니다</b> —
        /// 그 이름들은 <c>MuzzleFlashShapes.Resolve</c>가 해석하지 못해서, 골라도 조용히
        /// 기본 도형이 나왔습니다. "모양이 바뀌긴 하는데 안 바뀐 것 같다"의 원인입니다.
        /// </summary>
        private void BuildLayerTextureRow(Transform parent)
        {
            var row = MakeRect("LayerTextureRow", parent);
            SetHeight(row, 40f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            MakeButton(row, L.Muzzle.ShapePrev, 56f, () => CycleLayerTexture(-1), ButtonColor);

            _layerTextureLabel = MakeText("LayerTextureName", row, "-", 19, TextColor,
                TextAlignmentOptions.Center);
            _layerTextureLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MakeButton(row, L.Muzzle.ShapeNext, 56f, () => CycleLayerTexture(1), ButtonColor);

            // 그림으로 보고 고르는 창. ◀ ▶ 로 넘기는 것은 옆에 남겨 둡니다 —
            // 바로 옆 모양으로 바꿔 보는 데는 그쪽이 빠릅니다.
            MakeButton(row, L.Picker.Open, 110f, () =>
            {
                var layer = CurrentLayer();
                if (layer == null)
                    return;

                string current = string.IsNullOrEmpty(layer.textureName)
                    ? layer.shape.ToString()
                    : layer.textureName;

                OpenShapePicker(current, picked =>
                {
                    var target = CurrentLayer();
                    if (target == null)
                        return;

                    ApplyLayerShape(target, picked);

                    ApplyLayerEdit(true);
                    SyncLayerFromProfile();
                });
            }, ButtonAccentColor);
        }

        /// <summary>
        /// 고른 이름을 레이어에 넣습니다 — 내장 도형이면 enum으로, 아니면 파일 이름으로.
        /// (<c>Resolve</c>는 textureName이 비어 있을 때만 shape를 봅니다)
        /// </summary>
        private static void ApplyLayerShape(WeaponEffectLayer target, string picked)
        {
            if (Enum.TryParse(picked, out MuzzleFlashShape parsed)
                && Array.IndexOf(MuzzleFlashShapes.All, parsed) >= 0)
            {
                target.shape = parsed;
                target.textureName = "";
                return;
            }

            target.textureName = picked;
        }

        private void CycleLayerTexture(int delta)
        {
            var layer = CurrentLayer();
            if (layer == null)
                return;

            var choices = MuzzleShapeChoices();
            if (choices.Count == 0)
                return;

            string current = string.IsNullOrEmpty(layer.textureName)
                ? layer.shape.ToString()
                : layer.textureName;

            int index = choices.IndexOf(current);
            if (index < 0)
                index = 0;

            index = (index + delta + choices.Count) % choices.Count;

            ApplyLayerShape(layer, choices[index]);

            ApplyLayerEdit(true);
            SyncLayerFromProfile();
        }

        private void AddAuraLayer()
        {
            var host = CurrentLayerHost();
            if (host == null)
                return;

            var created = host.AddLayer();
            if (created == null)
            {
                ShowHint(string.Format(L.Particles.LayerFull, host.LayerLimit));
                return;
            }

            _editingLayer = host.Layers.Length - 1;

            ApplyLayerEdit(true);
            RebuildLayerButtons();
            SyncLayerFromProfile();
        }

        private void RemoveAuraLayer()
        {
            var host = CurrentLayerHost();
            if (host == null || host.Layers.Length == 0)
                return;

            if (!host.RemoveLayer(_editingLayer))
                return;

            _editingLayer = Mathf.Max(0, Mathf.Min(_editingLayer, host.Layers.Length - 1));

            ApplyLayerEdit(true);
            RebuildLayerButtons();
            SyncLayerFromProfile();
        }

        /// <summary>
        /// 편집을 실제 무기에 반영합니다.
        ///
        /// 값만 바뀐 경우와 구조가 바뀐 경우를 갈라야 합니다. 구조가 바뀌면 다시 만들고,
        /// 아니면 붙어 있는 것에 값만 바로 씁니다(슬라이더가 끌리는 동안 따라오도록).
        /// </summary>
        private void ApplyLayerEdit(bool rebuild)
        {
            // 개별 무기를 고치는 중이면 탭과 무관하게 알려야 합니다 — 무기 목록의
            // 〈전용 설정 있음〉 표시가 이 신호로 갱신됩니다.
            if (EditingOverride)
                WeaponOverrides.NotifyChanged();

            // 총구·참격 레이어는 쏘거나 휘두를 때 그 자리에서 새로 만들어지므로
            // 지금 다시 만들 것이 없습니다. 다음 발사에 바뀐 값이 그대로 나갑니다.
            if (_tab != WindowTab.Aura)
                return;

            if (rebuild)
            {
                WeaponAuraLayerSystem.RebuildNow();

                // 레이어가 늘거나 자리·모양이 바뀌면 미리보기 무대도 다시 세워야 합니다.
                _preview?.RequestRebuild();
            }
            else
            {
                WeaponAuraLayerSystem.ApplyNow();
                _preview?.ApplyLayerValues();
            }
        }

        // ── 갱신 ────────────────────────────────────────────────────

        private void SyncLayerFromProfile()
        {
            var host = CurrentLayerHost();
            var layer = CurrentLayer();

            for (int i = 0; i < _layerButtons.Count; i++)
            {
                var button = _layerButtons[i];
                if (button?.targetGraphic != null)
                    button.targetGraphic.color = i == _editingLayer ? ButtonAccentColor : ButtonColor;
            }

            if (_removeLayerButton != null)
                _removeLayerButton.gameObject.SetActive(layer != null);

            if (_layerEnableButton != null)
            {
                _layerEnableButton.gameObject.SetActive(layer != null);

                var label = _layerEnableButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null && layer != null)
                    label.text = layer.enabled ? L.Particles.LayerOn : L.Particles.LayerOff;

                if (_layerEnableButton.targetGraphic != null && layer != null)
                    _layerEnableButton.targetGraphic.color = layer.enabled ? ButtonAccentColor : ButtonColor;
            }

            foreach (var pair in _anchorButtons)
            {
                if (pair.Value?.targetGraphic != null)
                {
                    pair.Value.targetGraphic.color =
                        layer != null && pair.Key == layer.anchor ? ButtonAccentColor : ButtonColor;
                }
            }

            if (_layerTextureLabel != null)
            {
                // 내장 도형은 번역된 이름으로 보여 줍니다.
                //
                // enum 이름을 그대로 찍으면 이 줄만 영어로 남습니다 — 총구 화염·근접 참격은
                // 이미 LocalizedShapeName을 거치고 있었는데 겹만 빼먹었습니다.
                // 직접 그린 도형·PNG는 사용자가 붙인 파일 이름이라 번역 대상이 아닙니다.
                _layerTextureLabel.text = layer == null
                    ? "-"
                    : (string.IsNullOrEmpty(layer.textureName)
                        ? LocalizedShapeName(layer.shape)
                        : layer.textureName);
            }

            foreach (var row in _layerSliders)
            {
                if (row.Slider == null)
                    continue;

                // 줄 전체(라벨·슬라이더·값)를 함께 감춥니다.
                var line = row.Slider.transform.parent;
                if (line != null)
                    line.gameObject.SetActive(layer != null);

                if (layer == null)
                    continue;

                float value = row.Get(layer);
                row.Slider.SetValueWithoutNotify(value);
                row.ValueText.text = value.ToString(row.Format);
            }

            if (_layerPicker != null && layer != null)
                _layerPicker.SetColor(layer.color);

            foreach (var pair in _directionButtons)
            {
                if (pair.Value?.targetGraphic != null)
                {
                    pair.Value.targetGraphic.color =
                        layer != null && pair.Key == layer.direction ? ButtonAccentColor : ButtonColor;
                }
            }

            if (_useColorEndButton?.targetGraphic != null)
            {
                _useColorEndButton.targetGraphic.color =
                    layer != null && layer.useColorEnd ? ButtonAccentColor : ButtonColor;
            }

            if (_layerEndColorRows != null)
                _layerEndColorRows.SetActive(layer != null && layer.useColorEnd);

            if (_layerEndPicker != null && layer != null)
                _layerEndPicker.SetColor(layer.colorEnd);

            if (_arcSpreadButton != null)
            {
                // 참격에만 있는 선택지입니다.
                _arcSpreadButton.gameObject.SetActive(_tab == WindowTab.Melee && layer != null);

                if (_arcSpreadButton.targetGraphic != null && layer != null)
                {
                    _arcSpreadButton.targetGraphic.color =
                        layer.arcSpread ? ButtonAccentColor : ButtonColor;
                }
            }

            if (_stretchButton?.targetGraphic != null)
            {
                _stretchButton.targetGraphic.color =
                    layer != null && layer.stretch ? ButtonAccentColor : ButtonColor;
            }

            if (_gizmoButton?.targetGraphic != null)
            {
                _gizmoButton.targetGraphic.color =
                    _preview != null && _preview.ShowGizmos ? ButtonAccentColor : ButtonColor;
            }

            if (_layerTitle != null)
            {
                // 어느 탭의 레이어인지 제목에 적습니다 — 창이 하나뿐이라 열어 놓고 보면
                // 무엇을 만지는 중인지 알 길이 없습니다.
                string scope = _tab == WindowTab.Muzzle ? L.Tab.Muzzle
                    : _tab == WindowTab.Melee ? L.Tab.Melee
                    : L.Tab.Aura;

                _layerTitle.text = $"{L.Particles.Layers} — {scope}";
            }

            if (_layerStatus != null)
            {
                int count = host?.Layers.Length ?? 0;

                if (count == 0)
                {
                    _layerStatus.text = L.Particles.LayerEmpty;
                    _layerStatus.color = WarnTextColor;
                }
                else
                {
                    _layerStatus.color = DimTextColor;
                    // 괄호 안은 <b>게임에 실제로 붙어 있는</b> 겹 수입니다. 편집 중인 무기를
                    // 손에 들고 있지 않으면 0이 정상입니다 — 그 사실이 드러나야
                    // "설정은 했는데 왜 0이지"를 스스로 답할 수 있습니다.
                    _layerStatus.text =
                        $"{L.Particles.Layers}: {count} / {(host != null ? host.LayerLimit : 0)}  " +
                        $"({L.Particles.LiveCount}: {WeaponAuraLayerSystem.LiveLayerCount})";
                }
            }
        }
    }
}
