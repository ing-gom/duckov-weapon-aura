using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 도형 그리기 판.
    ///
    /// 칸마다 버튼을 두면 24×24 = 576개짜리 UI가 되어 창이 무거워집니다. 대신 칸을 그려 넣은
    /// 텍스처 한 장을 띄우고, 그 위의 드래그 영역에서 포인터 위치를 칸 번호로 환산합니다.
    /// 오브젝트는 두 개면 충분하고, 칠하는 느낌도 이쪽이 더 좋습니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        /// <summary>칸 하나를 몇 픽셀로 그릴지 (격자선 포함)</summary>
        private const int ShapeCellPixels = 8;

        private RawImage? _shapeCanvasImage;
        private Texture2D? _shapeCanvasTexture;
        private TMP_InputField? _shapeNameField;
        private TextMeshProUGUI? _shapeEditorHint;

        private bool[]? _shapeCells;

        /// <summary>지금 드래그가 칠하는 중인지 지우는 중인지</summary>
        private bool _shapePaintValue = true;

        private bool[] ShapeCells => _shapeCells ??= new bool[CustomShapes.GridSize * CustomShapes.GridSize];

        // ── 구성 ────────────────────────────────────────────────────

        private void BuildShapeEditor(Transform parent)
        {
            AddSectionLabel(parent, L.Muzzle.SectionDraw);

            var row = MakeRect("ShapeEditorRow", parent);
            SetHeight(row, 232f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            // ── 칠하는 판 ──
            var frame = MakeImage("ShapeCanvasFrame", row, new Color(0f, 0f, 0f, 0.75f));
            var frameElement = frame.gameObject.AddComponent<LayoutElement>();
            frameElement.preferredWidth = 232f;
            frameElement.flexibleWidth = 0f;

            var canvasGo = MakeRect("ShapeCanvas", frame.transform);
            canvasGo.anchorMin = Vector2.zero;
            canvasGo.anchorMax = Vector2.one;
            canvasGo.offsetMin = new Vector2(8f, 8f);
            canvasGo.offsetMax = new Vector2(-8f, -8f);

            _shapeCanvasImage = canvasGo.gameObject.AddComponent<RawImage>();
            _shapeCanvasImage.raycastTarget = true;

            var drag = canvasGo.gameObject.AddComponent<PointerDragArea>();

            // 누른 칸이 비어 있으면 칠하고, 차 있으면 지웁니다. 드래그하는 동안 그 동작을
            // 유지해야 손이 떨릴 때 칠했다 지웠다를 반복하지 않습니다.
            drag.OnPressed = position =>
            {
                _shapePaintValue = !ReadCell(position);
                PaintCell(position);
            };
            drag.OnPicked = PaintCell;

            // ── 오른쪽 조작 ──
            var side = MakeRect("ShapeEditorSide", row);
            side.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var sideLayout = side.gameObject.AddComponent<VerticalLayoutGroup>();
            sideLayout.spacing = 6f;
            sideLayout.childControlWidth = true;
            sideLayout.childControlHeight = true;
            sideLayout.childForceExpandWidth = true;
            sideLayout.childForceExpandHeight = false;

            _shapeNameField = MakeInputField(side, L.Muzzle.ShapeName, 300f,
                TMP_InputField.CharacterValidation.None);
            _shapeNameField.characterLimit = 16;
            SetHeight((RectTransform)_shapeNameField.transform.parent, 34f);

            AddShapeEditorButton(side, L.Muzzle.ShapeSave, SaveDrawnShape, ButtonAccentColor);
            AddShapeEditorButton(side, L.Muzzle.ShapeRandom, RandomizeDrawnShape, ButtonColor);
            AddShapeEditorButton(side, L.Muzzle.ShapeClear, ClearDrawnShape, ButtonColor);
            AddShapeEditorButton(side, L.Muzzle.ShapeLoad, LoadSelectedShape, ButtonColor);
            AddShapeEditorButton(side, L.Muzzle.ShapeDelete, DeleteSelectedShape, ButtonColor);

            _shapeEditorHint = MakeText("ShapeEditorHint", side, L.Muzzle.ShapeHint, 15, DimTextColor,
                TextAlignmentOptions.TopLeft);
            SetHeight(_shapeEditorHint.rectTransform, 34f);
            _shapeEditorHint.enableWordWrapping = true;

            RedrawShapeCanvas();
        }

        private void AddShapeEditorButton(Transform parent, string label, Action onClick, Color color)
        {
            var button = MakeButton(parent, label, 0f, onClick, color);
            SetHeight((RectTransform)button.transform, 32f);
        }

        // ── 칠하기 ──────────────────────────────────────────────────

        /// <summary>드래그 영역이 주는 0~1 위치를 칸 번호로 바꿉니다.</summary>
        private static bool TryCellIndex(Vector2 normalized, out int index)
        {
            int grid = CustomShapes.GridSize;

            int x = Mathf.Clamp(Mathf.FloorToInt(normalized.x * grid), 0, grid - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normalized.y * grid), 0, grid - 1);

            index = y * grid + x;
            return true;
        }

        private bool ReadCell(Vector2 normalized)
        {
            return TryCellIndex(normalized, out int index) && ShapeCells[index];
        }

        private void PaintCell(Vector2 normalized)
        {
            if (!TryCellIndex(normalized, out int index))
                return;

            if (ShapeCells[index] == _shapePaintValue)
                return;

            ShapeCells[index] = _shapePaintValue;
            RedrawShapeCanvas();
        }

        /// <summary>
        /// 칸 상태를 텍스처로 다시 그립니다.
        ///
        /// 칸마다 <see cref="ShapeCellPixels"/>칸씩 채우고 경계에 격자선을 남겨서
        /// 모눈종이처럼 보이게 합니다 — 어디를 칠했는지 세어 볼 수 있어야 합니다.
        /// </summary>
        private void RedrawShapeCanvas()
        {
            if (_shapeCanvasImage == null)
                return;

            int grid = CustomShapes.GridSize;
            int side = grid * ShapeCellPixels;

            if (_shapeCanvasTexture == null)
            {
                _shapeCanvasTexture = new Texture2D(side, side, TextureFormat.RGBA32, false)
                {
                    name = "WeaponAura_ShapeCanvas",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            var filled = new Color32(235, 242, 250, 255);
            var empty = new Color32(18, 26, 34, 255);
            var line = new Color32(38, 52, 66, 255);
            var centre = new Color32(58, 78, 96, 255);

            var pixels = new Color32[side * side];

            for (int py = 0; py < side; py++)
            {
                int cy = py / ShapeCellPixels;

                for (int px = 0; px < side; px++)
                {
                    int cx = px / ShapeCellPixels;

                    Color32 colour = ShapeCells[cy * grid + cx] ? filled : empty;

                    if (!ShapeCells[cy * grid + cx])
                    {
                        bool onLine = px % ShapeCellPixels == 0 || py % ShapeCellPixels == 0;

                        // 가운데 십자선은 조금 밝게 — 대칭을 맞출 때 기준이 됩니다.
                        bool onCentre = cx == grid / 2 || cy == grid / 2;

                        if (onLine)
                            colour = onCentre ? centre : line;
                    }

                    pixels[py * side + px] = colour;
                }
            }

            _shapeCanvasTexture.SetPixels32(pixels);
            _shapeCanvasTexture.Apply(false);

            _shapeCanvasImage.texture = _shapeCanvasTexture;
            _shapeCanvasImage.color = Color.white;
        }

        // ── 버튼 동작 ───────────────────────────────────────────────

        private void ClearDrawnShape()
        {
            Array.Clear(ShapeCells, 0, ShapeCells.Length);
            RedrawShapeCanvas();
        }

        private void RandomizeDrawnShape()
        {
            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            CustomShapes.Randomize(seed, CustomShapes.GridSize, ShapeCells);

            RedrawShapeCanvas();
            ShowHint(string.Format(L.Muzzle.ShapeRandomised, seed));
        }

        private void SaveDrawnShape()
        {
            string name = _shapeNameField != null ? _shapeNameField.text : "";

            if (!CustomShapes.Save(name, ShapeCells, CustomShapes.GridSize, out string reason))
            {
                ShowHint(reason switch
                {
                    "name" => L.Muzzle.ShapeNeedName,
                    "empty" => L.Muzzle.ShapeEmpty,
                    "full" => string.Format(L.Muzzle.ShapeFull, CustomShapes.MaxShapes),
                    _ => L.Muzzle.ShapeSaveFailed,
                });
                return;
            }

            // 저장한 도형을 지금 편집 중인 등급에 바로 물려 줍니다 — 저장만 하고
            // 목록에서 다시 찾아 고르게 하면 두 번 일하는 셈입니다.
            var profile = CurrentMuzzleProfile();
            if (profile != null)
            {
                profile.textureName = name.Trim();

                if (Settings.MuzzleFlashSettings.Mode == Settings.MuzzleFlashMode.TintDefault)
                    Settings.MuzzleFlashSettings.SetMode(Settings.MuzzleFlashMode.Replace);

                SyncMuzzleFromProfile();
            }

            ShowHint(string.Format(L.Muzzle.ShapeSaved, name.Trim()));
        }

        /// <summary>지금 등급이 쓰는 도형이 직접 그린 것이면 판으로 불러옵니다.</summary>
        private void LoadSelectedShape()
        {
            var profile = CurrentMuzzleProfile();
            var shape = profile != null ? CustomShapes.Find(profile.textureName) : null;

            if (shape == null || !shape.IsValid)
            {
                ShowHint(L.Muzzle.ShapeNotDrawn);
                return;
            }

            int grid = CustomShapes.GridSize;
            var cells = ShapeCells;

            for (int y = 0; y < grid; y++)
            {
                for (int x = 0; x < grid; x++)
                    cells[y * grid + x] = shape.Get(x, y);
            }

            _shapeNameField?.SetTextWithoutNotify(shape.name);

            RedrawShapeCanvas();
            ShowHint(string.Format(L.Muzzle.ShapeLoaded, shape.name));
        }

        private void DeleteSelectedShape()
        {
            string name = _shapeNameField != null ? _shapeNameField.text.Trim() : "";

            if (!CustomShapes.Delete(name))
            {
                ShowHint(L.Muzzle.ShapeNotDrawn);
                return;
            }

            // 지운 도형을 쓰고 있던 등급은 내장 도형으로 되돌립니다.
            foreach (var profile in MuzzleFlashProfiles.Runtime)
            {
                if (profile != null &&
                    string.Equals(profile.textureName, name, StringComparison.OrdinalIgnoreCase))
                {
                    profile.textureName = "";
                }
            }

            SyncMuzzleFromProfile();
            ShowHint(string.Format(L.Muzzle.ShapeDeleted, name));
        }

        private void DisposeShapeEditor()
        {
            if (_shapeCanvasTexture == null)
                return;

            Destroy(_shapeCanvasTexture);
            _shapeCanvasTexture = null;

            if (_shapeCanvasImage != null)
                _shapeCanvasImage.texture = null;
        }
    }
}
