using System.Collections.Generic;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>꼬리를 어떻게 그릴지.</summary>
    public enum BulletTrailStyle
    {
        /// <summary>이어진 선 하나 (기본)</summary>
        Line = 0,
        /// <summary>지나간 자리에 도형을 일정 간격으로 남김</summary>
        Stamp = 1,
    }

    /// <summary>총알 머리 모양.</summary>
    public enum BulletHeadShape
    {
        /// <summary>게임 원본과 같은 계열 — 길이 전체가 고른 굵기의 띠 (기본)</summary>
        Capsule = 0,
        /// <summary>둥근 점</summary>
        Dot = 1,
        /// <summary>앞뒤로 뾰족한 마름모</summary>
        Diamond = 2,
        /// <summary>한쪽이 뾰족한 화살촉 — 날아가는 방향이 읽힙니다</summary>
        Arrow = 3,
        /// <summary>가운데가 빈 고리</summary>
        Ring = 4,
        /// <summary>십자로 뻗는 반짝임</summary>
        Spark = 5,
    }

    /// <summary>
    /// 총알 머리 텍스처를 코드로 그립니다.
    ///
    /// 머리는 TrailRenderer에 <c>LineTextureMode.Stretch</c>로 그려집니다. 그래서 텍스처
    /// 한 장이 머리 길이 전체에 한 번 늘어납니다 — <b>가로(U)가 진행 방향, 세로(V)가 굵기</b>
    /// 입니다. 총구 화염 도형(<see cref="MuzzleFlashShapes"/>)이 정사각형에 그대로 찍히는
    /// 것과 달리, 여기 도형은 늘어난다는 전제로 그려야 모양이 유지됩니다.
    ///
    /// 가산 합성이라 색은 흰색으로 두고 알파에만 모양을 담습니다 — 실제 색은
    /// TrailRenderer의 그라디언트가 입힙니다.
    /// </summary>
    public static class BulletHeadShapes
    {
        private const int Resolution = 64;

        private static readonly Dictionary<BulletHeadShape, Texture2D> _textures =
            new Dictionary<BulletHeadShape, Texture2D>();

        /// <summary>설정 창에서 보여 줄 순서</summary>
        public static readonly BulletHeadShape[] All =
        {
            BulletHeadShape.Capsule,
            BulletHeadShape.Dot,
            BulletHeadShape.Diamond,
            BulletHeadShape.Arrow,
            BulletHeadShape.Ring,
            BulletHeadShape.Spark,
        };

        /// <summary>
        /// 이 프로필이 실제로 쓸 텍스처.
        ///
        /// 이름이 있으면 그쪽이 우선입니다 — 직접 그린 도형이 먼저고, 없으면
        /// <c>assets/vfx_textures/</c>의 PNG, 둘 다 못 찾으면 내장 도형으로 돌아갑니다.
        /// (총구 화염과 같은 규칙이라 두 탭에서 같은 도형을 골라 쓸 수 있습니다)
        /// </summary>
        public static Texture2D Resolve(BulletHeadShape shape, string? textureName)
        {
            if (!string.IsNullOrEmpty(textureName))
            {
                var drawn = CustomShapes.GetTexture(textureName);
                if (drawn != null)
                    return drawn;

                var loaded = WeaponAuraResources.LoadTexture(textureName!);
                if (loaded != null)
                    return loaded;
            }

            return Get(shape);
        }

        public static Texture2D Get(BulletHeadShape shape)
        {
            if (_textures.TryGetValue(shape, out var cached) && cached != null)
                return cached;

            var texture = Create(shape);
            _textures[shape] = texture;
            return texture;
        }

        public static void Dispose()
        {
            foreach (var texture in _textures.Values)
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            _textures.Clear();
        }

        private static Texture2D Create(BulletHeadShape shape)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "WeaponAura_BulletHead_" + shape,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            {
                // 굵기 축: -1(한쪽 가장자리) ~ +1(반대쪽 가장자리)
                float v = (y + 0.5f) / Resolution * 2f - 1f;

                for (int x = 0; x < Resolution; x++)
                {
                    // 진행 축: 0 ~ 1
                    float u = (x + 0.5f) / Resolution;
                    float alpha = Mathf.Clamp01(Coverage(shape, u, v));

                    pixels[y * Resolution + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <param name="u">진행 방향 0~1</param>
        /// <param name="v">굵기 방향 -1~1</param>
        private static float Coverage(BulletHeadShape shape, float u, float v)
        {
            switch (shape)
            {
                case BulletHeadShape.Dot:
                {
                    // 늘어나기 전 기준으로 원을 그립니다. 머리 길이를 늘리면
                    // 그만큼 타원으로 퍼지는데, 그게 "빠른 총알" 느낌이라 그대로 둡니다.
                    float dx = (u - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx + v * v);
                    return Falloff(1f - d);
                }

                case BulletHeadShape.Diamond:
                {
                    float dx = Mathf.Abs(u - 0.5f) * 2f;
                    return Falloff(1f - (dx + Mathf.Abs(v)) / 0.95f);
                }

                case BulletHeadShape.Arrow:
                {
                    // u=1 쪽이 뾰족한 끝. 뒤로 갈수록 넓어집니다.
                    float halfWidth = Mathf.Lerp(1f, 0.05f, u);
                    float t = 1f - Mathf.Abs(v) / Mathf.Max(0.02f, halfWidth);
                    return Falloff(t);
                }

                case BulletHeadShape.Ring:
                {
                    float dx = (u - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx + v * v);

                    // 반지름 0.62 언저리에만 테두리를 남깁니다.
                    float band = 1f - Mathf.Abs(d - 0.62f) / 0.3f;
                    return Falloff(band);
                }

                case BulletHeadShape.Spark:
                {
                    float dx = (u - 0.5f) * 2f;

                    // 가로·세로로 뻗는 두 막대를 겹칩니다.
                    float horizontal = Falloff(1f - Mathf.Abs(v) / 0.28f) * Falloff(1f - Mathf.Abs(dx));
                    float vertical = Falloff(1f - Mathf.Abs(dx) / 0.28f) * Falloff(1f - Mathf.Abs(v));
                    return Mathf.Max(horizontal, vertical);
                }

                case BulletHeadShape.Capsule:
                default:
                {
                    // 길이 전체가 고른 굵기 — 양 끝만 살짝 둥글립니다.
                    // 게임 원본 총알이 이 모양이라 기본값으로 둡니다.
                    float across = Falloff(1f - Mathf.Abs(v));

                    float dx = Mathf.Abs(u - 0.5f) * 2f;
                    float along = Mathf.Clamp01((1f - dx) / 0.18f);

                    return across * Mathf.Min(1f, along);
                }
            }
        }

        /// <summary>
        /// 가장자리 처리.
        ///
        /// 예전에는 제곱으로 부드럽게 떨궜는데, 머리에는 HDR 발광(원본 기준 4.58배)이
        /// 얹힙니다. 알파가 10%만 돼도 그만큼 밝아져서 흐린 가장자리가 통째로 흰색으로
        /// 터지고, 마름모든 고리든 실루엣이 사라졌습니다.
        ///
        /// 그래서 안쪽은 꽉 채우고 <b>가장자리에만 좁은 띠</b>를 남깁니다. 계단처럼 보이지
        /// 않을 만큼만 부드럽고, 밝아져도 모양은 유지됩니다.
        /// </summary>
        private static float Falloff(float value)
        {
            const float edge = 0.22f;
            return Mathf.Clamp01(value / edge);
        }
    }
}
