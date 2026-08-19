using System;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>알갱이를 무기의 어디서 뿜을지</summary>
    public enum WeaponParticleAnchor
    {
        /// <summary>총구 소켓 (총기만). 없으면 본체로 떨어집니다.</summary>
        Muzzle = 0,

        /// <summary>무기 본체 한가운데 — 한 점에서 퍼집니다.</summary>
        Body = 1,

        /// <summary>총열 앞쪽 — 본체 바운즈의 진행 방향 끝</summary>
        Barrel = 2,

        /// <summary>
        /// 무기 전체 — 바운즈 상자 전체에서 고르게 뿜습니다.
        ///
        /// 오라가 무기를 감싸는 방식과 같은 기준입니다. 한 점에서 나오는 것과 달리
        /// 무기 실루엣을 따라 흩날려서 "무기가 전체적으로 뭔가를 두르고 있다"가 됩니다.
        /// </summary>
        Whole = 3,
    }

    /// <summary>
    /// 알갱이가 어느 쪽으로 뿜어지는지.
    ///
    /// 기준은 <b>무기</b>입니다 — 총을 돌리면 방향도 함께 돕니다. 월드 기준(항상 아래로)이
    /// 필요하면 방향 대신 <c>rise</c>를 음수로 주면 중력이 그 역할을 합니다.
    /// </summary>
    public enum WeaponParticleDirection
    {
        /// <summary>사방으로 (기본). 구 모양으로 퍼집니다.</summary>
        Sphere = 0,

        /// <summary>총구가 향하는 쪽</summary>
        Forward = 1,

        /// <summary>총구 반대쪽</summary>
        Backward = 2,

        /// <summary>무기 위쪽</summary>
        Up = 3,

        /// <summary>무기 아래쪽 — 총구에서 물이 떨어지는 연출은 이것입니다.</summary>
        Down = 4,
    }

    /// <summary>
    /// 무기에 붙는 알갱이 한 겹.
    ///
    /// 하나만 두지 않고 <b>겹쳐 쌓을 수 있게</b> 만든 이유 — 하나로는 밀도가 안 나옵니다.
    /// "총구에 주황 불티 + 무기 전체에 옅은 연기"처럼 성격이 다른 것을 얹어야 인상이
    /// 만들어집니다.
    ///
    /// 예전에는 여기에 점광원(빛) 종류도 있었습니다. 뺐습니다 — 게임 조명 에셋을 빌려
    /// 쓰는 방식이라 무기마다 결과가 달랐고(점광원이 아예 없는 무기도 있었습니다),
    /// 조절할 수 있는 것도 색·반경 정도뿐이었습니다. 알갱이는 우리가 만드는 것이라
    /// 어느 무기에서나 같게 동작하고 조절 폭도 넓습니다.
    ///
    /// JsonUtility로 직렬화되므로 public 필드만 씁니다.
    /// </summary>
    [Serializable]
    public class WeaponEffectLayer
    {
        public string name = "Layer";
        public bool enabled = true;

        public Color color = new Color(1f, 0.45f, 0.18f, 1f);

        /// <summary>색 밝기 배율. 1을 넘으면 블룸에 걸려 타오르는 느낌이 납니다.</summary>
        public float intensity = 2f;

        /// <summary>알갱이 크기(m)</summary>
        public float size = 0.05f;

        /// <summary>초당 몇 개를 뿜을지</summary>
        public float rate = 18f;

        /// <summary>알갱이 하나가 살아 있는 시간(초)</summary>
        public float lifetime = 0.7f;

        /// <summary>뿜어져 나가는 속도(m/s)</summary>
        public float speed = 0.35f;

        /// <summary>
        /// 퍼지는 범위.
        ///
        /// 한 점에서 뿜는 자리(총구·본체·총열)에서는 <b>구의 반지름(m)</b>이고,
        /// 무기 전체에서는 <b>바운즈 상자를 얼마나 부풀릴지</b>의 배율입니다.
        /// </summary>
        public float spread = 0.08f;

        /// <summary>
        /// 호를 따라 퍼뜨릴지 — <b>근접 참격에서만</b> 씁니다.
        ///
        /// 참격은 반월입니다. 한 점에서 뿜으면 그 반월과 아무 상관 없는 자리에서 알갱이가
        /// 나와서, 게임이 그리는 참격과 우리 레이어가 따로 놉니다. 켜 두면 반월 위 전체에
        /// 고르게 뿜고 바깥으로 퍼져 나갑니다 — 참격 모양과 자리가 맞습니다.
        ///
        /// 끄면 호 위의 한 점(참격 방향 값이 가리키는 곳)에서만 뿜습니다. 총구처럼
        /// 한 자리에서 터뜨리고 싶을 때 씁니다.
        ///
        /// 켜져 있는 동안은 <see cref="direction"/>이 뜻을 잃습니다 — 퍼지는 방향이
        /// 호의 바깥으로 정해져 있기 때문입니다.
        /// </summary>
        public bool arcSpread = true;

        /// <summary>알갱이 모양. 총구 화염·총알 머리와 같은 그림 목록을 씁니다.</summary>
        public MuzzleFlashShape shape = MuzzleFlashShape.Glow;

        /// <summary>사용자 텍스처 이름. 비어 있으면 <see cref="shape"/>를 씁니다.</summary>
        public string textureName = "";

        /// <summary>위로 떠오르는 정도(m/s). 음수면 가라앉습니다.</summary>
        public float rise = 0.15f;

        // ── 방향 ────────────────────────────────────────────────

        public WeaponParticleDirection direction = WeaponParticleDirection.Sphere;

        /// <summary>방향이 있을 때 원뿔이 벌어지는 각도(도). 0에 가까우면 한 줄기입니다.</summary>
        public float coneAngle = 25f;

        // ── 수명에 따른 변화 ────────────────────────────────────
        //
        // 이게 없으면 무엇을 골라도 "같은 색 알갱이가 뿅 나타났다 뿅 사라지는" 그림이
        // 됩니다. 불꽃은 식어가며 색이 바뀌고 연기는 커지며 옅어집니다 — 그 변화가
        // 알갱이를 무엇으로 읽을지 정합니다.

        /// <summary>수명 끝의 색을 따로 쓸지. 꺼져 있으면 시작색을 끝까지 씁니다.</summary>
        public bool useColorEnd;

        /// <summary>수명 끝의 색 (<see cref="useColorEnd"/>가 켜져 있을 때)</summary>
        public Color colorEnd = new Color(1f, 0.15f, 0.05f, 1f);

        /// <summary>수명 시작·끝의 투명도</summary>
        public float alphaStart = 1f;
        public float alphaEnd;

        /// <summary>수명 시작·끝의 크기 배율</summary>
        public float sizeStart = 1f;
        public float sizeEnd = 1f;

        // ── 모양새 ──────────────────────────────────────────────

        /// <summary>속도 방향으로 늘일지. 물방울·불티처럼 길쭉한 것에 씁니다.</summary>
        public bool stretch;

        /// <summary>늘이는 정도</summary>
        public float stretchScale = 2f;

        /// <summary>흔들림. 곧게 날아가는 것과 일렁이는 것을 가릅니다.</summary>
        public float noise;

        /// <summary>알갱이가 도는 속도(도/초)</summary>
        public float spin;

        public WeaponParticleAnchor anchor = WeaponParticleAnchor.Whole;

        /// <summary>붙인 자리에서의 앞뒤 이동(m)</summary>
        public Vector3 offset = Vector3.zero;

        /// <summary>숨쉬듯 밝기가 오르내리는 폭 (0이면 고정)</summary>
        public float pulseAmount;

        /// <summary>맥동 한 번에 걸리는 시간(초)</summary>
        public float pulsePeriod = 1.6f;

        /// <summary>
        /// 새 겹의 시작값. 오라 색을 물려받습니다 — 흰 알갱이가 튀어나오면
        /// "추가했더니 색이 초기화됐다"로 읽힙니다.
        /// </summary>
        public static WeaponEffectLayer CreateDefault(Color color)
        {
            return new WeaponEffectLayer
            {
                enabled = true,
                color = color,
                intensity = 2f,

                // 처음엔 0.05m로 잡았는데 그 크기에서는 도형을 구분할 수가 없습니다 —
                // 무엇을 골라도 작은 점으로 보여서 "모양이 적용되나?"가 됩니다.
                // 크게 시작해서 줄이는 쪽이 낫습니다.
                size = 0.12f,

                rate = 12f,
                lifetime = 0.8f,
                speed = 0.2f,
                spread = 0.2f,
                shape = MuzzleFlashShape.Glow,
                rise = 0.2f,
                direction = WeaponParticleDirection.Sphere,
                coneAngle = 25f,
                useColorEnd = false,
                colorEnd = color,
                alphaStart = 1f,
                alphaEnd = 0f,
                sizeStart = 1f,
                sizeEnd = 1f,
                stretch = false,
                stretchScale = 2f,
                noise = 0f,
                spin = 0f,
                anchor = WeaponParticleAnchor.Whole,
                offset = Vector3.zero,
                pulseAmount = 0f,
                pulsePeriod = 1.6f,
            };
        }

        public WeaponEffectLayer Clone()
        {
            var clone = new WeaponEffectLayer();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), clone);
            return clone;
        }

        /// <summary>세기를 곱한 실제 알갱이 색.</summary>
        public Color ResolveColor()
        {
            float scale = Mathf.Max(0f, intensity);
            return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
        }
    }

}
