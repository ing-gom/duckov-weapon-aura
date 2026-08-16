using System;
using UnityEngine;
using WeaponAura.Settings;
using WeaponAura.Systems;

namespace WeaponAura.UI
{
    /// <summary>
    /// 무기 오라 실시간 튜닝용 디버그 패널.
    ///
    /// Release 빌드에도 포함되지만 <b>Ctrl+Shift+F8</b> 조합에서만 열려서 일반 플레이 중에는 눌릴 일이 없습니다.
    /// (DEBUG 빌드에서는 F8 단독으로도 열립니다.)
    ///
    /// - 닫혀 있으면 OnGUI가 즉시 반환하므로 평소 비용은 0입니다.
    /// - 티어별 파라미터를 슬라이더로 바꾸면 게임을 끄지 않고 바로 반영됩니다.
    /// - [저장]으로 JSON에 기록하고, [코드 스니펫]으로 WeaponAuraProfiles.CreateDefaults()에
    ///   그대로 붙여넣을 수 있는 C# 코드를 로그로 뽑습니다.
    /// </summary>
    public class WeaponAuraDebugWindow
    {
        private const int WindowId = 12346;
        private const float WindowWidth = 430f;
        private const float WindowHeight = 620f;

        private static WeaponAuraDebugWindow? _instance;
        public static WeaponAuraDebugWindow Instance => _instance ??= new WeaponAuraDebugWindow();

        private bool _isOpen;
        private Rect _windowRect = new Rect(40f, 40f, WindowWidth, WindowHeight);
        private Vector2 _scroll;

        private int _editingTier;
        private bool _useLevelOverride;
        private float _levelOverride = 4f;
        private int _tierOverrideIndex;      // 0=자동, 1=없음, 2..=티어0..
        private int _randomSeed = 1234;
        private bool _showRarityTable = true;
        private string _lastStatus = "";

        private GUIStyle? _headerStyle;
        private GUIStyle? _labelStyle;
        private GUIStyle? _statusStyle;
        private Texture2D? _backgroundTexture;

        public bool IsOpen => _isOpen;

        public void Toggle()
        {
            _isOpen = !_isOpen;
            if (_isOpen)
                SyncEditingTierToCurrent();
        }

        public void Close() => _isOpen = false;

        /// <summary>모드 종료 시 미리보기 카메라·텍스처를 정리합니다.</summary>
        public void Dispose()
        {
            _preview.Dispose();
        }

        public void OnGUI()
        {
            if (!_isOpen)
                return;

            try
            {
                EnsureStyles();

                // 다른 GunMaster 창들보다 위에 그립니다.
                int previousDepth = GUI.depth;
                GUI.depth = -200;
                _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "무기 오라 · 튜닝 패널 (F8)",
                    GUILayout.Width(WindowWidth), GUILayout.Height(WindowHeight));
                GUI.depth = previousDepth;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] WeaponAuraDebugWindow.OnGUI 오류: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────

