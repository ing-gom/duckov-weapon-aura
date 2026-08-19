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
    /// 모양 고르기 — 내장 도형 · 직접 그린 도형 · 이미지 파일을 <b>그림으로 보고</b> 고릅니다.
    ///
    /// 예전에는 <c>◀ 이름 ▶</c>로 한 칸씩 넘기는 것뿐이었습니다. 파일이 늘어나면 원하는
    /// 것을 지나쳐서 한 바퀴를 다시 돌아야 했고, 무엇보다 <b>이름만 보고 골라야</b> 했습니다.
    /// 모양을 이름으로 기억하는 사람은 없습니다.
    ///
    /// 고른 결과를 어디에 넣을지는 <see cref="_shapePickTarget"/>이 들고 있습니다 — 이 창은
    /// 여섯 군데(오라 · 겹 · 자국 · 총알 머리 · 총구 화염 · 근접 참격)가 함께 씁니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private const float ShapeCellSize = 84f;
        private const float ShapePreviewSize = 56f;

        private GameObject? _shapePickerRoot;
        private RectTransform? _shapePickerGrid;
        private TextMeshProUGUI? _shapePickerPath;

        private readonly List<GameObject> _shapePickerCells = new List<GameObject>();
        private readonly List<Texture2D> _shapePickerThumbs = new List<Texture2D>();

        /// <summary>고른 이름을 받아 갈 곳. 창을 열 때 정합니다.</summary>
        private Action<string>? _shapePickTarget;

        /// <summary>지금 쓰이고 있는 이름 (강조 표시용)</summary>
        private string _shapePickCurrent = "";

        private bool ShapePickerOpen => _shapePickerRoot != null && _shapePickerRoot.activeSelf;

        /// <summary>
        /// 모양 고르기 창을 엽니다.
        /// </summary>
        /// <param name="current">지금 쓰는 이름 (빈 문자열이면 "기본")</param>
        /// <param name="onPicked">고른 이름을 받을 곳</param>
        private void OpenShapePicker(string current, Action<string> onPicked)
        {
            _shapePickCurrent = current ?? "";
            _shapePickTarget = onPicked;

            if (_shapePickerRoot == null)
                BuildShapePicker();

            if (_shapePickerRoot == null)
                return;

            _shapePickerRoot.SetActive(true);
            RefreshShapePicker();
        }

        /// <summary>
        /// 고른 이름을 <b>도형</b>과 <b>파일</b>로 갈라 넣습니다.
        ///
        /// 자리마다 쓰는 enum이 다르고(총구 계열 · 총알 머리 계열) 파일 이름은 enum이
        /// 아니므로, 넣는 쪽을 각자 넘겨받습니다. 갈라 넣는 규칙 자체는 어디서나 같아서
        /// 여기 한 번만 적습니다 — 여섯 자리에 같은 if를 여섯 번 적을 이유가 없습니다.
        /// </summary>
        private void OpenShapePickerFor<TShape>(string current, TShape[] builtIn,
            Action<TShape> setShape, Action<string> setTexture, Action after)
            where TShape : struct, Enum
        {
            OpenShapePicker(current, picked =>
            {
                if (Enum.TryParse(picked, out TShape parsed) && Array.IndexOf(builtIn, parsed) >= 0)
                {
                    setShape(parsed);
                    setTexture("");
                }
                else
                {
                    setTexture(picked);
                }

                after();
            });
        }

        /// <summary>enum 없이 이름만 쓰는 자리(오라·링·참격 그림)용.</summary>
        private void OpenShapePickerForName(string current, Action<string> setName, Action after)
        {
            OpenShapePicker(current, picked =>
            {
                setName(picked);
                after();
            });
        }

        private void CloseShapePicker()
        {
            if (_shapePickerRoot != null)
                _shapePickerRoot.SetActive(false);

            _shapePickTarget = null;
        }

        private void BuildShapePicker()
        {
            if (_canvasRoot == null)
                return;

            var backdrop = MakeImage("ShapePickerBackdrop", _canvasRoot.transform, BackdropColor);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _shapePickerRoot = backdrop.gameObject;

            var panel = MakeImage("ShapePickerPanel", backdrop.transform, PanelColor);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(880f, 620f);

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

            var title = MakeText("Title", header, L.Picker.Title, 26, TextColor,
                TextAlignmentOptions.MidlineLeft);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // 폴더에 이미지를 새로 넣었을 때 게임을 껐다 켜지 않아도 되게.
            MakeButton(header, L.Muzzle.ShapeRescan, 60f, () =>
            {
                WeaponAuraResources.GetTextureNames(refresh: true);
                RefreshShapePicker();
            }, ButtonColor);

            MakeButton(header, L.Library.Close, 110f, CloseShapePicker, ButtonColor);

            var scrollGo = MakeRect("Scroll", panel.transform);
            scrollGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 470f;

            var content = BuildScrollBody(scrollGo);

            // 세로 목록을 격자로 바꿉니다. Destroy는 프레임 끝까지 미뤄져서 레이아웃 그룹이
            // 둘 붙은 상태가 되므로 반드시 DestroyImmediate여야 합니다.
            var vertical = content.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                UnityEngine.Object.DestroyImmediate(vertical);

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.cellSize = new Vector2(ShapeCellSize, ShapeCellSize + 18f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;

            _shapePickerGrid = content;

            // 이미지를 어디에 넣으면 되는지 알려 줍니다 — 폴더를 못 찾아 헤매는 것이
            // 이 기능에서 제일 흔한 막힘입니다.
            _shapePickerPath = MakeText("Path", panel.transform, "", 15, DimTextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(_shapePickerPath.rectTransform, 26f);

            ApplyFont(panel.gameObject);
        }

        private void RefreshShapePicker()
        {
            if (_shapePickerGrid == null)
                return;

            foreach (var cell in _shapePickerCells)
            {
                if (cell != null)
                    UnityEngine.Object.DestroyImmediate(cell);
            }

            _shapePickerCells.Clear();

            foreach (var thumb in _shapePickerThumbs)
            {
                if (thumb != null)
                    UnityEngine.Object.DestroyImmediate(thumb);
            }

            _shapePickerThumbs.Clear();

            // 기본(그림 없음)
            AddShapeCell("", L.Section.TextureDefault, null);

            foreach (var shape in MuzzleFlashShapes.All)
            {
                string name = shape.ToString();
                AddShapeCell(name, LocalizedShapeName(shape), MuzzleFlashShapes.Get(shape));
            }

            try
            {
                foreach (var drawn in CustomShapes.All)
                {
                    if (drawn == null || string.IsNullOrEmpty(drawn.name))
                        continue;

                    var texture = CustomShapes.GetTexture(drawn.name);
                    if (texture != null)
                        _shapePickerThumbs.Add(texture);

                    AddShapeCell(drawn.name, drawn.name, texture);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 그린 도형 목록 실패: {ex.Message}");
            }

            try
            {
                foreach (string file in WeaponAuraResources.GetTextureNames())
                {
                    if (string.IsNullOrEmpty(file))
                        continue;

                    AddShapeCell(file, file, WeaponAuraResources.LoadTexture(file));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 이미지 목록 실패: {ex.Message}");
            }

            if (_shapePickerPath != null)
                _shapePickerPath.text = WeaponAuraResources.GetUserTextureFolder() ?? "";

            ApplyFont(_shapePickerGrid.gameObject);
        }

        private void AddShapeCell(string name, string label, Texture2D? texture)
        {
            bool selected = name == _shapePickCurrent;

            var cell = MakeImage($"Shape_{label}", _shapePickerGrid!,
                selected ? ButtonAccentColor : ButtonColor);

            var button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            string captured = name;
            button.onClick.AddListener(() =>
            {
                _shapePickTarget?.Invoke(captured);
                CloseShapePicker();
            });

            var layout = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 2f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            var preview = MakeImage("Preview", cell.transform,
                texture != null ? Color.white : new Color(1f, 1f, 1f, 0.08f));

            var previewElement = preview.gameObject.AddComponent<LayoutElement>();
            previewElement.preferredHeight = ShapePreviewSize;
            previewElement.flexibleHeight = 0f;

            if (texture != null)
            {
                preview.sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                preview.preserveAspect = true;
            }

            preview.raycastTarget = false;

            var text = MakeText("Name", cell.transform, label, 13, TextColor,
                TextAlignmentOptions.Center);
            SetHeight(text.rectTransform, 18f);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            _shapePickerCells.Add(cell.gameObject);
        }
    }
}
