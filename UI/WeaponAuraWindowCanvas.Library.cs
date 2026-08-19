using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeaponAura.Helpers;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 무기 라이브러리 — 게임에 있는 무기를 <b>가지고 있지 않아도</b> 골라서 꾸밉니다.
    ///
    /// 목록은 <see cref="WeaponCatalog"/>가 들고 있고 아이콘은 아이템 메타데이터에 이미
    /// 들어 있어서 추가 로딩이 없습니다. 실측으로 134정 전부 아이콘이 있었습니다.
    ///
    /// 창 위에 덮는 오버레이로 만듭니다. 옆에 붙이면 1180px 패널이 화면을 넘고,
    /// 별도 창으로 띄우면 게임의 패널 스택을 한 겹 더 쌓아야 합니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        /// <summary>격자 한 칸</summary>
        private const float LibraryCellWidth = 190f;
        private const float LibraryCellHeight = 56f;
        private const float LibraryIconSize = 44f;

        private GameObject? _libraryRoot;
        private RectTransform? _libraryGrid;
        private TMP_InputField? _librarySearch;
        private TextMeshProUGUI? _libraryCount;
        private Button? _markedOnlyButton;

        private readonly List<KeyValuePair<string, Button>> _libraryFilterButtons =
            new List<KeyValuePair<string, Button>>();

        /// <summary>분류 필터. 빈 문자열이면 전체.</summary>
        private string _libraryFilter = "";

        /// <summary>전용 설정이 있는 무기만 보기.</summary>
        private bool _libraryMarkedOnly;

        private string _librarySearchText = "";

        /// <summary>목록을 만들 때 재사용할 버튼들. 다시 그릴 때 통째로 지웁니다.</summary>
        private readonly List<GameObject> _libraryCells = new List<GameObject>();

        // ── 열기 / 닫기 ─────────────────────────────────────────────

        private void OpenWeaponLibrary()
        {
            // 모델을 만들 수 있는지는 월드가 있어야 알 수 있습니다. 목록을 처음 열 때
            // 한 번 확인하고(실측 20ms), 그 결과로 "맨손" 같은 항목을 걸러 냅니다.
            if (CharacterMainControl.Main == null)
            {
                ShowHint(L.Target.NeedWorld);
                return;
            }

            try
            {
                WeaponCatalog.ValidateModels();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 무기 목록 확인 실패: {ex.Message}");
            }

            if (_libraryRoot == null)
                BuildLibrary();

            if (_libraryRoot == null)
                return;

            _libraryRoot.SetActive(true);
            RebuildLibraryGrid();
        }

        private void CloseWeaponLibrary()
        {
            if (_libraryRoot != null)
                _libraryRoot.SetActive(false);
        }

        // ── 구성 ────────────────────────────────────────────────────

        private void BuildLibrary()
        {
            if (_canvasRoot == null)
                return;

            var backdrop = MakeImage("LibraryBackdrop", _canvasRoot.transform, BackdropColor);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            _libraryRoot = backdrop.gameObject;

            var panel = MakeImage("LibraryPanel", backdrop.transform, PanelColor);
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(980f, 760f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildLibraryHeader(panel.transform);
            BuildLibraryFilters(panel.transform);
            BuildLibraryBody(panel.transform);

            ApplyFont(panel.gameObject);
        }

        private void BuildLibraryHeader(Transform parent)
        {
            var row = MakeRect("Header", parent);
            SetHeight(row, 42f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var title = MakeText("Title", row, L.Library.Title, 26, TextColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(title.rectTransform, 200f);

            _librarySearch = MakeInputField(row, L.Library.Search, 340f, TMP_InputField.CharacterValidation.None);
            _librarySearch.onValueChanged.AddListener(value =>
            {
                _librarySearchText = value ?? "";
                RebuildLibraryGrid();
            });

            _libraryCount = MakeText("Count", row, "", 18, DimTextColor, TextAlignmentOptions.MidlineRight);
            _libraryCount.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            MakeButton(row, L.Library.Close, 110f, CloseWeaponLibrary, ButtonColor);
        }

        /// <summary>분류 필터 줄. 전체 + 카탈로그에 실제로 있는 분류.</summary>
        private void BuildLibraryFilters(Transform parent)
        {
            var row = MakeRect("Filters", parent);
            SetHeight(row, 34f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            _libraryFilterButtons.Clear();

            var all = MakeButton(row, L.Library.All, 76f, () => SetLibraryFilter(""), ButtonAccentColor);
            _libraryFilterButtons.Add(new KeyValuePair<string, Button>("", all));

            // 전용 설정을 만들어 둔 무기를 다시 찾는 것이 목록에서 제일 어려웠습니다.
            // 색만으로는 134개 중에서 눈에 안 걸립니다.
            _markedOnlyButton = MakeButton(row, L.Library.MarkedOnly, 96f, () =>
            {
                _libraryMarkedOnly = !_libraryMarkedOnly;
                RebuildLibraryGrid();
            }, ButtonColor);

            foreach (string name in CollectClasses())
            {
                string display = name == WeaponHelper.MeleeClass ? L.Target.Melee : name;
                string captured = name;

                var button = MakeButton(row, display, 76f, () => SetLibraryFilter(captured), ButtonColor);
                _libraryFilterButtons.Add(new KeyValuePair<string, Button>(name, button));
            }
        }

        private void BuildLibraryBody(Transform parent)
        {
            var scrollGo = MakeRect("Scroll", parent);
            scrollGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 610f;

            var content = BuildScrollBody(scrollGo);

            // 세로로 쌓는 기본 레이아웃을 격자로 바꿉니다.
            //
            // 반드시 DestroyImmediate여야 합니다. Destroy는 프레임 끝까지 미뤄지기 때문에
            // 바로 아래에서 GridLayoutGroup을 붙이면 한 오브젝트에 레이아웃 그룹이 둘 붙은
            // 상태가 됩니다. 둘이 서로 자식 크기를 덮어써서 칸이 0으로 찌그러지고,
            // 목록이 통째로 비어 보입니다.
            var vertical = content.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                UnityEngine.Object.DestroyImmediate(vertical);

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.cellSize = new Vector2(LibraryCellWidth, LibraryCellHeight);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            _libraryGrid = content;
        }

        private void SetLibraryFilter(string name)
        {
            _libraryFilter = name;
            RebuildLibraryGrid();
        }

        // ── 목록 ────────────────────────────────────────────────────

        private void RebuildLibraryGrid()
        {
            if (_libraryGrid == null)
                return;

            // 여기도 즉시 지웁니다 — 미뤄 두면 이전 칸이 격자에 남은 채로 새 칸이 붙어서
            // 한 프레임 동안 목록이 두 배로 늘어나고 자리도 밀립니다.
            foreach (var cell in _libraryCells)
            {
                if (cell != null)
                    UnityEngine.Object.DestroyImmediate(cell);
            }

            _libraryCells.Clear();

            int heldTypeId = HeldWeaponTypeId();
            var entries = FilteredEntries();

            // 지금 든 무기를 맨 앞으로 올립니다 — 대부분의 경우 꾸미고 싶은 것이 그것입니다.
            entries.Sort((a, b) =>
            {
                bool aHeld = a.TypeId == heldTypeId;
                bool bHeld = b.TypeId == heldTypeId;
                if (aHeld != bHeld)
                    return aHeld ? -1 : 1;

                bool aMarked = WeaponOverrides.Has(WeaponOverrides.WeaponKey(a.TypeId));
                bool bMarked = WeaponOverrides.Has(WeaponOverrides.WeaponKey(b.TypeId));
                if (aMarked != bMarked)
                    return aMarked ? -1 : 1;

                return 0;   // 나머지는 카탈로그 순서(분류 → 등급 → 이름)를 유지합니다.
            });

            foreach (var entry in entries)
                _libraryCells.Add(BuildLibraryCell(entry, heldTypeId));

            if (_libraryCount != null)
            {
                _libraryCount.text = WeaponOverrides.Count > 0
                    ? $"{string.Format(L.Library.Count, entries.Count)}  ●{WeaponOverrides.Count}"
                    : string.Format(L.Library.Count, entries.Count);
            }

            if (entries.Count == 0)
            {
                var empty = MakeText("Empty", _libraryGrid, L.Library.Empty, 18, DimTextColor,
                    TextAlignmentOptions.MidlineLeft);
                _libraryCells.Add(empty.gameObject);
            }

            foreach (var pair in _libraryFilterButtons)
            {
                if (pair.Value?.targetGraphic != null)
                    pair.Value.targetGraphic.color = pair.Key == _libraryFilter ? ButtonAccentColor : ButtonColor;
            }

            if (_markedOnlyButton?.targetGraphic != null)
                _markedOnlyButton.targetGraphic.color = _libraryMarkedOnly ? MarkedColor : ButtonColor;

            // 이 탭에 붙지 않는 분류 버튼은 감춥니다. 눌러도 결과가 0인 버튼이 남아
            // 있으면 "왜 아무것도 안 나오지"가 됩니다.
            var allowedKind = AllowedWeaponKind();

            foreach (var pair in _libraryFilterButtons)
            {
                if (pair.Value == null || pair.Key.Length == 0)
                    continue;

                bool isMelee = pair.Key == WeaponHelper.MeleeClass;

                bool show = !allowedKind.HasValue
                            || (allowedKind.Value == WeaponKind.Melee ? isMelee : !isMelee);

                pair.Value.gameObject.SetActive(show);
            }

            ApplyFont(_libraryGrid.gameObject);
        }

        /// <summary>
        /// 이 탭에서 고를 수 있는 무기 종류. null이면 가리지 않습니다.
        ///
        /// 근접 참격은 근접무기에만, 탄환 잔상·총구 화염은 총기에만 붙습니다. 목록에
        /// 붙지도 않을 무기를 보여 주면 골라 놓고 "왜 아무 일도 없지"가 됩니다.
        /// 무기 오라는 둘 다에 붙으므로 가리지 않습니다.
        /// </summary>
        private WeaponKind? AllowedWeaponKind()
        {
            switch (_tab)
            {
                case WindowTab.Melee:
                    return WeaponKind.Melee;

                case WindowTab.Trail:
                case WindowTab.Muzzle:
                    return WeaponKind.Gun;

                default:
                    return null;
            }
        }

        private List<WeaponCatalogEntry> FilteredEntries()
        {
            var result = new List<WeaponCatalogEntry>();
            string search = _librarySearchText.Trim();
            var allowed = AllowedWeaponKind();

            foreach (var entry in WeaponCatalog.WithModel())
            {
                if (allowed.HasValue && entry.Kind != allowed.Value)
                    continue;

                if (_libraryMarkedOnly && !WeaponOverrides.Has(WeaponOverrides.WeaponKey(entry.TypeId)))
                    continue;

                if (_libraryFilter.Length > 0)
                {
                    bool match = _libraryFilter == WeaponHelper.MeleeClass
                        ? entry.Kind == WeaponKind.Melee
                        : entry.GunClass == _libraryFilter;

                    if (!match)
                        continue;
                }

                if (search.Length > 0
                    && entry.Name.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    continue;
                }

                result.Add(entry);
            }

            return result;
        }

        /// <summary>무기 한 칸 — 아이콘 + 이름 + 등급.</summary>
        private GameObject BuildLibraryCell(WeaponCatalogEntry entry, int heldTypeId)
        {
            bool marked = WeaponOverrides.Has(WeaponOverrides.WeaponKey(entry.TypeId));
            bool held = entry.TypeId == heldTypeId;

            // 전용 설정이 있는 무기는 <b>글자로도</b> 알 수 있어야 합니다.
            // 배경색만으로는 아이콘 격자에서 눈에 안 걸립니다.
            // 두 가지를 <b>서로 다른 축</b>으로 보여 줍니다.
            //
            // 배경색 하나로 둘을 다 표현하려니 충돌했습니다 — 전용 설정이 있는 무기를
            // 손에 들면 초록이 파랑을 덮어서 "지금 든 무기"라는 정보가 사라졌습니다.
            // 그래서 배경은 전용 설정 여부만 맡고, 지금 든 무기는 왼쪽 띠로 따로 표시합니다.
            Color background = marked ? MarkedColor : ButtonColor;

            var image = MakeImage($"Cell_{entry.TypeId}", _libraryGrid!, background);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            int captured = entry.TypeId;
            button.onClick.AddListener(() =>
            {
                SelectWeapon(captured);
                CloseWeaponLibrary();
            });

            var layout = image.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 4, 4);
            AddHeldStripe(image.transform, held);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            if (entry.Icon != null)
            {
                var icon = MakeImage("Icon", image.transform, Color.white);
                icon.sprite = entry.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var element = icon.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = LibraryIconSize;
                element.preferredHeight = LibraryIconSize;
                element.flexibleWidth = 0f;
            }

            // 전용 설정이 있는 무기는 굵게 + 앞에 표식을 붙입니다.
            // 배경색만으로는 아이콘 격자 134칸에서 눈에 안 걸립니다.
            string title = marked ? $"<b>● {entry.Name}</b>" : entry.Name;

            var text = MakeText("Name", image.transform,
                $"{title}\n<size=13>{DescribeEntry(entry, held, marked)}</size>",
                16, TextColor, TextAlignmentOptions.MidlineLeft);

            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            return image.gameObject;
        }

        /// <summary>지금 든 무기임을 알리는 왼쪽 띠. 아니면 자리만 비워 폭을 맞춥니다.</summary>
        private static void AddHeldStripe(Transform parent, bool held)
        {
            var stripe = MakeImage("HeldStripe", parent,
                held ? new Color(0.30f, 0.68f, 1f, 1f) : new Color(0f, 0f, 0f, 0f));

            stripe.raycastTarget = false;

            var element = stripe.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 4f;
            element.flexibleWidth = 0f;
        }

        private static string DescribeEntry(WeaponCatalogEntry entry, bool held, bool marked)
        {
            if (marked)
                return held ? $"{L.Library.Marked} · {L.Library.Held}" : L.Library.Marked;

            if (held)
                return L.Library.Held;

            string kind = entry.GunClass ?? L.Target.Melee;
            return $"{kind} · {entry.Quality}";
        }

        /// <summary>전용 설정이 있는 무기를 표시하는 색. 다른 파랑·회색과 확실히 갈립니다.</summary>
        private static readonly Color MarkedColor = new Color(0.16f, 0.52f, 0.36f, 0.98f);

        private static int HeldWeaponTypeId()
        {
            try
            {
                var player = CharacterMainControl.Main;
                var holder = player != null ? player.agentHolder : null;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;
                var item = agent != null ? agent.Item : null;

                return item != null ? item.TypeID : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