        private void DrawWindow(int id)
        {
            DrawStatusBlock();
            GUILayout.Space(4f);
            DrawOverrideBlock();
            GUILayout.Space(4f);

            var profile = WeaponAuraProfiles.Get(_editingTier);
            if (profile == null)
            {
                GUILayout.Label("편집할 티어가 없습니다.", _labelStyle);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
                return;
            }

            DrawPreview();
            GUILayout.Space(4f);
            DrawSaveRow();
            GUILayout.Space(4f);
            DrawRarityTable();
            GUILayout.Space(4f);
            DrawTierSelector();

            // 변경 감지용 스냅샷
            var before = profile.Clone();

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            DrawProfileEditor(profile);
            GUILayout.EndScrollView();

            ApplyIfChanged(before, profile);

            GUILayout.Space(4f);
            DrawButtons();

            if (!string.IsNullOrEmpty(_lastStatus))
                GUILayout.Label(_lastStatus, _statusStyle);

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawStatusBlock()
        {
            string tierName = "-";
            var currentProfile = WeaponAuraProfiles.Get(WeaponAuraSystem.CurrentTier);
            if (currentProfile != null)
            {
                // 실제로 적용 중인 티어의 색까지 같이 보여줍니다.
                // "티어 번호는 맞는데 색이 다르다" 같은 혼동을 바로 가려낼 수 있습니다.
                string rarityName = WeaponAuraSystem.CurrentTier < RarityNames.Length
                    ? RarityNames[WeaponAuraSystem.CurrentTier]
                    : currentProfile.name;
                tierName = $"{WeaponAuraSystem.CurrentTier} {rarityName} " +
                           $"#{ColorUtility.ToHtmlStringRGB(currentProfile.colorA)}";
            }
            else if (WeaponAuraSystem.CurrentTier == -1)
            {
                tierName = "없음";
            }

            GUILayout.Label($"무기: {WeaponAuraSystem.CurrentWeaponName}", _headerStyle);
            GUILayout.Label(
                $"등급 {WeaponAuraSystem.CurrentLevel} (표시 {WeaponAuraSystem.CurrentDisplayQuality} / 메타 {WeaponAuraSystem.CurrentMetaQuality})   티어 {tierName}   " +
                $"파티클 {(WeaponAuraSystem.HasAura ? "ON" : "OFF")}",
                _labelStyle);

            var size = WeaponAuraSystem.WeaponBoundsSize;
            GUILayout.Label(
                $"방출: {WeaponAuraSystem.ShapeReason}   크기 {size.x:0.00}×{size.y:0.00}×{size.z:0.00}m   " +
                $"모델 스케일 {WeaponAuraSystem.HostScale.x:0.##}",
                _labelStyle);
            GUILayout.Label(
                $"셰이더: 면={WeaponAuraResources.ResolvedSheetShaderName} / 입자={WeaponAuraResources.ResolvedShaderName}   " +
                $"면: {WeaponAuraSystem.SheetMode}",
                _statusStyle);
            DrawOverrideWarning();
            GUILayout.Label("※ 이 창이 열려 있는 동안 발사 입력은 차단됩니다", _statusStyle);

            DrawModeRow();
        }

        /// <summary>
        /// 무기 등급을 무시하게 만드는 설정이 켜져 있으면 눈에 띄게 알립니다.
        /// (티어 고정 2로 두고 "왜 이 무기만 파랗지"를 한참 찾은 적이 있습니다)
        /// </summary>
        private void DrawOverrideWarning()
        {
            var warnings = new System.Collections.Generic.List<string>();

            if (AuraSettings.TierSource == AuraTierSource.Fixed)
                warnings.Add($"티어 고정({AuraSettings.FixedTier})");
            else if (AuraSettings.TierSource == AuraTierSource.WeaponClass)
                warnings.Add("티어 기준=무기 종류");

            if (WeaponAuraSystem.DebugLevelOverride >= 0)
                warnings.Add($"등급 강제({WeaponAuraSystem.DebugLevelOverride})");

            if (WeaponAuraSystem.DebugTierOverride >= -1)
                warnings.Add($"티어 강제({WeaponAuraSystem.DebugTierOverride})");

            if (warnings.Count == 0)
                return;

            var previous = _statusStyle!.normal.textColor;
            _statusStyle.normal.textColor = new Color(1f, 0.75f, 0.3f);
            GUILayout.Label($"⚠ {string.Join(" · ", warnings.ToArray())} — 무기 등급이 무시됩니다", _statusStyle);
            _statusStyle.normal.textColor = previous;

            if (GUILayout.Button("등급 기준으로 되돌리기"))
            {
                AuraSettings.SetTierSource(AuraTierSource.Quality);
                WeaponAuraSystem.DebugLevelOverride = -1;
                WeaponAuraSystem.DebugTierOverride = -2;
                _useLevelOverride = false;
                _tierOverrideIndex = 0;
                WeaponAuraSystem.RebuildNow();
                _lastStatus = "아이템 등급 기준으로 되돌렸습니다.";
            }
        }

        /// <summary>
        /// 모드 설정 — 표시 세기와 티어 소스. 게임 설정 화면 대신 여기서 바꿉니다.
        /// (여기서 바꾼 값은 OptionsManager에 저장되어 다음 실행에도 유지됩니다.)
        /// </summary>
        private void DrawModeRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("표시 세기", _labelStyle, GUILayout.Width(60f));
            int mode = GUILayout.Toolbar((int)AuraSettings.AuraMode, new[] { "끄기", "약", "보통", "강" });
            GUILayout.EndHorizontal();

            if (mode != (int)AuraSettings.AuraMode)
                AuraSettings.SetMode((WeaponAuraMode)mode);

            GUILayout.BeginHorizontal();
            GUILayout.Label("티어 기준", _labelStyle, GUILayout.Width(60f));
            int source = GUILayout.Toolbar((int)AuraSettings.TierSource, new[] { "아이템 등급", "무기 종류", "고정" });
            GUILayout.EndHorizontal();

            if (source != (int)AuraSettings.TierSource)
                AuraSettings.SetTierSource((AuraTierSource)source);

            if (AuraSettings.TierSource == AuraTierSource.Fixed)
            {
                int maxTier = Mathf.Max(0, WeaponAuraProfiles.TierCount - 1);
                int fixedTier = Mathf.RoundToInt(Slider("고정 티어", AuraSettings.FixedTier, 0f, maxTier, "0"));
                if (fixedTier != AuraSettings.FixedTier)
                    AuraSettings.SetFixedTier(fixedTier);
            }
        }

