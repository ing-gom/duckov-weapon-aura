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
    /// 편집 대상 전환 — <b>등급별 / 분류별 / 개별 무기</b>.
    ///
    /// 네 탭(오라·잔상·총구·참격)이 각자 <c>CurrentXProfile()</c> 하나로 편집 대상을
    /// 정하고 있어서, 그 넷만 갈라 주면 슬라이더·미리보기·공유 코드가 전부 따라옵니다.
    /// 그래서 이 파일은 "무엇을 편집 중인가"라는 상태와 그것을 고르는 줄만 담당합니다.
    ///
    /// 해석 순서는 런타임과 같습니다 — 개별 무기 → 분류 → 등급. 구체적인 것이 이깁니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        private enum EditTarget
        {
            /// <summary>기존 동작 — 등급 티어를 편집합니다.</summary>
            Grade = 0,

            /// <summary>
            /// 무기 한 정.
            ///
            /// 예전에는 가운데에 "분류별(AR·SMG…)" 층이 하나 더 있었습니다. 없앴습니다 —
            /// 층이 셋이면 "왜 이 총이 이렇게 보이지"의 답이 세 군데로 갈리는데,
            /// 실제로 필요한 것은 "전부 등급대로" 아니면 "이 총만 따로" 둘이었습니다.
            /// </summary>
            Weapon = 2,
        }

        private EditTarget _editTarget = EditTarget.Grade;

        /// <summary>개별 무기 대상일 때의 TypeID</summary>
        private int _editWeaponTypeId;

        private readonly List<KeyValuePair<EditTarget, Button>> _targetButtons =
            new List<KeyValuePair<EditTarget, Button>>();

        private GameObject? _weaponRow;

        /// <summary>
        /// 각 탭의 "등급 고르기" 영역.
        ///
        /// 무기 한 정을 편집할 때는 등급이 아무 의미가 없습니다 — 그 무기의 값을 직접
        /// 고치는 중이니까요. 그대로 두면 "등급 3을 골랐는데 왜 안 바뀌지"가 됩니다.
        /// 탭마다 흩어져 있어서 한 목록에 모아 두고 함께 켜고 끕니다.
        /// </summary>
        private readonly List<GameObject> _gradeSelectors = new List<GameObject>();

        /// <summary>등급 선택 영역을 등록합니다 (각 탭이 만들 때 부릅니다).</summary>
        private void RegisterGradeSelector(GameObject? target)
        {
            if (target != null && !_gradeSelectors.Contains(target))
                _gradeSelectors.Add(target);
        }
        private TextMeshProUGUI? _targetStatus;
        private Button? _pickWeaponButton;
        private Button? _removeOverrideButton;

        // ── 상태 ────────────────────────────────────────────────────

        /// <summary>지금 편집 중인 전용 설정의 키. 등급 편집 중이면 빈 문자열.</summary>
        private string CurrentOverrideKey()
        {
            return _editTarget == EditTarget.Weapon && _editWeaponTypeId > 0
                ? WeaponOverrides.WeaponKey(_editWeaponTypeId)
                : "";
        }

        /// <summary>
        /// 지금 편집 중인 전용 설정. 없으면 null이고, 그때는 네 탭이 모두 기존처럼
        /// 등급 프로필을 편집합니다.
        /// </summary>
        /// <summary>재진입 방지 — Create가 알림을 쏘고 그 알림이 다시 여기로 들어옵니다.</summary>
        private bool _restoringOverride;

        private WeaponOverride? CurrentOverride()
        {
            string key = CurrentOverrideKey();
            if (key.Length == 0)
                return null;

            var found = WeaponOverrides.Get(key);
            if (found != null)
                return found;

            // 무기를 편집 대상으로 골라 둔 상태인데 설정이 없으면 <b>지금 만듭니다.</b>
            //
            // 없는 채로 두면 아래 탭들이 등급 프로필로 폴백하고, 사용자가 만지는 값이
            // 그 무기가 아니라 <b>등급 전체</b>에 들어갑니다. 실제로 저장 버튼이 편집 중인
            // 설정을 지워 버리는 바람에 그 뒤 편집이 전부 등급으로 새어 나갔습니다.
            // 대상이 무기면 그 무기의 설정이 있다 — 이 불변식을 여기서 지킵니다.
            if (_restoringOverride || _editTarget != EditTarget.Weapon || _editWeaponTypeId <= 0)
                return null;

            try
            {
                _restoringOverride = true;
                return WeaponOverrides.Create(key,
                    WeaponHelper.GetDisplayName(_editWeaponTypeId), _editWeaponTypeId);
            }
            finally
            {
                _restoringOverride = false;
            }
        }

        /// <summary>전용 설정을 편집 중인지 (탭들이 "따라가기" 자동 선택을 끌 때 씁니다)</summary>
        private bool EditingOverride => CurrentOverride() != null;

        // ── 대상 줄 ─────────────────────────────────────────────────

        /// <summary>
        /// 머리말 바로 아래에 놓이는 줄. 네 탭 어디서나 같은 자리에 보입니다 —
        /// 탭마다 따로 두면 "지금 무엇을 편집 중인지"가 탭을 옮길 때마다 흔들립니다.
        /// </summary>
        private void BuildTargetRow(Transform parent)
        {
            var holder = MakeImage("TargetRow", parent, SectionColor).rectTransform;

            var layout = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = holder.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildTargetMainRow(holder);
            BuildWeaponRow(holder);

            RefreshTargetRow();
        }

        private void BuildTargetMainRow(Transform parent)
        {
            var row = MakeRect("Main", parent);
            SetHeight(row, 38f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var label = MakeText("Label", row, L.Target.Label, 19, TextColor, TextAlignmentOptions.MidlineLeft);
            SetWidth(label.rectTransform, 90f);

            _targetButtons.Clear();
            AddTargetButton(row, L.Target.Grade, EditTarget.Grade);
            AddTargetButton(row, L.Target.Weapon, EditTarget.Weapon);

            _targetStatus = MakeText("Status", row, "", 17, DimTextColor, TextAlignmentOptions.MidlineLeft);
            _targetStatus.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _removeOverrideButton = MakeButton(row, L.Target.Remove, 160f, RemoveCurrentOverride, ButtonColor);
        }

        private void AddTargetButton(Transform parent, string label, EditTarget target)
        {
            var button = MakeButton(parent, label, 118f, () => SelectTarget(target), ButtonColor);
            _targetButtons.Add(new KeyValuePair<EditTarget, Button>(target, button));
        }

        /// <summary>
        /// 카탈로그에 실제로 있는 분류. 총기 분류 먼저, 근접은 마지막.
        ///
        /// 분류는 이제 설정 층이 아니라 <b>무기를 찾는 수단</b>으로만 씁니다 —
        /// 라이브러리에서 "저격총만 보기" 같은 필터에 쓰입니다.
        /// </summary>
        private static List<string> CollectClasses()
        {
            var guns = new List<string>();
            bool hasMelee = false;

            foreach (var entry in WeaponCatalog.All)
            {
                if (entry.Kind == WeaponKind.Melee)
                {
                    hasMelee = true;
                    continue;
                }

                if (entry.GunClass != null && !guns.Contains(entry.GunClass))
                    guns.Add(entry.GunClass);
            }

            guns.Sort(StringComparer.Ordinal);

            if (hasMelee)
                guns.Add(WeaponHelper.MeleeClass);

            return guns;
        }

        private void BuildWeaponRow(Transform parent)
        {
            var row = MakeRect("Weapon", parent);
            SetHeight(row, 34f);
            _weaponRow = row.gameObject;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            _pickWeaponButton = MakeButton(row, L.Target.Pick, 200f, OpenWeaponLibrary, ButtonAccentColor);

            row.gameObject.SetActive(false);
        }

        // ── 선택 ────────────────────────────────────────────────────

        private void SelectTarget(EditTarget target)
        {
            if (_editTarget == target)
            {
                // 같은 버튼을 다시 누르면 고르는 줄만 다시 띄웁니다 (무기를 바꾸고 싶을 때).
                if (target == EditTarget.Weapon)
                    OpenWeaponLibrary();

                return;
            }

            PruneUnmodified();

            _editTarget = target;

            // 개별 무기로 넘어왔는데 아직 아무것도 안 골랐으면 고르는 줄을 띄웁니다.
            if (target == EditTarget.Weapon && _editWeaponTypeId <= 0)
                OpenWeaponLibrary();

            RefreshTargetRow();
            SyncAllTabs();
        }

        /// <summary>
        /// 탭을 옮기면 고른 무기를 놓습니다.
        ///
        /// 탭마다 붙는 무기 종류가 다릅니다 — 근접 참격에서 고른 칼을 들고 총구 화염 탭으로
        /// 넘어가면, 그 무기에는 총구가 없어서 무엇을 편집하는지 알 수 없는 상태가 됩니다.
        /// 등급별로 돌려놓으면 어느 탭에서든 뜻이 통합니다.
        /// </summary>
        private void ResetWeaponTargetForTab()
        {
            if (_editTarget != EditTarget.Weapon)
                return;

            PruneUnmodified();

            _editTarget = EditTarget.Grade;
            _editWeaponTypeId = 0;

            if (_preview != null)
                _preview.TargetWeaponTypeId = 0;

            RefreshTargetRow();
        }

        private void SelectWeapon(int typeId)
        {
            if (typeId <= 0)
                return;

            PruneUnmodified();

            _editWeaponTypeId = typeId;
            _editTarget = EditTarget.Weapon;

            EnsureOverride(WeaponOverrides.WeaponKey(typeId), WeaponHelper.GetDisplayName(typeId), typeId);

            RefreshTargetRow();
            SyncAllTabs();
        }

        /// <summary>
        /// 고른 대상에 전용 설정이 없으면 만듭니다.
        ///
        /// 고르는 순간 만드는 이유 — 없는 채로 두면 슬라이더가 조용히 <b>등급 프로필</b>을
        /// 고치게 됩니다. "이 총만 바꾸려 했는데 같은 등급 무기가 전부 바뀌었다"가 되는
        /// 최악의 경우입니다. 대신 시작값이 지금 값 그대로라 만들어도 화면은 안 바뀌고,
        /// 하나도 안 고친 것은 <see cref="PruneUnmodified"/>가 알아서 걷어냅니다.
        /// </summary>
        private void EnsureOverride(string key, string label, int sampleTypeId)
        {
            if (WeaponOverrides.Has(key))
                return;

            WeaponOverrides.Create(key, label, sampleTypeId);
            ShowHint(string.Format(L.Target.Created, label));
        }

        /// <summary>
        /// 만들었지만 값을 하나도 안 고친 전용 설정을 걷어냅니다.
        ///
        /// 라이브러리에서 이것저것 눌러 보기만 해도 항목이 쌓이면, 저장 파일이 지저분해지고
        /// "내가 언제 이걸 만들었지" 목록이 됩니다. 대상을 옮길 때마다 직전 것을 검사해서
        /// 기본값과 같으면 지웁니다.
        /// </summary>
        private void PruneUnmodified()
        {
            var current = CurrentOverride();
            if (current == null)
                return;

            if (!IsUnmodified(current))
                return;

            if (WeaponOverrides.Remove(current.key))
                ShowHint(L.Target.Pruned);
        }

        /// <summary>
        /// 전용 설정이 "만들었을 때 그대로"인지.
        ///
        /// 필드를 하나씩 비교하는 대신 같은 시작값을 다시 만들어 JSON으로 견줍니다.
        /// 프로필에 필드가 추가돼도 비교가 저절로 따라옵니다.
        /// </summary>
        private static bool IsUnmodified(WeaponOverride entry)
        {
            try
            {
                int sampleTypeId = SampleTypeIdFor(entry.key);
                int quality = WeaponHelper.GetMetaQuality(sampleTypeId);

                int tier = WeaponAuraProfiles.ResolveTier(quality);

                return SameJson(entry.aura, WeaponAuraProfiles.Get(tier))
                       && SameJson(entry.trail, BulletTrailProfiles.Resolve(quality))
                       && SameJson(entry.muzzle, MuzzleFlashProfiles.Resolve(quality))
                       && SameJson(entry.melee, MeleeSlashProfiles.Resolve(quality));
            }
            catch
            {
                // 판단이 안 되면 남겨 둡니다 — 사용자가 만든 것을 실수로 지우는 것보다
                // 안 쓰는 항목이 하나 남는 편이 낫습니다.
                return false;
            }
        }

        private static bool SameJson(object? a, object? b)
        {
            if (a == null || b == null)
                return false;

            return JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        }

        /// <summary>키에서 기준 무기 TypeID를 되찾습니다 (등급 기본값을 다시 뜨기 위한 것).</summary>
        private static int SampleTypeIdFor(string key)
        {
            const string weaponPrefix = "weapon:";

            return key.StartsWith(weaponPrefix, StringComparison.Ordinal)
                   && int.TryParse(key.Substring(weaponPrefix.Length), out int typeId)
                ? typeId
                : 0;
        }

        private void RemoveCurrentOverride()
        {
            var current = CurrentOverride();
            if (current == null)
                return;

            string label = current.ResolveLabel();

            if (WeaponOverrides.Remove(current.key))
            {
                ShowHint(string.Format(L.Target.Removed, label));

                // 지운 대상에 그대로 머물면 "만들기"가 곧바로 다시 만들어 버립니다.
                _editTarget = EditTarget.Grade;

                RefreshTargetRow();
                SyncAllTabs();
            }
        }

        // ── 갱신 ────────────────────────────────────────────────────

        private void RefreshTargetRow()
        {
            foreach (var pair in _targetButtons)
            {
                if (pair.Value?.targetGraphic != null)
                {
                    pair.Value.targetGraphic.color =
                        pair.Key == _editTarget ? ButtonAccentColor : ButtonColor;
                }
            }

            if (_weaponRow != null)
                _weaponRow.SetActive(_editTarget == EditTarget.Weapon);

            if (_pickWeaponButton != null)
            {
                var label = _pickWeaponButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = _editWeaponTypeId > 0
                        ? WeaponHelper.GetDisplayName(_editWeaponTypeId)
                        : L.Target.Pick;
                }
            }

            var over = CurrentOverride();

            if (_targetStatus != null)
            {
                if (_editTarget == EditTarget.Grade)
                {
                    _targetStatus.text = "";
                    _targetStatus.color = DimTextColor;
                }
                else if (over != null)
                {
                    // 예전에 만든 전용 설정은 그때의 등급 상태를 물려받아 꺼져 있을 수
                    // 있습니다. 그 상태로는 무기를 골라도 아무것도 안 보이는데, 이유가
                    // 화면 어디에도 없으면 "이 무기만 고장났다"로 읽힙니다.
                    bool off = !over.aura.enabled;

                    _targetStatus.text = off
                        ? string.Format(L.Target.EditingOff, over.ResolveLabel())
                        : string.Format(L.Target.Editing, over.ResolveLabel());

                    _targetStatus.color = off ? WarnTextColor : DimTextColor;
                }
                else
                {
                    _targetStatus.text = L.Target.UsingGrade;
                    _targetStatus.color = DimTextColor;
                }
            }

            if (_removeOverrideButton != null)
                _removeOverrideButton.gameObject.SetActive(over != null);

            // 무기 한 정을 편집하는 동안에는 등급 격자를 숨깁니다.
            bool showGrades = _editTarget != EditTarget.Weapon;
            foreach (var selector in _gradeSelectors)
            {
                if (selector != null)
                    selector.SetActive(showGrades);
            }
        }

        /// <summary>
        /// 네 탭을 지금 대상의 값으로 다시 채웁니다.
        ///
        /// 대상이 바뀌면 보이는 값이 통째로 달라지므로 탭 하나만 갱신하면 안 됩니다 —
        /// 다른 탭으로 넘어갔을 때 이전 대상의 값이 남아 있게 됩니다.
        /// </summary>
        private void SyncAllTabs()
        {
            try
            {
                SyncFromProfile();
                SyncTrailFromProfile();
                SyncMuzzleFromProfile();
                SyncMeleeFromProfile();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 대상 전환 갱신 실패: {ex.Message}");
            }

            // 무기별 설정을 편집 중이면 미리보기도 그 무기를 보여 줘야 합니다.
            // 손에 든 다른 총에 편집 중인 색이 칠해져 있으면 무엇을 고치는지 알 수 없습니다.
            if (_preview != null)
            {
                _preview.TargetWeaponTypeId =
                    _editTarget == EditTarget.Weapon ? _editWeaponTypeId : 0;
            }

            // 전용 설정을 고치면 든 무기에 즉시 반영되어야 합니다.
            WeaponOverrides.NotifyChanged();
            _preview?.RequestRebuild();
        }
    }
}
