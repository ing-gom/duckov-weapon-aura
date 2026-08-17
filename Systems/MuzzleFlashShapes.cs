using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>내장 도형. 파일을 고르지 않았을 때 쓰는 모양입니다.</summary>
    public enum MuzzleFlashShape
    {
        /// <summary>가운데가 밝은 원형 글로우 (기본 화염)</summary>
        Glow = 0,
        Heart = 1,
        Star = 2,
        Diamond = 3,
        Ring = 4,
        Sparkle = 5,
    }

    /// <summary>
    /// 내장 도형 텍스처를 코드로 그려 둡니다.
    ///
    /// 이미지 파일을 모드에 넣어 배포하면 재배포 라이선스를 따져야 하고, 사용자가 폴더에
    /// 뭔가 넣기 전에는 고를 것이 하나도 없습니다. 도형 몇 개는 수식으로 그리면 그만이라
    /// 기본 제공은 코드로 만듭니다. 사용자 PNG는 <c>assets/vfx_textures/</c>에서 그대로 읽습니다
    /// (<see cref="WeaponAuraResources.GetTextureNames"/>).
    ///
    /// 가산 합성이라 색은 흰색으로 두고 알파에만 모양을 담습니다 — 실제 색은 파티클이 입힙니다.
    /// </summary>
    public static class MuzzleFlashShapes
    {
        private const int Resolution = 128;

        private static readonly Dictionary<MuzzleFlashShape, Texture2D> _textures =
            new Dictionary<MuzzleFlashShape, Texture2D>();

        private static readonly Dictionary<Texture2D, Material> _materials =
            new Dictionary<Texture2D, Material>();

        /// <summary>설정 창에서 보여 줄 순서</summary>
        public static readonly MuzzleFlashShape[] All =
        {
            MuzzleFlashShape.Glow,
            MuzzleFlashShape.Heart,
            MuzzleFlashShape.Star,
            MuzzleFlashShape.Diamond,
            MuzzleFlashShape.Ring,
            MuzzleFlashShape.Sparkle,
        };

        /// <summary>
        /// 이 프로필이 실제로 쓸 텍스처.
        /// 파일 이름이 있으면 그쪽이 우선이고, 없거나 못 읽으면 내장 도형으로 돌아갑니다.
        /// </summary>
        public static Texture2D Resolve(MuzzleFlashShape shape, string? textureName)
        {
            if (!string.IsNullOrEmpty(textureName))
            {
                // 직접 그린 도형이 먼저입니다. 설정 창에서 방금 만든 것이 파일보다
                // 눈에 밟히는 게 자연스럽고, 이름이 겹치면 그린 쪽을 원했을 것입니다.
                var drawn = CustomShapes.GetTexture(textureName);
                if (drawn != null)
                    return drawn;

                var loaded = WeaponAuraResources.LoadTexture(textureName!);
                if (loaded != null)
                    return loaded;
            }

            return Get(shape);
        }

        /// <summary>
        /// 이름 하나로 도형을 찾습니다 — 내장 도형 · 직접 그린 도형 · vfx_textures의 PNG 순.
        ///
        /// 세 군데를 뒤지는 순서가 기능마다 다르면 같은 이름이 탭에 따라 다른 그림이 됩니다.
        /// 총구 화염 · 근접 참격 · 무기 오라가 모두 이 한 곳을 지나갑니다.
        /// </summary>
        public static Texture2D? ResolveByName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (Enum.TryParse(name, out MuzzleFlashShape shape) && Array.IndexOf(All, shape) >= 0)
                return Get(shape);

            // 설정 창에서 방금 그린 것이 파일보다 눈에 밟히는 게 자연스럽습니다.
            var drawn = CustomShapes.GetTexture(name!);
            if (drawn != null)
                return drawn;

            return WeaponAuraResources.LoadTexture(name!);
        }

        public static Texture2D Get(MuzzleFlashShape shape)
        {
            if (_textures.TryGetValue(shape, out var cached) && cached != null)
                return cached;

            var texture = Create(shape);
            _textures[shape] = texture;
            return texture;
        }

        /// <summary>텍스처에 대응하는 가산 파티클 머티리얼 (텍스처당 하나만 만듭니다).</summary>
        public static Material GetMaterial(Texture2D texture)
        {
            if (texture == null)
                return WeaponAuraResources.SharedMaterial;

            if (_materials.TryGetValue(texture, out var cached) && cached != null)
                return cached;

            Shader? shader = FindShader(
                "Particles/Additive",
                "Legacy Shaders/Particles/Additive",
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Sprites/Default");

            // 마지막 보루는 오라가 쓰는 머티리얼의 셰이더입니다. 그건 이 빌드에 확실히 들어
            // 있습니다(오라가 실제로 그려지고 있으니까요). new Material(null)로 만들면
            // 아무것도 그려지지 않는데, 그러면 "작다"와 "안 보인다"를 구분할 수가 없습니다.
            if (shader == null)
            {
                var fallback = WeaponAuraResources.SharedMaterial;
                shader = fallback != null ? fallback.shader : null;
            }

            if (shader == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[WeaponAura] 총구 화염용 셰이더를 하나도 찾지 못했습니다. 도형이 보이지 않습니다.");
                return WeaponAuraResources.SharedMaterial;
            }

            var material = new Material(shader)
            {
                name = "WeaponAura_MuzzleShape_" + texture.name,
                mainTexture = texture,
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);

            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);    // 1 = Transparent
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 1f);      // 1 = Additive
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);

            // 이 빌드에 실제로 들어 있는 셰이더는 URP 파티클입니다(로그로 확인).
            // URP 계열은 _Surface 값만 바꿔서는 투명으로 안 그려집니다 — 키워드가 있어야
            // 합니다. 오라의 면 머티리얼이 같은 이유로 이 키워드를 켭니다.
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

            _materials[texture] = material;
            return material;
        }

        private static Shader? FindShader(params string[] names)
        {
            foreach (string name in names)
            {
                var shader = Shader.Find(name);
                if (shader != null)
                    return shader;
            }
            return null;
        }

        // ── 도형 그리기 ──────────────────────────────────────────
        //
        // 좌표는 -1~1로 정규화해서 씁니다. 각 도형은 "안쪽이면 1, 바깥이면 0"인
        // 커버리지를 돌려주고, 가장자리는 한 픽셀 폭으로 부드럽게 깎습니다
        // (안 그러면 128픽셀 도형의 계단이 그대로 보입니다).

        private static Texture2D Create(MuzzleFlashShape shape)
        {
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "WeaponAura_Shape_" + shape,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[Resolution * Resolution];

            for (int y = 0; y < Resolution; y++)
            {
                float ny = (y + 0.5f) / Resolution * 2f - 1f;

                for (int x = 0; x < Resolution; x++)
                {
                    float nx = (x + 0.5f) / Resolution * 2f - 1f;
                    float alpha = Mathf.Clamp01(Coverage(shape, nx, ny));

                    pixels[y * Resolution + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static float Coverage(MuzzleFlashShape shape, float x, float y)
        {
            switch (shape)
            {
                case MuzzleFlashShape.Heart:
                    return Heart(x, y);

                case MuzzleFlashShape.Star:
                    // 뾰족한 끝이 위를 향하도록 4분의 1 바퀴 돌려 둡니다.
                    return Polar(x, y, points: 5, innerRatio: 0.4f,
                        sharpness: 3f, rotation: -Mathf.PI * 0.5f);

                case MuzzleFlashShape.Diamond:
                    return Falloff(1f - (Mathf.Abs(x) + Mathf.Abs(y)) / 0.95f);

                case MuzzleFlashShape.Ring:
                    return Ring(x, y);

                case MuzzleFlashShape.Sparkle:
                    return Polar(x, y, points: 4, innerRatio: 0.1f,
                        sharpness: 3f, rotation: 0f);

                case MuzzleFlashShape.Glow:
                default:
                    // 가운데가 밝고 가장자리로 제곱 감쇠 — 내장 글로우와 같은 계열
                    float d = Mathf.Sqrt(x * x + y * y);
                    float a = Mathf.Clamp01(1f - d);
                    return a * a;
            }
        }

        /// <summary>
        /// 하트 = 원 두 개 + 아래를 향한 삼각형.
        ///
        /// 처음에는 고전적인 음함수 (x²+y²-1)³ = x²y³ 를 썼는데, 그 식은 자리마다 기울기가
        /// 크게 달라서 경계 폭이 들쭉날쭉합니다. 결과가 윤곽 없는 뿌연 덩어리라 하트로
        /// 보이지 않았습니다. 도형을 직접 합치면 가장자리 폭을 일정하게 잡을 수 있습니다.
        /// </summary>
        private static float Heart(float x, float y)
        {
            const float lobeX = 0.36f;      // 위쪽 두 원의 중심
            const float lobeY = 0.30f;
            const float lobeR = 0.45f;

            const float halfWidth = 0.80f;  // 아래 삼각형
            const float topY = 0.32f;
            const float tipY = -0.92f;

            float left = lobeR - Mathf.Sqrt((x + lobeX) * (x + lobeX) + (y - lobeY) * (y - lobeY));
            float right = lobeR - Mathf.Sqrt((x - lobeX) * (x - lobeX) + (y - lobeY) * (y - lobeY));

            float triangle = Mathf.Min(
                Mathf.Min(Edge(x, y, -halfWidth, topY, 0f, tipY),
                          Edge(x, y, 0f, tipY, halfWidth, topY)),
                Edge(x, y, halfWidth, topY, -halfWidth, topY));

            return Falloff(Mathf.Max(Mathf.Max(left, right), triangle) * 14f);
        }

        /// <summary>선분 기준 부호 있는 거리. 반시계 방향으로 돌면 안쪽이 양수입니다.</summary>
        private static float Edge(float px, float py, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax;
            float ey = by - ay;
            float length = Mathf.Sqrt(ex * ex + ey * ey);
            if (length <= 0.0001f)
                return 0f;

            return (ex * (py - ay) - ey * (px - ax)) / length;
        }

        /// <summary>
        /// 꼭짓점이 <paramref name="points"/>개인 별. 각도에 따라 반지름이
        /// 바깥(0.95)과 안쪽(innerRatio) 사이를 오갑니다.
        ///
        /// 주기는 <c>cos(각도 × 꼭짓점 수)</c>여야 홀수 개에서도 맞습니다.
        /// 반 주기(× 0.5)를 쓰면 5각처럼 홀수일 때 좌우가 어긋난 덩어리가 됩니다.
        /// <paramref name="sharpness"/>가 클수록 끝이 뾰족해집니다 — 1에 가까우면
        /// 꽃잎처럼 뭉툭해서 별로 안 보입니다.
        /// </summary>
        private static float Polar(float x, float y, int points, float innerRatio,
            float sharpness, float rotation)
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            if (distance <= 0.0001f)
                return 1f;

            float angle = Mathf.Atan2(y, x);

            float wave = (Mathf.Cos(angle * points + rotation) + 1f) * 0.5f;
            float radius = Mathf.Lerp(innerRatio, 0.95f, Mathf.Pow(wave, sharpness));

            return Falloff((radius - distance) * 12f);
        }

        private static float Ring(float x, float y)
        {
            float distance = Mathf.Sqrt(x * x + y * y);

            const float centre = 0.68f;
            const float halfWidth = 0.18f;

            return Falloff((halfWidth - Mathf.Abs(distance - centre)) * 10f);
        }

        /// <summary>경계값을 0~1로 부드럽게 눕힙니다.</summary>
        private static float Falloff(float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
        }
    }
}
