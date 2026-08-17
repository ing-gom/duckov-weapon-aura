using System;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 게임이 만든 이펙트의 <b>형태는 그대로 두고</b> 색과 크기만 갈아 끼웁니다.
    ///
    /// 총구 화염과 근접 참격이 똑같은 문제를 풉니다 — 무기마다 다른 프리팹이라 안에
    /// 무엇이 들었는지 알 수 없고, 색이 나올 수 있는 곳을 전부 훑는 수밖에 없습니다.
    /// 파티클 시작색 · 수명 곡선 · 속도별 색 · 렌더러 머티리얼 · 라이트가 그 목록이고,
    /// 없는 것은 그냥 지나갑니다.
    /// </summary>
    public static class EffectTint
    {
        /// <summary>셰이더마다 색 프로퍼티 이름이 달라서 알려진 이름을 전부 시도합니다.</summary>
        public static readonly string[] ColorProperties =
        {
            "_TintColor", "_Color", "_BaseColor", "_Tint", "_MainColor", "_TintColor1",
        };

        /// <summary>
        /// <paramref name="target"/> 아래 전부를 <paramref name="inner"/>~<paramref name="outer"/>로 물들입니다.
        /// </summary>
        /// <param name="sizeScale">1이 아니면 로컬 스케일에 곱합니다.</param>
        public static void Apply(GameObject? target, Color inner, Color outer, float alpha, float sizeScale)
        {
            if (target == null)
                return;

            alpha = Mathf.Clamp01(alpha);

            float scale = Mathf.Max(0.05f, sizeScale);
            if (!Mathf.Approximately(scale, 1f))
                target.transform.localScale *= scale;

            foreach (var system in target.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system == null)
                    continue;

                var main = system.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    WithAlpha(inner, alpha), WithAlpha(outer, alpha));

                // 수명 곡선에 원본 색이 남아 있으면 우리 색과 곱해져서 탁해집니다.
                // 알파(페이드 모양)는 살리고 색만 흰색으로 눕혀 둡니다.
                var overLifetime = system.colorOverLifetime;
                if (overLifetime.enabled)
                    overLifetime.color = Whiten(overLifetime.color);

                // 속도에 따라 색을 바꾸는 모듈이 켜져 있으면 그쪽에도 원본 색이 남습니다.
                var bySpeed = system.colorBySpeed;
                if (bySpeed.enabled)
                    bySpeed.color = Whiten(bySpeed.color);
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                if (renderer is ParticleSystemRenderer)
                {
                    // 파티클의 최종 색은 "정점 색 × 머티리얼 색"입니다. 정점 색에 우리 색을
                    // 넣어도 머티리얼에 원래 색이 박혀 있으면 그게 곱해져서 결국 그 색으로
                    // 보입니다. 머티리얼 쪽은 흰색으로 눕혀서 정점 색만 남깁니다.
                    TintRenderer(renderer, Color.white, 1f, emission: false);
                    continue;
                }

                // 꼬리·선은 정점 색을 컴포넌트가 직접 들고 있습니다. 머티리얼 프로퍼티만
                // 건드리면 그 정점 색이 그대로 곱해져서 원본 색이 남습니다
                // (참격이 꼬리로 그려져 있으면 여기가 아니면 손댈 곳이 없습니다).
                if (renderer is TrailRenderer trail)
                {
                    trail.colorGradient = BuildGradient(inner, outer, alpha);
                    TintRenderer(renderer, Color.white, 1f, emission: false);
                    continue;
                }

                if (renderer is LineRenderer line)
                {
                    line.colorGradient = BuildGradient(inner, outer, alpha);
                    TintRenderer(renderer, Color.white, 1f, emission: false);
                    continue;
                }

                // 메시·빌보드는 정점 색이 없으므로 머티리얼에 직접 색을 넣습니다.
                TintRenderer(renderer, inner, alpha, emission: true);
            }

            foreach (var light in target.GetComponentsInChildren<Light>(true))
            {
                if (light != null)
                    light.color = outer;
            }
        }

        /// <summary>
        /// <b>한 가지 색</b>으로 확실하게 물들입니다.
        ///
        /// <see cref="Apply"/>는 알갱이가 여럿인 이펙트를 전제로 두 색 사이를 무작위로
        /// 섞고, 그 색을 파티클 정점 색에 싣습니다. 알갱이가 <b>한 장</b>뿐인 이펙트
        /// (근접 참격이 그렇습니다 — 호 한 장이 전부입니다)에서는 그 방식이 통하지 않습니다.
        /// 무작위 하나를 뽑는 것이라 안쪽 색이 흰빛이면 대부분 흰색이 나옵니다.
        ///
        /// 그래서 여기서는 반대로 갑니다 — 정점 색을 흰색으로 눕히고 <b>머티리얼</b>에 색을
        /// 싣습니다. 최종 색은 정점 색 × 머티리얼 색이므로 흰색 × 우리 색 = 우리 색으로
        /// 딱 떨어집니다. 무작위가 끼어들 자리가 없습니다.
        /// </summary>
        /// <remarks>
        /// <see cref="Apply"/>와 달리 <b>크기를 건드리지 않습니다.</b> 근접 참격이 이 길을 쓰는데,
        /// 참격 호의 크기는 그 무기의 공격 사거리를 그대로 보여 주는 정보입니다. 색을 바꾼다고
        /// 크기까지 늘리면 실제 닿는 거리와 화면이 어긋나서 플레이어를 속이게 됩니다.
        /// </remarks>
        public static void ApplySolid(GameObject? target, Color color, float alpha)
        {
            if (target == null)
                return;

            alpha = Mathf.Clamp01(alpha);

            foreach (var system in target.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system == null)
                    continue;

                // 정점 색은 흰색. 알파는 1로 두고 머티리얼 쪽 알파만 쓰게 합니다.
                var main = system.main;
                main.startColor = Color.white;

                // 원본 색이 곡선에 남아 있으면 우리 색과 곱해져 탁해집니다.
                // 알파(페이드 모양)는 살리고 색만 눕힙니다.
                var overLifetime = system.colorOverLifetime;
                if (overLifetime.enabled)
                    overLifetime.color = Whiten(overLifetime.color);

                var bySpeed = system.colorBySpeed;
                if (bySpeed.enabled)
                    bySpeed.color = Whiten(bySpeed.color);
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                if (renderer is TrailRenderer trail)
                {
                    trail.colorGradient = BuildGradient(color, color, alpha);
                    continue;
                }

                if (renderer is LineRenderer line)
                {
                    line.colorGradient = BuildGradient(color, color, alpha);
                    continue;
                }

                // 파티클이든 메시든 머티리얼에 색을 싣습니다.
                // 발광 색까지 같이 넣지 않으면 원본의 흰 발광이 남아 하얗게 떠오릅니다.
                TintRenderer(renderer, color, alpha, emission: true);
            }

            foreach (var light in target.GetComponentsInChildren<Light>(true))
            {
                if (light != null)
                    light.color = color;
            }
        }

        public static void TintRenderer(Renderer renderer, Color color, float alpha, bool emission)
        {
            var material = renderer.sharedMaterial;
            if (material == null)
                return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            var tinted = new Color(color.r, color.g, color.b, alpha);
            bool touched = false;

            foreach (string property in ColorProperties)
            {
                if (!material.HasProperty(property))
                    continue;

                block.SetColor(property, tinted);
                touched = true;
            }

            // 파티클에 발광 색까지 흰색으로 밀면 오히려 하얗게 떠오릅니다.
            if (emission && material.HasProperty("_EmissionColor"))
            {
                block.SetColor("_EmissionColor", tinted);
                touched = true;
            }

            // 알려진 이름이 하나도 안 걸리면 셰이더가 쓰는 색 프로퍼티를 직접 물어봅니다.
            //
            // 이름을 추측하는 목록만으로는 커스텀 셰이더를 못 잡습니다 — 그리고 못 잡으면
            // "색이 안 바뀐다"로만 보이지, 이름이 달랐다는 사실은 드러나지 않습니다.
            // 하나라도 걸렸을 때는 전부 훑지 않습니다: _SpecColor 같은 곁가지까지 밀면
            // 오히려 원본과 다른 재질로 보입니다.
            if (!touched)
            {
                foreach (string property in ColorPropertyNames(material))
                {
                    block.SetColor(property, tinted);
                    touched = true;
                }
            }

            if (touched)
                renderer.SetPropertyBlock(block);
        }

        /// <summary>이 머티리얼의 셰이더가 실제로 들고 있는 색(Color) 프로퍼티 이름들.</summary>
        public static string[] ColorPropertyNames(Material? material)
        {
            if (material == null || material.shader == null)
                return Array.Empty<string>();

            try
            {
                var shader = material.shader;
                var found = new System.Collections.Generic.List<string>();

                int count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color)
                        continue;

                    found.Add(shader.GetPropertyName(i));
                }

                return found.ToArray();
            }
            catch
            {
                // 셰이더 리플렉션은 런타임 버전을 탑니다. 못 읽으면 이름 추측만으로 갑니다.
                return Array.Empty<string>();
            }
        }

        /// <summary>꼬리·선용 그라디언트: 안쪽 색에서 바깥 색으로 번지며 사라집니다.</summary>
        private static Gradient BuildGradient(Color inner, Color outer, float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(inner, 0f),
                    new GradientColorKey(outer, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(Mathf.Clamp01(alpha), 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        /// <summary>색은 흰색으로, 알파(페이드 모양)는 그대로 두고 돌려줍니다.</summary>
        public static ParticleSystem.MinMaxGradient Whiten(ParticleSystem.MinMaxGradient source)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(WhiteWithAlpha(source.color));

                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        WhiteWithAlpha(source.colorMin), WhiteWithAlpha(source.colorMax));

                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(WhitenGradient(source.gradient));

                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        WhitenGradient(source.gradientMin), WhitenGradient(source.gradientMax));

                default:
                    return source;
            }
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private static Color WhiteWithAlpha(Color color)
        {
            return new Color(1f, 1f, 1f, color.a);
        }

        /// <summary>
        /// 이 이펙트가 실제로 무엇으로 만들어져 있는지 한 줄로 적어 줍니다.
        ///
        /// 색이 안 바뀔 때 원인은 거의 항상 "우리가 찾은 곳에 색이 없다"입니다.
        /// 렌더러 종류·셰이더·잡을 수 있는 색 프로퍼티를 적어 두면 그 자리에서 갈립니다.
        /// </summary>
        public static string Describe(GameObject? target)
        {
            if (target == null)
                return "(없음)";

            var particles = target.GetComponentsInChildren<ParticleSystem>(true);
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var animators = target.GetComponentsInChildren<Animator>(true);
            var lights = target.GetComponentsInChildren<Light>(true);

            Bounds bounds = default;
            bool hasBounds = false;

            var detail = new System.Text.StringBuilder();

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                var material = renderer.sharedMaterial;
                string shader = material != null && material.shader != null
                    ? material.shader.name : "(머티리얼 없음)";

                // 색을 넣을 구멍이 하나도 없으면 프로퍼티 블록으로는 손댈 수 없습니다.
                // 그때는 색이 텍스처에 구워져 있다는 뜻이라, 원인 파악에 이 목록이 필요합니다.
                var known = new System.Collections.Generic.List<string>();
                if (material != null)
                {
                    foreach (string property in ColorProperties)
                    {
                        if (material.HasProperty(property))
                            known.Add(property);
                    }

                    if (material.HasProperty("_EmissionColor"))
                        known.Add("_EmissionColor");
                }

                string shaderColors = string.Join("+", ColorPropertyNames(material));

                detail.Append($" | {renderer.GetType().Name} '{renderer.gameObject.name}' " +
                              $"셰이더={shader} 아는색속성=" +
                              (known.Count > 0 ? string.Join("+", known) : "없음") +
                              $" 셰이더색속성=" + (shaderColors.Length > 0 ? shaderColors : "없음"));
            }

            return $"파티클={particles.Length} 렌더러={renderers.Length} " +
                   $"애니메이터={animators.Length} 라이트={lights.Length} " +
                   $"월드크기={(hasBounds ? bounds.size.ToString("0.###") : "없음")}" + detail;
        }

        private static Gradient WhitenGradient(Gradient source)
        {
            var result = new Gradient();

            if (source == null)
                return result;

            var colorKeys = source.colorKeys;
            for (int i = 0; i < colorKeys.Length; i++)
                colorKeys[i].color = Color.white;

            result.SetKeys(colorKeys, source.alphaKeys);
            result.mode = source.mode;
            return result;
        }
    }
}
