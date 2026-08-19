using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WeaponAura.Helpers
{
    /// <summary>무기에 원래 붙어 있는 연출에서 뽑아낸 색 한 쌍</summary>
    public readonly struct WeaponAttributeColors
    {
        public readonly bool Found;
        public readonly Color Primary;
        public readonly Color Secondary;

        /// <summary>어디서 뽑았는지 (로그·진단용)</summary>
        public readonly string Source;

        public WeaponAttributeColors(Color primary, Color secondary, string source)
        {
            Found = true;
            Primary = primary;
            Secondary = secondary;
            Source = source;
        }
    }

    /// <summary>
    /// 속성 무기(불꽃 AK·프로스트 SMG 등)에 원래 붙어 있는 연출에서 색을 읽습니다.
    ///
    /// 이런 무기는 게임이 이미 점광원과 파티클로 속성을 표현하고 있습니다. 전용 설정을
    /// 만들 때 등급 색(신화=붉은색)으로 시작하면, 파란 프로스트 총에 붉은 오라가 붙은 채로
    /// 시작합니다. 사용자가 원하는 첫 화면이 아닙니다.
    ///
    /// <b>색만 가져옵니다.</b> 겹 수·뻗는 거리·물결·링 같은 값은 우리 오라만의 개념이라
    /// 원본에 대응하는 것이 없습니다. 억지로 끌어다 맞추면 숫자는 채워지지만 원본과
    /// 닮지도 않은 근거 없는 값이 됩니다. 그건 기본값이 아니라 그냥 무작위입니다.
    ///
    /// 점광원을 먼저 봅니다 — 속성 색이 가장 또렷하게 들어 있고 한 값으로 정해져 있습니다.
    /// 파티클은 그라디언트라 대표색을 골라야 해서 덜 정확합니다.
    /// </summary>
    public static class WeaponAttributeEffect
    {
        /// <summary>
        /// 흰색에 가까운 빛은 속성이 아니라 그냥 조명입니다.
        ///
        /// 일반 총에도 <c>SodaPointLight</c>가 붙어 있습니다(총구 주변을 밝히는 용도).
        /// 그것까지 속성으로 보면 모든 무기가 "속성 있음"이 되어 버립니다. 채도로 가릅니다.
        /// </summary>
        private const float MinSaturation = 0.25f;

        /// <summary>너무 어두운 색은 시작값으로 쓸 수 없습니다 (오라가 안 보입니다).</summary>
        private const float MinValue = 0.2f;

        /// <summary>두 색을 "서로 다른 색"으로 볼 최소 색상환 거리 (0~1).</summary>
        private const float DistinctHue = 0.08f;

        /// <summary>
        /// 점광원 색을 그대로 쓸 수 없습니다 — <b>HDR 값</b>입니다.
        ///
        /// 실측(불꽃 AK-47): <c>RGBA(13.766, 5.858, 3.442, 0.000)</c>.
        /// RGB가 1을 한참 넘고 알파는 0입니다. 빛의 세기를 색에 담는 방식이라 그렇습니다.
        /// 이걸 그대로 오라 색에 넣으면 알파 0이라 아무것도 안 보이고, 설령 보이더라도
        /// 하얗게 타 버립니다. 색조·채도만 살리고 밝기는 우리 범위로 되돌립니다.
        /// </summary>
        private static Color Normalize(Color raw)
        {
            float max = Mathf.Max(raw.r, Mathf.Max(raw.g, raw.b));

            Color rgb = max > 1f
                ? new Color(raw.r / max, raw.g / max, raw.b / max)
                : raw;

            // 알파는 무조건 1입니다. 점광원의 알파는 색이 아니라 다른 뜻으로 쓰입니다.
            return new Color(rgb.r, rgb.g, rgb.b, 1f);
        }

        /// <summary>지금 든 무기에서 속성 색을 읽습니다.</summary>
        public static WeaponAttributeColors FromHeldWeapon()
        {
            try
            {
                var player = CharacterMainControl.Main;
                var holder = player != null ? player.agentHolder : null;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;

                return agent == null ? default : FromRoot(agent.transform);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// 카탈로그의 무기에서 속성 색을 읽습니다 — 손에 없어도 됩니다.
        ///
        /// 모델을 한 번 만들어 보고 지웁니다. 실측으로 한 정당 0.1ms라 전용 설정을 만드는
        /// 순간에 한 번 하는 정도는 체감되지 않습니다.
        /// </summary>
        public static WeaponAttributeColors FromCatalog(int typeId)
        {
            if (typeId <= 0)
                return default;

            var host = new GameObject("WeaponAura_AttributeProbe");
            host.transform.position = new Vector3(0f, -2000f, 0f);

            try
            {
                var handle = WeaponModelSource.Create(typeId, host.transform);
                if (handle == null)
                    return default;

                try
                {
                    return FromRoot(handle.Model.transform);
                }
                finally
                {
                    handle.Dispose();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 속성 색 확인 실패(TypeID {typeId}): {ex.Message}");
                return default;
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        /// <summary>계층 아래에서 속성 색을 찾습니다.</summary>
        public static WeaponAttributeColors FromRoot(Transform? root)
        {
            if (root == null)
                return default;

            var lights = new List<Color>();

            foreach (var light in root.GetComponentsInChildren<SodaPointLight>(true))
            {
                if (light == null)
                    continue;

                Color color;
                try
                {
                    color = Normalize(light.LightColor);
                }
                catch
                {
                    continue;
                }

                if (IsAttributeColor(color))
                    lights.Add(color);
            }

            if (lights.Count > 0)
            {
                // 여러 개면 가장 진한 것을 주색으로. 불꽃 AK처럼 점광원이 둘인 경우
                // 하나는 일반 조명, 하나가 속성입니다.
                lights.Sort((a, b) => Saturation(b).CompareTo(Saturation(a)));

                Color primary = lights[0];

                // 두 번째 빛은 색이 <b>확실히 다를 때만</b> 바깥쪽 색으로 씁니다.
                // 불꽃 AK는 점광원이 둘인데 둘 다 같은 주황(H0.04)이라, 그대로 쓰면
                // 안팎이 같은 색이 되어 층이 죽습니다. 그때는 밝은 짝을 만들어 씁니다.
                Color secondary = lights.Count > 1 && HueDistance(primary, lights[1]) >= DistinctHue
                    ? lights[1]
                    : Lighten(primary);

                return new WeaponAttributeColors(primary, secondary, "점광원");
            }

            // 점광원에서 못 찾으면 무기에 붙은 파티클의 시작색을 봅니다.
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles == null || IsOurs(particles.transform))
                    continue;

                Color color;
                try
                {
                    color = Normalize(particles.main.startColor.color);
                }
                catch
                {
                    continue;
                }

                if (IsAttributeColor(color))
                    return new WeaponAttributeColors(color, Lighten(color), $"파티클({particles.gameObject.name})");
            }

            return default;
        }

        /// <summary>이 오브젝트(또는 조상)가 우리가 만든 것인지.</summary>
        internal static bool IsOurs(Transform? node)
        {
            for (var t = node; t != null; t = t.parent)
            {
                if (t.gameObject.name.StartsWith("WeaponAura_", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>속성 색이라고 볼 만한지 — 충분히 진하고 충분히 밝은지.</summary>
        private static bool IsAttributeColor(Color color)
        {
            Color.RGBToHSV(color, out _, out float s, out float v);
            return s >= MinSaturation && v >= MinValue;
        }

        /// <summary>색상환에서 두 색이 얼마나 떨어져 있는지 (0~0.5).</summary>
        private static float HueDistance(Color a, Color b)
        {
            Color.RGBToHSV(a, out float ha, out _, out _);
            Color.RGBToHSV(b, out float hb, out _, out _);

            float d = Mathf.Abs(ha - hb);
            return Mathf.Min(d, 1f - d);
        }

        private static float Saturation(Color color)
        {
            Color.RGBToHSV(color, out _, out float s, out _);
            return s;
        }

        /// <summary>바깥쪽 색이 없을 때 쓸 밝은 짝. 같은 색 두 개보다 층이 살아납니다.</summary>
        private static Color Lighten(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 0.6f), Mathf.Clamp01(v * 1.15f + 0.1f));
        }

        /// <summary>
        /// 카탈로그 전체를 훑어 <b>어떤 파티클이 무기마다 다른지</b> 가려냅니다.
        ///
        /// "불꽃 AK의 불티를 다른 총에 옮길 수 있나"는 결국 "그 불티가 그 총만의 것인가"에
        /// 달려 있습니다. 한 자루만 봐서는 알 수 없습니다 — Spark·Smoke가 모든 총에 있으면
        /// 그건 발사 연출이고 옮겨봐야 이미 있는 것이 하나 더 생길 뿐입니다.
        ///
        /// 그래서 전 무기의 파티클 이름을 모아 <b>거의 모든 무기에 있는 것(공통)</b>과
        /// <b>일부에만 있는 것(고유)</b>으로 가릅니다. 고유 쪽이 옮길 가치가 있는 후보입니다.
        /// </summary>
        public static string SurveyCatalog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WeaponAura 무기 파티클 조사 (카탈로그 전체) ===");

            var all = WeaponCatalog.All;
            if (all.Count == 0)
            {
                sb.AppendLine("카탈로그가 비어 있습니다.");
                return sb.ToString();
            }

            var host = new GameObject("WeaponAura_ParticleSurvey");
            host.transform.position = new Vector3(0f, -2000f, 0f);

            // 파티클 이름 → 그 이름을 가진 무기 수
            var counts = new Dictionary<string, int>();

            // 무기 → 그 무기가 가진 파티클 이름들 (고유 후보를 되짚기 위한 것)
            var perWeapon = new Dictionary<string, List<string>>();

            int scanned = 0;

            try
            {
                foreach (var entry in all)
                {
                    var handle = WeaponModelSource.Create(entry.TypeId, host.transform);
                    if (handle == null)
                        continue;

                    try
                    {
                        var names = new List<string>();

                        foreach (var particles in handle.Model.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            if (particles == null || IsOurs(particles.transform))
                                continue;

                            string name = particles.gameObject.name;
                            if (names.Contains(name))
                                continue;

                            names.Add(name);
                            counts.TryGetValue(name, out int c);
                            counts[name] = c + 1;
                        }

                        perWeapon[entry.Name] = names;
                        scanned++;
                    }
                    finally
                    {
                        handle.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"조사 중 오류: {ex.Message}");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }

            sb.AppendLine($"조사한 무기: {scanned}정");
            sb.AppendLine();
            sb.AppendLine("--- 파티클 이름별 보유 무기 수 ---");

            var ordered = new List<KeyValuePair<string, int>>(counts);
            ordered.Sort((a, b) => b.Value.CompareTo(a.Value));

            foreach (var pair in ordered)
            {
                // 절반 넘게 갖고 있으면 공통 연출로 봅니다.
                string kind = pair.Value * 2 >= scanned ? "공통" : "고유 후보";
                sb.AppendLine($"  {pair.Key,-28} {pair.Value,4}정  [{kind}]");
            }

            sb.AppendLine();
            sb.AppendLine("--- 고유 파티클을 가진 무기 ---");

            int listed = 0;
            foreach (var pair in perWeapon)
            {
                var unique = new List<string>();
                foreach (string name in pair.Value)
                {
                    if (counts.TryGetValue(name, out int c) && c * 2 < scanned)
                        unique.Add(name);
                }

                if (unique.Count == 0)
                    continue;

                if (listed++ >= 25)
                {
                    sb.AppendLine("  … (이하 생략)");
                    break;
                }

                sb.AppendLine($"  {pair.Key}: {string.Join(", ", unique)}");
            }

            if (listed == 0)
                sb.AppendLine("  없습니다 — 모든 무기가 같은 파티클만 갖고 있습니다.");

            string text = sb.ToString();
            UnityEngine.Debug.Log(text);
            return text;
        }

        /// <summary>진단용 — 이 무기에 어떤 연출이 붙어 있는지 전부 찍습니다.</summary>
        public static string Describe(Transform? root, string label)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== WeaponAura 무기 내장 연출: {label} ===");

            if (root == null)
            {
                sb.AppendLine("대상이 없습니다.");
                return sb.ToString();
            }

            sb.AppendLine("--- 점광원 ---");
            foreach (var light in root.GetComponentsInChildren<SodaPointLight>(true))
            {
                if (light == null)
                    continue;

                try
                {
                    var color = light.LightColor;
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    sb.AppendLine($"  {light.gameObject.name}: {color} (H{h:F2} S{s:F2} V{v:F2}) " +
                                  $"→ 속성색={(IsAttributeColor(color) ? "O" : "X")}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  {light.gameObject.name}: 읽기 실패 {ex.Message}");
                }
            }

            sb.AppendLine("--- 파티클 ---");
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles == null)
                    continue;

                // 우리 것을 원본으로 착각하면 안 됩니다. 이름 접두사만 보면 놓칩니다 —
                // 오라의 링은 그냥 "Ring"이고 접두사는 부모에 있습니다.
                if (IsOurs(particles.transform))
                    continue;

                try
                {
                    var main = particles.main;
                    sb.AppendLine($"  {particles.gameObject.name}: startColor={main.startColor.color} " +
                                  $"mode={main.startColor.mode} playing={particles.isPlaying} " +
                                  $"playOnAwake={main.playOnAwake}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  {particles.gameObject.name}: 읽기 실패 {ex.Message}");
                }
            }

            var found = FromRoot(root);
            sb.AppendLine(found.Found
                ? $"→ 채택: {found.Primary} / {found.Secondary} (출처 {found.Source})"
                : "→ 속성 색을 찾지 못했습니다 (일반 무기)");

            return sb.ToString();
        }
    }
}
