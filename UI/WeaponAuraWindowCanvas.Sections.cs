using UnityEngine;
using UnityEngine.UI;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 탭 안쪽을 <b>기본 / 고급</b>으로 가르는 장치.
    ///
    /// 네 탭이 모두 같은 문제를 갖고 있습니다 — 자주 만지는 값(색·크기)과 성격을 다듬는
    /// 값(노이즈·부채꼴·불꽃 세부)이 한 줄로 늘어서서, 처음 여는 사람이 어디부터 봐야
    /// 할지 알 수 없습니다. 탭마다 따로 짜면 동작이 미묘하게 갈리므로 한 곳에 둡니다.
    /// </summary>
    public partial class WeaponAuraWindowCanvas
    {
        /// <summary>기본/고급 한 벌. 탭마다 하나씩 들고 있습니다.</summary>
        private sealed class SectionSwitch
        {
            public GameObject Basic = null!;
            public GameObject Advanced = null!;
            public Button BasicButton = null!;
            public Button AdvancedButton = null!;


            public void Select(bool advanced) => Select(advanced ? 1 : 0);

            /// <summary>0=기본 · 1=고급</summary>
            public void Select(int index)
            {
                if (Basic != null)
                    Basic.SetActive(index == 0);
                if (Advanced != null)
                    Advanced.SetActive(index == 1);

                Paint(BasicButton, index == 0);
                Paint(AdvancedButton, index == 1);
            }

            private static void Paint(Button? button, bool selected)
            {
                if (button != null && button.targetGraphic != null)
                    button.targetGraphic.color = selected ? ButtonAccentColor : ButtonColor;
            }
        }

        /// <summary>
        /// 오른쪽 열을 만들고 기본/고급 두 묶음을 돌려줍니다.
        ///
        /// 스크롤 본체는 <see cref="BuildScrollBody"/>가 만들고, 여기서는 그 위에 버튼 줄을
        /// 얹고 내용 컨테이너를 둘로 나누기만 합니다.
        /// </summary>
        private SectionSwitch BuildSectionedColumn(Transform parent)
        {
            var column = MakeImage("Right", parent, SectionColor).rectTransform;
            column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 0);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var switcher = new SectionSwitch();

            var row = MakeRect("SectionRow", column);
            SetHeight(row, 36f);

            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;

            switcher.BasicButton = MakeButton(row, L.Section.Basic, 0f,
                () => switcher.Select(0), ButtonColor);
            switcher.AdvancedButton = MakeButton(row, L.Section.Advanced, 0f,
                () => switcher.Select(1), ButtonColor);

            var scrollGo = MakeRect("Scroll", column);
            scrollGo.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var content = BuildScrollBody(scrollGo);

            switcher.Basic = MakeStack("Basic", content);
            switcher.Advanced = MakeStack("Advanced", content);

            return switcher;
        }

        /// <summary>세로로 쌓기만 하는 빈 컨테이너 (기본/고급을 통째로 켜고 끄기 위한 것).</summary>
        private static GameObject MakeStack(string name, Transform parent)
        {
            var rect = MakeRect(name, parent);

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect.gameObject;
        }
    }
}