        private void DrawOverrideBlock()
        {
            bool ignoreSetting = GUILayout.Toggle(WeaponAuraSystem.DebugIgnoreSetting, " 설정 무시하고 강제 표시");
            if (ignoreSetting != WeaponAuraSystem.DebugIgnoreSetting)
            {
                WeaponAuraSystem.DebugIgnoreSetting = ignoreSetting;
                WeaponAuraSystem.Reevaluate();
            }

            GUILayout.BeginHorizontal();
            bool useOverride = GUILayout.Toggle(_useLevelOverride, " 점수 강제", GUILayout.Width(90f));
            _levelOverride = GUILayout.HorizontalSlider(_levelOverride, 0f, 12f, GUILayout.ExpandWidth(true));
            GUILayout.Label(Mathf.RoundToInt(_levelOverride).ToString(), _labelStyle, GUILayout.Width(36f));
            GUILayout.EndHorizontal();

            int newOverrideLevel = useOverride ? Mathf.RoundToInt(_levelOverride) : -1;
            if (useOverride != _useLevelOverride || newOverrideLevel != WeaponAuraSystem.DebugLevelOverride)
            {
                _useLevelOverride = useOverride;
                WeaponAuraSystem.DebugLevelOverride = newOverrideLevel;
                // 티어가 실제로 바뀔 때만 재생성 (슬라이더를 끄는 동안 깜빡임 방지)
                WeaponAuraSystem.Reevaluate();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("티어 강제", _labelStyle, GUILayout.Width(60f));
            string[] options = BuildTierOverrideOptions();
            int selected = GUILayout.Toolbar(_tierOverrideIndex, options);
            GUILayout.EndHorizontal();

            if (selected != _tierOverrideIndex)
            {
                _tierOverrideIndex = selected;
                WeaponAuraSystem.DebugTierOverride = selected == 0 ? -2 : selected - 2;
                if (selected >= 2)
                    _editingTier = selected - 2;
                WeaponAuraSystem.Reevaluate();
            }
        }

        private readonly WeaponAuraPreview _preview = new WeaponAuraPreview();
        private bool _showPreview = true;

        /// <summary>사용자 프리셋 슬롯 이름 (파일명에 그대로 쓰입니다)</summary>
        private static readonly string[] PresetSlots = { "1", "2", "3" };

        /// <summary>
        /// 저장 / 불러오기 / 프리셋 슬롯.
        ///
        /// 기본 저장(weapon_aura_tuning.json)은 모드 시작 시 자동으로 불러오므로,
        /// [저장]만 눌러 두면 게임을 껐다 켜도 색이 유지됩니다.
        /// 슬롯은 여러 조합을 넣어 두고 골라 쓰는 용도입니다.
        /// </summary>
        private void DrawSaveRow()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("저장", GUILayout.Height(24f)))
            {
                _lastStatus = WeaponAuraProfiles.Save(out string savePath)
                    ? "저장했습니다 — 다음 실행에도 유지됩니다."
                    : $"저장 실패: {savePath}";
            }

            if (GUILayout.Button("되돌리기", GUILayout.Height(24f)))
            {
                _lastStatus = WeaponAuraProfiles.Load(out _)
                    ? "저장된 설정으로 되돌렸습니다."
                    : "저장된 파일이 없습니다.";
                WeaponAuraSystem.RebuildNow();
            }

