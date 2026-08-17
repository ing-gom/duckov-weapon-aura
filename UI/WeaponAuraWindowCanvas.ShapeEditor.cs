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
    /// 도형 그리기 판.
    ///
    /// 칸마다 버튼을 두면 24×24 = 576개짜리 UI가 되어 창이 무거워집니다. 대신 칸을 그려 넣은
    /// 텍스처 한 장을 띄우고, 그 위의 드래그 영역에서 포인터 위치를 칸 번호로 환산합니다.
    /// 오브젝트는 두 개면 충분하고, 칠하는 느낌도 이쪽이 더 좋습니다.
    ///
    /// 판은 <b>무기 오라 · 총구 화염 · 근접 참격</b> 세 탭에 각각 하나씩 놓입니다. 칸 상태와
    /// 텍스처는 셋이 나눠 씁니다 — 한 번 그린 도형을 탭을 옮겨 가며 쓸 수 있어야 하고,
    /// 같은 그림을 세 벌 들고 있을 이유도 없습니다. 저장·불러오기·삭제만 지금 보고 있는
    /// 탭의 프로필로 갈라집니다(탄환 잔상은 도형을 쓰지 않아 빠집니다).
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        /// <summary>칸 하나를 몇 픽셀로 그릴지 (격자선 포함)</summary>
        private const int ShapeCellPixels = 8;

        /// <summary>탭마다 하나씩. 텍스처는 공유하므로 그림은 셋이 항상 같습니다.</summary>
        private readonly List<RawImage> _shapeCanvasImages = new List<RawImage>();

        /// <summary>탭마다 하나씩. 한쪽에 친 이름이 나머지에도 그대로 보이게 묶어 둡니다.</summary>
        private readonly List<TMP_InputField> _shapeNameFields = new List<TMP_InputField>();

        private Texture2D? _shapeCanvasTexture;
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

            var canvasImage = canvasGo.gameObject.AddComponent<RawImage>();
            canvasImage.raycastTarget = true;
            _shapeCanvasImages.Add(canvasImage);

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

            var nameField = MakeInputField(side, L.Muzzle.ShapeName, 300f,
                TMP_InputField.CharacterValidation.None);
            nameField.characterLimit = 16;
            SetHeight((RectTransform)nameField.transform.parent, 34f);

            // 탭을 옮겨도 방금 친 이름이 그대로 보여야 합니다.
            nameField.onValueChanged.AddListener(value => MirrorShapeName(nameField, value));
            _shapeNameFields.Add(nameField);

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

        /// <summary>한 탭에서 고친 이름을 나머지 탭의 칸에도 그대로 옮깁니다.</summary>
        private void MirrorShapeName(TMP_InputField source, string value)
        {
            foreach (var field in _shapeNameFields)
            {
                if (field == null || field == source)
                    continue;

                if (!string.Equals(field.text, value, StringComparison.Ordinal))
                    field.SetTextWithoutNotify(value);
            }
        }

        /// <summary>지금 판에 적힌 도형 이름</summary>
        private string ShapeName()
        {
            foreach (var field in _shapeNameFields)
            {
                if (field != null && !string.IsNullOrWhiteSpace(field.text))
                    return field.text;
            }

            return "";
        }

        private void SetShapeName(string value)
        {
            foreach (var field in _shapeNameFields)
            {
                if (field != null)
                    field.SetTextWithoutNotify(value);
            }
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
            if (_shapeCanvasImages.Count == 0)
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

            foreach (var image in _shapeCanvasImages)
            {
                if (image == null)
                    continue;

                image.texture = _shapeCanvasTexture;
                image.color = Color.white;
            }
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

        // ── 지금 보고 있는 탭에 물려 주기 ──────────────────────────
        //
        // 판은 셋이 공유하지만 "이 도형을 쓴다"는 지금 보고 있는 탭의 프로필에만 걸립니다.
        // 안 보이는 탭의 등급이 조용히 바뀌면 되돌릴 방법이 없습니다.

        /// <summary>지금 탭에서 편집 중인 등급이 쓰는 도형 이름</summary>
        private string CurrentShapeTexture()
        {
            switch (_tab)
            {
                case WindowTab.Aura: return CurrentProfile()?.textureName ?? "";
                case WindowTab.Melee: return CurrentMeleeProfile()?.textureName ?? "";
                default: return CurrentMuzzleProfile()?.textureName ?? "";
            }
        }

        /// <summary>지금 탭에서 편집 중인 등급이 이 도형을 쓰게 합니다.</summary>
        private void UseShapeInCurrentTab(string name)
        {
            switch (_tab)
            {
                case WindowTab.Aura:
                {
                    var auraProfile = CurrentProfile();
                    if (auraProfile == null)
                        return;

                    auraProfile.textureName = name;
                    SyncFromProfile();

                    // 텍스처는 머티리얼이 바뀌는 것이라 값만 반영해서는 안 되고 다시 만들어야 합니다.
                    ApplyEdit(true);
                    return;
                }

                case WindowTab.Melee:
                {
                    var meleeProfile = CurrentMeleeProfile();
                    if (meleeProfile == null)
                        return;

                    // 근접 탭의 도형은 흩뿌림 알갱이가 씁니다(참격 호는 위쪽 `참격 모양`에서
                    // 따로 고릅니다 — 그 목록에도 방금 그린 도형이 함께 올라옵니다).
                    meleeProfile.textureName = name;

                    if (Settings.MeleeSlashSettings.Mode == Settings.MeleeSlashMode.TintDefault)
                        Settings.MeleeSlashSettings.SetMode(Settings.MeleeSlashMode.Overlay);

                    SyncMeleeFromProfile();
                    return;
                }

                default:
                {
                    var muzzleProfile = CurrentMuzzleProfile();
                    if (muzzleProfile == null)
                        return;

                    muzzleProfile.textureName = name;

                    if (Settings.MuzzleFlashSettings.Mode == Settings.MuzzleFlashMode.TintDefault)
                        Settings.MuzzleFlashSettings.SetMode(Settings.MuzzleFlashMode.Replace);

                    SyncMuzzleFromProfile();
                    return;
                }
            }
        }

        /// <summary>
        /// 지운 도형을 쓰고 있던 등급을 전부 기본값으로 되돌립니다.
        /// 세 기능을 모두 훑습니다 — 도형 목록은 하나라, 어느 탭에서 지웠든 다른 탭에
        /// 그 이름이 남아 있으면 그쪽은 없는 그림을 가리키게 됩니다.
        /// </summary>
        private static void ForgetShapeEverywhere(string name)
        {
            foreach (var profile in WeaponAuraProfiles.Runtime)
            {
                if (profile != null && SameShape(profile.textureName, name))
                    profile.textureName = "";
            }

            foreach (var profile in MuzzleFlashProfiles.Runtime)
            {
                if (profile != null && SameShape(profile.textureName, name))
                    profile.textureName = "";
            }

            foreach (var profile in MeleeSlashProfiles.Runtime)
            {
                if (profile == null)
                    continue;

                if (SameShape(profile.textureName, name))
                    profile.textureName = "";

                if (SameShape(profile.slashTexture, name))
                    profile.slashTexture = "";
            }
        }

        private static bool SameShape(string? value, string target)
        {
            return string.Equals(value, target, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshCurrentTabAfterShapeChange()
        {
            switch (_tab)
            {
                case WindowTab.Aura:
                    SyncFromProfile();
                    ApplyEdit(true);
                    break;

                case WindowTab.Melee:
                    SyncMeleeFromProfile();
                    break;

                default:
                    SyncMuzzleFromProfile();
                    break;
            }
        }

        private void SaveDrawnShape()
        {
            string name = ShapeName();

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
            UseShapeInCurrentTab(name.Trim());

            ShowHint(string.Format(L.Muzzle.ShapeSaved, name.Trim()));
        }

        /// <summary>지금 등급이 쓰는 도형이 직접 그린 것이면 판으로 불러옵니다.</summary>
        private void LoadSelectedShape()
        {
            var shape = CustomShapes.Find(CurrentShapeTexture());

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

            SetShapeName(shape.name);

            RedrawShapeCanvas();
            ShowHint(string.Format(L.Muzzle.ShapeLoaded, shape.name));
        }

        private void DeleteSelectedShape()
        {
            string name = ShapeName().Trim();

            if (!CustomShapes.Delete(name))
            {
                ShowHint(L.Muzzle.ShapeNotDrawn);
                return;
            }

            ForgetShapeEverywhere(name);
            RefreshCurrentTabAfterShapeChange();

            ShowHint(string.Format(L.Muzzle.ShapeDeleted, name));
        }

        private void DisposeShapeEditor()
        {
            if (_shapeCanvasTexture == null)
                return;

            Destroy(_shapeCanvasTexture);
            _shapeCanvasTexture = null;

            foreach (var image in _shapeCanvasImages)
            {
                if (image != null)
                    image.texture = null;
            }
        }
    }
}
