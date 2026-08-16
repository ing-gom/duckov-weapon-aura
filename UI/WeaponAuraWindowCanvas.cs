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
    /// 인게임 스타일 오라 설정 창.
    ///
    /// IMGUI 패널(Ctrl+Shift+F8)은 개발·진단용으로 그대로 두고, 플레이어가 실제로 쓰는
    /// 색상 커스터마이즈는 이 uGUI 창이 담당합니다.
    ///
    /// 게임 UI처럼 보이게 만드는 방법:
    /// - 게임의 <see cref="UIPanel"/>을 상속해서 일시정지 메뉴의 자식 패널로 열립니다
    ///   (OpenChild가 public이라 게임의 패널 스택에 그대로 얹힙니다)
    /// - 게임 폰트(Jua)를 찾아 모든 텍스트에 적용합니다
    /// - 슬라이더 행은 게임 옵션 화면의 행(OptionsUIEntry_Slider)을 복제해서 씁니다
    ///   (복제가 안 되면 직접 만든 행으로 대체)
    /// </summary>
    public partial class WeaponAuraWindowCanvas : UIPanel
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        /// <summary>일시정지 메뉴(10000)보다 위에 그려야 합니다.</summary>
        private const int SortingOrder = 10500;

        private const string PreferredFontName = "jua";

        private const float PanelWidth = 1180f;

        /// <summary>
        /// 본문(좌우 두 열) 높이.
        /// 왼쪽 열 내용 합계보다 커야 합니다 — 작으면 아래 항목이 패널 배경 밖으로 밀려납니다.
        /// </summary>
        private const float BodyHeight = 840f;

        /// <summary>세로 스크롤바 두께 — 내용을 가리지 않게 얇게 둡니다.</summary>
        private const float ScrollbarWidth = 6f;

        /// <summary>티어 격자 한 칸 크기와 간격</summary>
        private const float TierCellWidth = 66f;
        private const float TierCellHeight = 42f;
        private const float TierCellSpacing = 6f;

        /// <summary>티어 격자에 한 번에 보여 줄 줄 수. 넘으면 격자 안에서 스크롤합니다.</summary>
        private const int TierVisibleRows = 2;

        private const int TierColumns = 5;

        /// <summary>등록 가능한 최대 등급 값 (게임의 제작·특수 등급이 999)</summary>
        private const int MaxTierGrade = 999;

        // ── 색상 (게임 일시정지/옵션 창 톤에 맞춘 어두운 청록 계열) ──────────
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color PanelColor = new Color(0.082f, 0.173f, 0.251f, 0.98f);
        private static readonly Color SectionColor = new Color(0.11f, 0.22f, 0.31f, 0.95f);
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color DimTextColor = new Color(0.72f, 0.80f, 0.86f, 1f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.30f, 0.42f, 0.95f);
        private static readonly Color ButtonAccentColor = new Color(0.20f, 0.45f, 0.62f, 0.95f);

        private static WeaponAuraWindowCanvas? _instance;

        public static WeaponAuraWindowCanvas Instance
        {
            get
            {
                if (_instance == null)
                {
                    var host = new GameObject("WeaponAuraWindow");
                    DontDestroyOnLoad(host);
                    _instance = host.AddComponent<WeaponAuraWindowCanvas>();
                }
                return _instance;
            }
        }

        private GameObject? _canvasRoot;
        private GameObject? _panel;
        private RawImage? _previewImage;
        private TextMeshProUGUI? _headerText;
        private TextMeshProUGUI? _statusText;
        private TextMeshProUGUI? _saveHintText;
        private TMP_FontAsset? _font;

        private readonly List<Button> _tierButtons = new List<Button>();

        private RectTransform? _tierGrid;
        private TMP_InputField? _newTierField;
        private Button? _removeTierButton;
        private readonly List<SliderRow> _rows = new List<SliderRow>();

        private ColorPickerControl? _pickerA;
        private ColorPickerControl? _pickerB;

        private Button? _toggleButton;
        private Button? _trailButton;

        private readonly List<KeyValuePair<WeaponAuraMode, Button>> _strengthButtons =
            new List<KeyValuePair<WeaponAuraMode, Button>>();

        private WeaponAuraPreviewStage? _preview;

        /// <summary>편집 중인 티어. -1이면 지금 든 무기의 티어를 따라갑니다.</summary>
        private int _editingTier = -1;

        private bool _followWeapon = true;
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        /// <summary>
        /// 창이 떠 있는지 — 아직 만들어지지 않았으면 만들지 않고 false를 돌려줍니다.
        /// (매 프레임 도는 입력 차단 패치에서 쓰기 때문에 Instance를 건드리면 안 됩니다)
        /// </summary>
        public static bool IsShown => _instance != null && _instance._isOpen;

        // ── 열기/닫기 ────────────────────────────────────────────────

        /// <summary>
        /// 창을 엽니다. 일시정지 메뉴가 있으면 그 자식 패널로 붙여서
        /// 게임의 패널 스택(ESC 처리·자식 닫기)에 그대로 얹힙니다.
        /// </summary>
        public void Show()
        {
            var menu = PauseMenuButton.FindPauseMenu();
            if (menu != null)
            {
                // UIPanel.Open은 internal이라 밖에서 못 부르지만 OpenChild는 public입니다.
                menu.OpenChild(this);
                return;
            }

            // 일시정지 메뉴를 못 찾으면 단독으로 띄웁니다.
            OnOpen();
        }

        public void Hide()
        {
            Close();
        }

        public void Toggle()
        {
            if (_isOpen)
                Hide();
            else
                Show();
        }

        protected override void OnOpen()
        {
            try
            {
                EnsureCanvas();
                BuildUI();
                SyncFromProfile();

                if (_canvasRoot != null)
                    _canvasRoot.SetActive(true);

                _isOpen = true;
                HookCancel(true);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WeaponAura] 설정 창 열기 실패: {ex}");
            }
        }

        protected override void OnClose()
        {
            _isOpen = false;
            HookCancel(false);

            if (_canvasRoot != null)
                _canvasRoot.SetActive(false);

            // 복제한 모델이 남아 있을 이유가 없습니다. 다음에 열 때 다시 세웁니다.
            _preview?.Dispose();
            _preview = null;
        }

        // ── ESC 처리 ────────────────────────────────────────────────

        private bool _cancelHooked;

        /// <summary>
        /// ESC를 우리 창이 먼저 가로챕니다.
        ///
        /// 게임은 취소 입력을 <c>OnCancelEarly</c> → <c>OnCancel</c> → <c>PauseMenu.Toggle()</c>
        /// 순으로 흘려보내고, 중간에 누가 <c>Use()</c>하면 뒤쪽은 실행하지 않습니다.
        /// 그래서 Early 단계에서 소비하면 일시정지 메뉴가 다시 뜨는 것까지 막을 수 있습니다.
        /// </summary>
        private void HookCancel(bool hook)
        {
            if (hook == _cancelHooked)
                return;

            if (hook)
                UIInputManager.OnCancelEarly += OnCancelEarly;
            else
                UIInputManager.OnCancelEarly -= OnCancelEarly;

            _cancelHooked = hook;
        }

        private void OnCancelEarly(UIInputEventData data)
        {
            if (!_isOpen || data == null || data.Used)
                return;

            data.Use();
            Hide();
        }

        private void OnDestroy()
        {
            HookCancel(false);

            _preview?.Dispose();
            _preview = null;

            if (_canvasRoot != null)
                Destroy(_canvasRoot);

            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!_isOpen)
                return;

            // 창이 떠 있는 동안은 커서를 확실히 풀어 둡니다.
            // (게임이 조준 모드로 커서를 다시 잠그는 경우가 있습니다)
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;

            if (_followWeapon)
            {
                int tier = WeaponAuraSystem.CurrentTier;
                if (tier >= 0 && tier != _editingTier)
                {
                    _editingTier = tier;
                    SyncFromProfile();
                }
            }

            // 편집 중인 티어의 프로필로 무대를 그립니다.
            // 게임 화면이 아니라 복제한 플레이어 모델 + 무기만 들어 있어서,
            // 티어를 바꾸면 그 색이 바로 보입니다.
            if (_previewImage != null)
            {
                _preview ??= new WeaponAuraPreviewStage();
                var texture = _preview.Render(CurrentProfile(), WeaponAuraSystem.CurrentIntensity());

                _previewImage.texture = texture;
                _previewImage.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);
            }

            RefreshStatus();
        }

        // ── 캔버스 ──────────────────────────────────────────────────

        private void EnsureCanvas()
        {
            if (_canvasRoot != null)
                return;

            var go = new GameObject("WeaponAuraCanvas");
            DontDestroyOnLoad(go);
            _canvasRoot = go;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // 게임에 EventSystem이 없을 일은 거의 없지만, 없으면 클릭이 안 먹습니다.
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("WeaponAuraEventSystem");
                DontDestroyOnLoad(es);
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        /// <summary>게임 한글 폰트(Jua)를 찾아 둡니다. 못 찾으면 TMP 기본 폰트를 씁니다.</summary>
        private void EnsureFont()
        {
            if (_font != null)
                return;

            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (asset == null)
                    continue;

                if (asset.name.IndexOf(PreferredFontName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _font = asset;
                    return;
                }
            }
        }

        /// <summary>창 안의 모든 텍스트에 게임 폰트를 입힙니다.</summary>
        private void ApplyFont(GameObject root)
        {
            if (_font == null)
                return;

            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text != null)
                    text.font = _font;
            }
        }
    }
}