            if (GUILayout.Button("기본값", GUILayout.Height(24f)))
            {
                WeaponAuraProfiles.ResetToDefaults();
                WeaponAuraSystem.RebuildNow();
                _lastStatus = "기본값으로 되돌렸습니다. (저장하려면 [저장])";
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("프리셋", _labelStyle, GUILayout.Width(44f));

            foreach (string slot in PresetSlots)
            {
                bool exists = WeaponAuraProfiles.SlotExists(slot);

                if (GUILayout.Button(exists ? $"▶ {slot}" : $"· {slot}", GUILayout.Width(46f)))
                {
                    if (WeaponAuraProfiles.Load(out _, slot))
                    {
                        WeaponAuraSystem.RebuildNow();
                        _lastStatus = $"프리셋 {slot} 불러옴";
                    }
                    else
                    {
                        _lastStatus = $"프리셋 {slot}이 비어 있습니다. [저장 {slot}]으로 만드세요.";
                    }
                }

                if (GUILayout.Button($"저장 {slot}", GUILayout.Width(56f)))
                {
                    _lastStatus = WeaponAuraProfiles.Save(out _, slot)
                        ? $"프리셋 {slot}에 저장했습니다."
                        : $"프리셋 {slot} 저장 실패";
                }
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 지금 든 무기를 3D로 보여줍니다. 드래그로 돌리고 휠로 확대할 수 있습니다.
        /// 복제본이 아니라 실물을 전용 카메라로 찍는 것이라, 여기 보이는 게 곧 인게임 모습입니다.
        /// </summary>
        private void DrawPreview()
        {
            GUILayout.BeginHorizontal();
            _showPreview = GUILayout.Toggle(_showPreview, " 3D 미리보기");
            if (_showPreview)
                _preview.AutoRotate = GUILayout.Toggle(_preview.AutoRotate, " 자동 회전");
            GUILayout.EndHorizontal();

            if (!_showPreview)
                return;

            // 카메라 렌더는 Repaint에서만 (레이아웃 단계에서 하면 GUI 상태가 어긋납니다)
            var rect = GUILayoutUtility.GetRect(WindowWidth - 32f, 180f);

            if (Event.current.type == EventType.Repaint)
            {
                var texture = _preview.Render();
                if (texture != null)
                    GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
                else
                    GUI.Label(rect, "  무기를 들면 여기에 표시됩니다", _statusStyle);
            }

            HandlePreviewInput(rect);
        }

        /// <summary>미리보기 영역에서의 드래그·휠 입력</summary>
        private void HandlePreviewInput(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition))
                return;

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _preview.AutoRotate = false;
                _preview.Rotate(-e.delta.x * 0.5f, e.delta.y * 0.4f);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _preview.Zoom(-e.delta.y * 0.05f);
                e.Use();
            }
        }

        /// <summary>희귀도 한글 이름 (프로필 순서 = 희귀도 1~7)</summary>
        private static readonly string[] RarityNames =
        {
            "낡음", "일반", "고급", "희귀", "영웅", "전설", "신화", "초월", "특수", "커스텀",
        };

        /// <summary>
        /// 희귀도 1~7이 각각 어떤 오라로 나오는지 표로 보여줍니다.
        /// 현재 들고 있는 무기의 희귀도 행에 ▶ 표시가 붙습니다.
        /// </summary>
        private void DrawRarityTable()
        {
            _showRarityTable = GUILayout.Toggle(_showRarityTable, " 희귀도 → 오라 표");
            if (!_showRarityTable)
                return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("", _labelStyle, GUILayout.Width(14f));
            GUILayout.Label("희귀도", _headerStyle, GUILayout.Width(64f));
            GUILayout.Label("세기", _headerStyle, GUILayout.Width(42f));
            GUILayout.Label("겹", _headerStyle, GUILayout.Width(26f));
            GUILayout.Label("동심원", _headerStyle, GUILayout.Width(44f));
            GUILayout.Label("링", _headerStyle, GUILayout.Width(26f));
            GUILayout.Label("색", _headerStyle);
            GUILayout.EndHorizontal();

            int currentTier = WeaponAuraSystem.CurrentTier;

            for (int i = 0; i < WeaponAuraProfiles.TierCount; i++)
            {
                var p = WeaponAuraProfiles.Get(i);
                if (p == null)
                    continue;

                bool isCurrent = i == currentTier;
                bool isEditing = i == _editingTier;

                GUILayout.BeginHorizontal();

                GUILayout.Label(isCurrent ? "▶" : " ", _statusStyle, GUILayout.Width(14f));

                string label = i < RarityNames.Length ? $"{p.minLevel} {RarityNames[i]}" : $"{p.minLevel} {p.name}";
                var nameStyle = isEditing ? _headerStyle : _labelStyle;
                if (GUILayout.Button(label, nameStyle, GUILayout.Width(64f)))
                {
                    _editingTier = i;
                }

                GUILayout.Label((p.alpha * p.colorIntensity).ToString("0.00"), _labelStyle, GUILayout.Width(42f));
                GUILayout.Label(p.sheetLayers.ToString(), _labelStyle, GUILayout.Width(26f));
                GUILayout.Label(p.sheetRings.ToString("0"), _labelStyle, GUILayout.Width(44f));
                GUILayout.Label(p.ringEnabled ? "●" : "–", _labelStyle, GUILayout.Width(26f));

                // 색 A→B 미리보기
                var swatch = GUILayoutUtility.GetRect(46f, 12f, GUILayout.Width(46f));
                DrawSwatch(new Rect(swatch.x, swatch.y + 2f, 22f, 10f), p.colorA);
                DrawSwatch(new Rect(swatch.x + 23f, swatch.y + 2f, 22f, 10f), p.colorB);

                GUILayout.EndHorizontal();
            }

            GUILayout.Label(
                "세기 = 불투명도 × 발광 배율. 게임에서는 무기의 아이템 등급이 그대로 희귀도가 됩니다.",
                _statusStyle);
        }

