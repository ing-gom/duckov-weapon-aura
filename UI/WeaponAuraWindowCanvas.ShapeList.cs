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
    /// 만들어 둔 도형 목록.
    ///
    /// 예전에는 <b>이름을 쳐서</b> 불러오고 지웠습니다. 이름을 정확히 기억해야 했고,
    /// 오타 하나면 "그런 도형이 없다"는 안내만 나왔습니다. 무엇이 있는지 보이지도 않아서
    /// 지우려면 먼저 이름부터 알아내야 했습니다.
    ///
    /// 목록으로 바꾸면 그 문제가 통째로 없어집니다 — 그림을 보고 고르고, 그 자리에서
    /// 지웁니다. 편집기 모달 왼쪽에 붙여서 "그리기"와 "가진 것"이 한 화면에 있습니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private RectTransform? _shapeListContent;
        private readonly List<GameObject> _shapeListCells = new List<GameObject>();

        /// <summary>목록 칸에 그린 미리보기 텍스처. 다시 그릴 때 함께 버립니다.</summary>
        private readonly List<Texture2D> _shapeListThumbs = new List<Texture2D>();

        private const float ShapeListCellHeight = 46f;
        private const float ShapeThumbSize = 36f;

        private void BuildShapeList(Transform parent)
        {
            var column = MakeImage("ShapeListColumn", parent, SectionColor).rectTransform;

            var element = column.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 280f;
            element.flexibleWidth = 0f;

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = MakeText("ShapeListTitle", column, L.Shape.ListTitle, 19, TextColor,
                TextAlignmentOptions.MidlineLeft);
            SetHeight(title.rectTransform, 28f);

            var scrollGo = MakeRect("ShapeListScroll", column);
            scrollGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 250f;

            _shapeListContent = BuildScrollBody(scrollGo);

            RefreshShapeList();
        }

        /// <summary>목록을 다시 그립니다. 저장·삭제 뒤에 부릅니다.</summary>
        private void RefreshShapeList()
        {
            if (_shapeListContent == null)
                return;

            foreach (var cell in _shapeListCells)
            {
                if (cell != null)
                    UnityEngine.Object.DestroyImmediate(cell);
            }

            _shapeListCells.Clear();

            // 칸과 함께 만든 미리보기도 버립니다 — 안 그러면 목록을 새로 그릴 때마다
            // 텍스처가 쌓입니다.
            foreach (var thumb in _shapeListThumbs)
            {
                if (thumb != null)
                    UnityEngine.Object.DestroyImmediate(thumb);
            }

            _shapeListThumbs.Clear();

            List<CustomShape> shapes;
            try
            {
                shapes = CustomShapes.All;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 도형 목록을 읽지 못했습니다: {ex.Message}");
                return;
            }

            if (shapes.Count == 0)
            {
                var empty = MakeText("ShapeListEmpty", _shapeListContent, L.Shape.ListEmpty, 17,
                    DimTextColor, TextAlignmentOptions.MidlineLeft);
                SetHeight(empty.rectTransform, 30f);
                _shapeListCells.Add(empty.gameObject);
                return;
            }

            foreach (var shape in shapes)
            {
                if (shape == null || string.IsNullOrEmpty(shape.name))
                    continue;

                _shapeListCells.Add(BuildShapeListCell(shape));
            }

            if (_font != null)
                ApplyFont(_shapeListContent.gameObject);
        }

        private GameObject BuildShapeListCell(CustomShape shape)
        {
            var row = MakeImage($"Shape_{shape.name}", _shapeListContent!, ButtonColor);
            SetHeight(row.rectTransform, ShapeListCellHeight);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // 그림 — 이름보다 이게 먼저 눈에 들어와야 고르기가 됩니다.
            var thumb = MakeImage("Thumb", row.transform, Color.white);
            var thumbElement = thumb.gameObject.AddComponent<LayoutElement>();
            thumbElement.preferredWidth = ShapeThumbSize;
            thumbElement.preferredHeight = ShapeThumbSize;
            thumbElement.flexibleWidth = 0f;

            var texture = SafeShapeTexture(shape);
            if (texture != null)
            {
                thumb.sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }

            var name = MakeText("Name", row.transform, shape.name, 17, TextColor,
                TextAlignmentOptions.MidlineLeft);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;

            string captured = shape.name;

            // 누르면 판으로 불러와 고칩니다 — "불러오기" 버튼이 하던 일입니다.
            MakeButton(row.transform, L.Shape.Edit, 66f, () => LoadShapeByName(captured), ButtonColor);

            // 지우기는 그 줄에 둡니다. 이름을 쳐서 지우던 것을 대신합니다.
            MakeButton(row.transform, L.Shape.Delete, 46f, () => DeleteShapeByName(captured),
                new Color(0.55f, 0.22f, 0.22f, 0.95f));

            return row.gameObject;
        }

        /// <summary>목록 칸에 쓸 미리보기. 실패해도 목록은 보여야 합니다.</summary>
        private Texture2D? SafeShapeTexture(CustomShape shape)
        {
            try
            {
                var texture = CustomShapes.GetTexture(shape.name);
                if (texture != null)
                    _shapeListThumbs.Add(texture);

                return texture;
            }
            catch
            {
                return null;
            }
        }

        private void LoadShapeByName(string name)
        {
            SetShapeName(name);
            LoadSelectedShape();
        }

        private void DeleteShapeByName(string name)
        {
            SetShapeName(name);
            DeleteSelectedShape();
            RefreshShapeList();
        }
    }
}