        private void DrawTierSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("편집 티어", _labelStyle, GUILayout.Width(60f));

            int tierCount = WeaponAuraProfiles.TierCount;
            var names = new string[tierCount];
            for (int i = 0; i < tierCount; i++)
            {
                var p = WeaponAuraProfiles.Get(i);
                // 7단계라 폭이 좁습니다 — 희귀도 번호만 표시
                names[i] = p != null ? p.minLevel.ToString() : i.ToString();
            }

            int selected = GUILayout.Toolbar(Mathf.Clamp(_editingTier, 0, tierCount - 1), names);
            GUILayout.EndHorizontal();

            _editingTier = selected;
        }

        private void DrawProfileEditor(WeaponAuraProfile p)
        {
            Section("적용 조건");
            p.minLevel = Mathf.RoundToInt(Slider("최소 등급", p.minLevel, 0f, 12f, "0"));

            Section("색상");
            p.colorA = ColorSliders("색 A", p.colorA);
            p.colorB = ColorSliders("색 B", p.colorB);
            p.alpha = Slider("불투명도", p.alpha, 0f, 1f);

            Section("무기 표면 파티클");
            p.emissionRate = Slider("방출량/초", p.emissionRate, 0f, 100f, "0.#");
            p.startSize = Slider("입자 크기", p.startSize, 0.005f, 0.2f, "0.###");
            p.lifetime = Slider("수명(초)", p.lifetime, 0.1f, 3f);
            p.speed = Slider("확산 속도", p.speed, 0f, 1f);
            p.drag = Slider("감속(번짐 유지)", p.drag, 0f, 1f);
            p.gravity = Slider("중력(음수=상승)", p.gravity, -0.3f, 0.3f, "0.###");

            Section("오로라 노이즈");
            p.noiseStrength = Slider("세기", p.noiseStrength, 0f, 1.5f);
            p.noiseFrequency = Slider("주파수", p.noiseFrequency, 0.05f, 2f);
            p.noiseScroll = Slider("스크롤", p.noiseScroll, 0f, 2f);

            Section("텍스처 / 렌더");
            DrawTexturePicker(p);
            p.tilesX = Mathf.RoundToInt(Slider("플립북 가로", p.tilesX, 1f, 8f, "0"));
            p.tilesY = Mathf.RoundToInt(Slider("플립북 세로", p.tilesY, 1f, 8f, "0"));
            if (p.tilesX > 1 || p.tilesY > 1)
                p.flipbookFps = Slider("플립북 FPS(0=수명)", p.flipbookFps, 0f, 60f, "0");
            p.colorIntensity = Slider("발광 배율", p.colorIntensity, 0.2f, 4f);
            p.rotationSpeed = Slider("회전(도/초)", p.rotationSpeed, -360f, 360f, "0");
            p.startRotationRandom = Slider("초기 회전 랜덤", p.startRotationRandom, 0f, 180f, "0");

            GUILayout.BeginHorizontal();
            GUILayout.Label("그리기 방식", _labelStyle, GUILayout.Width(110f));
            int styleIndex = GUILayout.Toolbar((int)p.renderStyle, new[] { "빌보드", "늘림", "리본", "면" });
            GUILayout.EndHorizontal();
            p.renderStyle = (WeaponAuraRenderStyle)Mathf.Clamp(styleIndex, 0, 3);

            if (p.renderStyle == WeaponAuraRenderStyle.Sheet)
            {
                p.sheetLayers = Mathf.RoundToInt(Slider("면 겹 수", p.sheetLayers, 1f, 8f, "0"));
                p.sheetSpread = Slider("뻗는 거리(m)", p.sheetSpread, 0.01f, 0.6f, "0.###");
                p.sheetPeriod = Slider("퍼짐 주기(초)", p.sheetPeriod, 0.3f, 6f);
                p.sheetRoundness = Slider("둥글기", p.sheetRoundness, 0f, 1f);
                p.sheetRings = Slider("동심원 개수", p.sheetRings, 0f, 12f, "0.#");
                if (p.sheetRings > 0.001f)
                    p.sheetRingSpeed = Slider("동심원 속도", p.sheetRingSpeed, -3f, 3f);
                p.sheetWobble = Slider(p.sheetRings > 0.001f ? "파동 깊이" : "면 일렁임", p.sheetWobble, 0f, 1f);
                p.sheetWobbleSpeed = Slider("일렁임 속도", p.sheetWobbleSpeed, 0f, 6f);
                p.sheetUseWeaponMesh = GUILayout.Toggle(p.sheetUseWeaponMesh, " 무기 메시를 면 원본으로 사용");
                if (!WeaponAuraSystem.SheetUsesWeaponMesh)
                {
                    p.sheetBoxiness = Slider("폴백 각짐", p.sheetBoxiness, 0f, 1f);
                    GUILayout.Label("→ 무기 메시를 못 읽어 박스형 셸 사용 중", _statusStyle);
                }
            }

            if (p.renderStyle == WeaponAuraRenderStyle.Stretched)
                p.stretchLength = Slider("늘림 길이", p.stretchLength, 0.5f, 8f);
            if (p.renderStyle == WeaponAuraRenderStyle.Ribbon)
            {
                p.ribbonCount = Mathf.RoundToInt(Slider("띠 가닥 수", p.ribbonCount, 1f, 12f, "0"));
                p.trailWidth = Slider("띠 폭", p.trailWidth, 0.05f, 2f);
                p.ribbonShowHeads = GUILayout.Toggle(p.ribbonShowHeads, " 입자 알갱이도 같이 표시");
            }

            Section("수명 곡선");
            p.fadeIn = Slider("페이드 인", p.fadeIn, 0f, 0.9f);
            p.fadeOut = Slider("페이드 아웃", p.fadeOut, 0f, 0.9f);
            p.sizeStart = Slider("크기 시작", p.sizeStart, 0f, 2f);
            p.sizePeak = Slider("크기 중간", p.sizePeak, 0f, 2f);
            p.sizeEnd = Slider("크기 끝", p.sizeEnd, 0f, 2f);

            Section("트레일");
            p.trailEnabled = GUILayout.Toggle(p.trailEnabled, " 꼬리 사용");
            if (p.trailEnabled)
            {
                p.trailRatio = Slider("적용 비율", p.trailRatio, 0f, 1f);
                p.trailWidth = Slider("꼬리 폭", p.trailWidth, 0.05f, 2f);
                p.trailLifetime = Slider("꼬리 수명", p.trailLifetime, 0.05f, 1f);
            }

            Section("Shape (구조 · 변경 시 재생성)");
            p.useMeshShape = GUILayout.Toggle(p.useMeshShape, " 무기 메시 표면에서 방출 (불가 시 박스로 폴백)");
            p.shapeScale = Slider("방출 영역 배율", p.shapeScale, 0.2f, 3f);
            if (WeaponAuraSystem.MeshShapeInUse)
                p.normalOffset = Slider("표면 띄우기", p.normalOffset, 0f, 0.1f, "0.###");
            else
                GUILayout.Label("→ 무기 바운딩 박스 껍데기에서 방출 중 (중심에서 바깥으로 확산)", _statusStyle);

            Section("링 (구조 · 사용/개수는 재생성)");
            p.ringEnabled = GUILayout.Toggle(p.ringEnabled, " 회전 링 사용");
            if (p.ringEnabled)
            {
                p.ringCount = Mathf.RoundToInt(Slider("링 개수", p.ringCount, 1f, 16f, "0"));
                p.ringRadius = Slider("링 반지름", p.ringRadius, 0.05f, 1f);
                p.ringSize = Slider("링 입자 크기", p.ringSize, 0.01f, 0.2f, "0.###");
                p.ringSpeed = Slider("링 회전(도/초)", p.ringSpeed, -360f, 360f, "0");
                p.ringTilt = Slider("링 기울기", p.ringTilt, -90f, 90f, "0");
                p.ringBob = Slider("상하 흔들림", p.ringBob, 0f, 0.2f, "0.###");
                p.ringBobSpeed = Slider("흔들림 속도", p.ringBobSpeed, 0f, 8f);
            }
        }

        /// <summary>
        /// assets/vfx_textures 안의 이미지를 좌우 버튼으로 넘겨가며 고릅니다.
        /// (첫 항목은 코드로 만든 내장 글로우)
        /// </summary>
        private void DrawTexturePicker(WeaponAuraProfile p)
        {
            string[] names = WeaponAuraResources.GetTextureNames();
            int index = Array.IndexOf(names, p.textureName ?? "");
            if (index < 0)
                index = 0;

            GUILayout.BeginHorizontal();
            GUILayout.Label("텍스처", _labelStyle, GUILayout.Width(110f));

            if (GUILayout.Button("◀", GUILayout.Width(26f)))
                index = (index - 1 + names.Length) % names.Length;

            string display = string.IsNullOrEmpty(names[index]) ? "(내장 글로우)" : names[index];
            GUILayout.Label(display, _labelStyle, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("▶", GUILayout.Width(26f)))
                index = (index + 1) % names.Length;

            if (GUILayout.Button("↻", GUILayout.Width(26f)))
            {
                WeaponAuraResources.GetTextureNames(refresh: true);
                _lastStatus = $"텍스처 폴더 재스캔: {WeaponAuraResources.GetTextureFolder() ?? "(경로 없음)"}";
                WeaponAuraSystem.RebuildNow();
            }
            GUILayout.EndHorizontal();

            p.textureName = names[Mathf.Clamp(index, 0, names.Length - 1)];

            if (names.Length <= 1)
            {
                GUILayout.Label(
                    $"PNG를 넣으면 목록에 뜹니다: assets/{WeaponAuraResources.TextureFolder}/",
                    _statusStyle);
            }
        }

        /// <summary>속성 프리셋 — 형태·색·움직임을 한 번에 바꿉니다.</summary>
        private void DrawPresetButtons()
        {
            GUILayout.Label("프리셋 (현재 편집 티어에 적용)", _statusStyle);

            var kinds = new[]
            {
                (WeaponAuraPresetKind.Aurora, "오로라"),
                (WeaponAuraPresetKind.Fire, "화염"),
                (WeaponAuraPresetKind.Frost, "냉기"),
                (WeaponAuraPresetKind.Toxic, "독"),
                (WeaponAuraPresetKind.Void, "공허"),
                (WeaponAuraPresetKind.Shock, "전격"),
                (WeaponAuraPresetKind.Holy, "신성"),
                (WeaponAuraPresetKind.Blood, "혈기"),
                (WeaponAuraPresetKind.Arcane, "비전"),
                (WeaponAuraPresetKind.Plasma, "플라즈마"),
                (WeaponAuraPresetKind.Nature, "자연"),
                (WeaponAuraPresetKind.Shadow, "그림자"),
            };

            const int columns = 4;
            for (int row = 0; row * columns < kinds.Length; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= kinds.Length)
                    {
                        GUILayout.Label("", _labelStyle);
                        continue;
                    }

                    var (kind, label) = kinds[index];
                    if (GUILayout.Button(label))
                        ApplyPreset(kind, label);
                }
                GUILayout.EndHorizontal();
            }

            DrawRandomRow();
        }

        /// <summary>시드 기반 랜덤 — 같은 시드는 항상 같은 결과입니다.</summary>
        private void DrawRandomRow()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("랜덤 생성", GUILayout.Width(90f)))
            {
                _randomSeed = UnityEngine.Random.Range(1, 999999);
                ApplyRandom();
            }

            if (GUILayout.Button("◀", GUILayout.Width(26f)))
            {
                _randomSeed = Mathf.Max(1, _randomSeed - 1);
                ApplyRandom();
            }

            GUILayout.Label($"시드 {_randomSeed}", _labelStyle, GUILayout.Width(92f));

            if (GUILayout.Button("▶", GUILayout.Width(26f)))
            {
                _randomSeed++;
                ApplyRandom();
            }

            if (GUILayout.Button("다시 적용"))
                ApplyRandom();

            GUILayout.EndHorizontal();
        }

        private void ApplyRandom()
        {
            var target = WeaponAuraProfiles.Get(_editingTier);
            if (target == null)
                return;

            var generated = WeaponAuraProfiles.CreateRandom(_randomSeed, target.name, target.minLevel);
            generated.textureName = target.textureName;
            generated.tilesX = target.tilesX;
            generated.tilesY = target.tilesY;

            target.CopyFrom(generated);
            WeaponAuraSystem.RebuildNow();
            _lastStatus = $"티어 {_editingTier} ← 랜덤 시드 {_randomSeed}";
        }

        private void ApplyPreset(WeaponAuraPresetKind kind, string label)
        {
            var target = WeaponAuraProfiles.Get(_editingTier);
            if (target == null)
                return;

            var preset = WeaponAuraProfiles.CreatePreset(kind, target.name, target.minLevel);
            // 텍스처 선택은 유지 — 형태/색만 프리셋으로 교체합니다.
            preset.textureName = target.textureName;
            preset.tilesX = target.tilesX;
            preset.tilesY = target.tilesY;
            preset.flipbookFps = target.flipbookFps;

            target.CopyFrom(preset);
            WeaponAuraSystem.RebuildNow();
            _lastStatus = $"티어 {_editingTier} → {label} 프리셋";
        }

        private void DrawButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("재생성"))
            {
                WeaponAuraSystem.RebuildNow();
                _lastStatus = "오라를 다시 생성했습니다.";
            }

            if (GUILayout.Button("기본값"))
            {
                WeaponAuraProfiles.ResetToDefaults();
                WeaponAuraSystem.RebuildNow();
                _lastStatus = "기본값으로 되돌렸습니다.";
            }

            GUILayout.EndHorizontal();

            DrawPresetButtons();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("코드 스니펫"))
            {
                UnityEngine.Debug.Log("[WeaponAura] 무기 오라 튜닝 결과\n" + WeaponAuraProfiles.ToCSharpSnippet());
                _lastStatus = "C# 스니펫을 로그로 출력했습니다.";
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("진단 로그 (크기·스케일·셰이더)"))
            {
                Helpers.WeaponAuraDiagnostics.Dump();
                _lastStatus = "진단 결과를 로그로 출력했습니다.";
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이 무기 OBJ"))
            {
                var result = WeaponAura.Helpers.WeaponMeshExporter.ExportHeldWeapon();
                _lastStatus = result.message;
                UnityEngine.Debug.Log($"[WeaponAura] 메시 내보내기: {result.message}");
            }

            if (GUILayout.Button("모든 무기 OBJ"))
            {
                var result = WeaponAura.Helpers.WeaponMeshExporter.ExportAllWeapons();
                _lastStatus = result.message;
                UnityEngine.Debug.Log($"[WeaponAura] 전체 메시 내보내기: {result.message}");
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("닫기"))
                Close();
        }

        // ──────────────────────────────────────────────────────────

        private void ApplyIfChanged(WeaponAuraProfile before, WeaponAuraProfile after)
        {
            // 지금 화면에 떠 있는 티어가 아니면 굳이 재적용하지 않습니다.
            bool affectsCurrent = _editingTier == WeaponAuraSystem.CurrentTier
                || WeaponAuraSystem.DebugTierOverride == _editingTier;

            bool structural = after.StructureDiffers(before) || after.minLevel != before.minLevel;
            bool anyChange = structural || JsonUtility.ToJson(before) != JsonUtility.ToJson(after);

            if (!anyChange)
                return;

            // 구조가 바뀌었거나 minLevel이 바뀌면 어느 티어를 편집했든 다시 판정해야 합니다.
            if (structural)
                WeaponAuraSystem.RebuildNow();
            else if (affectsCurrent)
                WeaponAuraSystem.ApplyLive();
        }

        private void SyncEditingTierToCurrent()
        {
            int tier = WeaponAuraSystem.CurrentTier;
            if (tier >= 0 && tier < WeaponAuraProfiles.TierCount)
                _editingTier = tier;
        }

        private string[] BuildTierOverrideOptions()
        {
            int tierCount = WeaponAuraProfiles.TierCount;
            var options = new string[tierCount + 2];
            options[0] = "자동";
            options[1] = "없음";
            for (int i = 0; i < tierCount; i++)
                options[i + 2] = i.ToString();
            return options;
        }

        private void Section(string title)
        {
            GUILayout.Space(6f);
            GUILayout.Label($"── {title}", _headerStyle);
        }

        private float Slider(string label, float value, float min, float max, string format = "0.##")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(110f));
            float result = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label(result.ToString(format), _labelStyle, GUILayout.Width(46f));
            GUILayout.EndHorizontal();
            return result;
        }

        private Color ColorSliders(string label, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(40f));

            var swatchRect = GUILayoutUtility.GetRect(22f, 16f, GUILayout.Width(22f));
            DrawSwatch(swatchRect, color);

            float r = GUILayout.HorizontalSlider(color.r, 0f, 1f);
            float g = GUILayout.HorizontalSlider(color.g, 0f, 1f);
            float b = GUILayout.HorizontalSlider(color.b, 0f, 1f);
            GUILayout.EndHorizontal();

            return new Color(r, g, b, 1f);
        }

        private void DrawSwatch(Rect rect, Color color)
        {
            if (_backgroundTexture == null)
            {
                _backgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _backgroundTexture.SetPixel(0, 0, Color.white);
                _backgroundTexture.Apply();
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _backgroundTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
                return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
            };
            _statusStyle.normal.textColor = new Color(0.6f, 0.9f, 0.6f);
        }
    }
}
