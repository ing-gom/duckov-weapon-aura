using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Duckov.Utilities;
using HarmonyLib;
using UnityEngine;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// 게임 원본 총알(<see cref="Projectile"/>) 프리팹의 시각 요소 구조 덤프.
    ///
    /// "게임 기본 탄도 표시를 끄고 모드 잔상만 남겨 달라"는 요청을 구현하려면
    /// 원본 궤적이 정확히 무엇으로 그려지는지부터 알아야 합니다. 코드상으로는
    /// <c>Projectile.trail</c>(TrailRenderer 1개)과 <c>otherTrails</c>(추가 목록)만
    /// 보이지만, 프리팹에는 필드로 참조되지 않은 파티클·라인 렌더러가 더 붙어 있을 수
    /// 있습니다. 그래서 추측하지 않고 실제 계층을 그대로 찍습니다.
    ///
    /// 대상:
    /// 1) 지금 든 총의 <c>bulletPfb</c>
    /// 2) <c>GameplayDataSettings.Prefabs.DefaultBullet</c> (총이 프리팹을 안 들고 있을 때 쓰는 기본 총알)
    /// 3) 이번 판에서 실제로 발사된 적 있는 모든 총알 프리팹 (<c>BulletPool.pools</c> 키)
    /// </summary>
    public static class ProjectileTrailDiagnostics
    {
        private static readonly FieldInfo? TrailField = AccessTools.Field(typeof(Projectile), "trail");
        private static readonly FieldInfo? OtherTrailsField = AccessTools.Field(typeof(Projectile), "otherTrails");

        public static string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WeaponAura 원본 탄환 궤적 구조 덤프 ===");
            sb.AppendLine($"Projectile.trail 필드      : {(TrailField != null ? "찾음" : "없음(!)")}");
            sb.AppendLine($"Projectile.otherTrails 필드: {(OtherTrailsField != null ? "찾음" : "없음(!)")}");

            var seen = new List<Projectile>();

            try
            {
                foreach (var entry in CollectPrefabs())
                {
                    var prefab = entry.Value;
                    if (prefab == null)
                        continue;

                    if (seen.Contains(prefab))
                    {
                        sb.AppendLine();
                        sb.AppendLine($"[{entry.Key}] {prefab.name} — 위에서 이미 출력함");
                        continue;
                    }

                    seen.Add(prefab);
                    DumpPrefab(sb, entry.Key, prefab);
                }

                if (seen.Count == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("총알 프리팹을 하나도 찾지 못했습니다. (레벨에 들어가 한 발 쏜 뒤 다시 눌러 보세요)");
                }

                DumpModTrails(sb);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"덤프 중 오류: {ex}");
            }

            return Finish(sb);
        }

        // ── 대상 수집 ─────────────────────────────────────────────

        private static IEnumerable<KeyValuePair<string, Projectile?>> CollectPrefabs()
        {
            yield return Pair("든 총의 bulletPfb", HeldGunBullet());
            yield return Pair("DefaultBullet", SafeDefaultBullet());

            // 이번 판에서 실제로 쏜 프리팹들. 풀이 프리팹을 키로 잡고 있어서
            // 총 종류별 총알을 한 번에 훑을 수 있습니다.
            BulletPool? pool = null;
            try
            {
                pool = LevelManager.Instance != null ? LevelManager.Instance.BulletPool : null;
            }
            catch
            {
                // 레벨 밖
            }

            if (pool == null || pool.pools == null)
                yield break;

            int i = 0;
            foreach (var key in pool.pools.Keys)
                yield return Pair($"발사된 프리팹 #{i++}", key);
        }

        private static KeyValuePair<string, Projectile?> Pair(string label, Projectile? p)
            => new KeyValuePair<string, Projectile?>(label, p);

        private static Projectile? HeldGunBullet()
        {
            try
            {
                var player = CharacterMainControl.Main;
                var holder = player != null ? player.agentHolder : null;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;
                if (agent is ItemAgent_Gun gun && gun.GunItemSetting != null)
                    return gun.GunItemSetting.bulletPfb;
            }
            catch
            {
                // 로비 등 플레이어가 없는 씬
            }
            return null;
        }

        private static Projectile? SafeDefaultBullet()
        {
            try
            {
                var prefabs = GameplayDataSettings.Prefabs;
                return prefabs != null ? prefabs.DefaultBullet : null;
            }
            catch
            {
                return null;
            }
        }

        // ── 출력 ─────────────────────────────────────────────────

        private static void DumpPrefab(StringBuilder sb, string label, Projectile prefab)
        {
            sb.AppendLine();
            sb.AppendLine($"[{label}] {prefab.name}");

            var mainTrail = TrailField != null ? TrailField.GetValue(prefab) as TrailRenderer : null;
            var others = OtherTrailsField != null ? OtherTrailsField.GetValue(prefab) as List<TrailRenderer> : null;

            sb.AppendLine($"  trail       = {Describe(mainTrail)}");
            sb.AppendLine($"  otherTrails = {(others == null ? "null" : others.Count + "개")}");
            if (others != null)
            {
                for (int i = 0; i < others.Count; i++)
                    sb.AppendLine($"    [{i}] {Describe(others[i])}");
            }

            sb.AppendLine($"  mesh(MeshRenderer) = {(prefab.mesh != null ? prefab.mesh.gameObject.name : "null")}");
            sb.AppendLine($"  random={prefab.random} colorName={prefab.colorName} colors={(prefab.colors != null ? prefab.colors.Count : 0)}개");

            sb.AppendLine("  --- 계층 전체 ---");
            WalkHierarchy(sb, prefab.transform, 1, mainTrail, others);
        }

        /// <summary>계층을 그대로 훑으면서 화면에 뭔가를 그리는 컴포넌트를 찍습니다.</summary>
        private static void WalkHierarchy(
            StringBuilder sb, Transform node, int depth,
            TrailRenderer? mainTrail, List<TrailRenderer>? others)
        {
            string pad = new string(' ', depth * 2 + 2);
            sb.AppendLine($"{pad}{node.name}  (activeSelf={node.gameObject.activeSelf}, layer={LayerMask.LayerToName(node.gameObject.layer)})");

            foreach (var c in node.GetComponents<Component>())
            {
                if (c == null)
                {
                    sb.AppendLine($"{pad}  · (누락된 스크립트)");
                    continue;
                }

                if (c is Transform)
                    continue;

                if (c is TrailRenderer tr)
                {
                    string tag = ReferenceEquals(tr, mainTrail) ? " ★trail 필드"
                        : (others != null && others.Contains(tr)) ? " ★otherTrails 필드"
                        : " ※어느 필드도 참조 안 함";
                    sb.AppendLine($"{pad}  · TrailRenderer{tag} enabled={tr.enabled} emitting={tr.emitting}");
                    DumpTrailDetail(sb, pad + "      ", tr);
                }
                else if (c is LineRenderer lr)
                {
                    sb.AppendLine($"{pad}  · LineRenderer enabled={lr.enabled} positions={lr.positionCount} " +
                                  $"width={lr.widthMultiplier} shader={ShaderOf(lr)}");
                }
                else if (c is ParticleSystem ps)
                {
                    var main = ps.main;
                    sb.AppendLine($"{pad}  · ParticleSystem playOnAwake={main.playOnAwake} duration={main.duration} " +
                                  $"startLifetime={main.startLifetime.constant} emission={ps.emission.enabled} " +
                                  $"maxParticles={main.maxParticles}");
                }
                else if (c is Renderer r)
                {
                    sb.AppendLine($"{pad}  · {r.GetType().Name} enabled={r.enabled} shader={ShaderOf(r)}");
                }
                else if (c is Light light)
                {
                    sb.AppendLine($"{pad}  · Light enabled={light.enabled} range={light.range} color={light.color}");
                }
                else if (c is SodaPointLight point)
                {
                    // 총알을 따라다니는 가짜 점광원. 색·경도·감쇠가 전부 public 프로퍼티라
                    // 값만 읽어 두면 그대로 되돌릴 수 있습니다.
                    sb.AppendLine($"{pad}  · SodaPointLight enabled={point.enabled} " +
                                  $"lightColor={Fmt(point.LightColor)} hardness={point.Hardness} " +
                                  $"fallOff={point.FallOff} enviromentTint={point.enviromentTint}");
                    sb.AppendLine($"{pad}      lossyScale={point.transform.lossyScale} " +
                                  $"localScale={point.transform.localScale}");
                }
                else
                {
                    sb.AppendLine($"{pad}  · {c.GetType().Name}");
                }
            }

            for (int i = 0; i < node.childCount; i++)
                WalkHierarchy(sb, node.GetChild(i), depth + 1, mainTrail, others);
        }

        /// <summary>
        /// 모드가 만든 잔상·머리를 원본과 같은 형식으로 찍습니다.
        ///
        /// "원본이 더 잘 보인다"를 고치려면 두 쪽 값을 같은 자로 재서 비교해야 합니다.
        /// 굵기만 맞춰도 머티리얼 틴트나 합성 방식이 다르면 밝기가 딴판이 됩니다.
        /// </summary>
        private static void DumpModTrails(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("=== 모드가 만든 잔상 (비교용) ===");

            var holder = GameObject.Find("WeaponAura_BulletTrails");
            if (holder == null)
            {
                sb.AppendLine("아직 만들어지지 않았습니다. 한 발 쏜 뒤 다시 눌러 보세요.");
                return;
            }

            int shown = 0;

            foreach (var tr in holder.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (tr == null)
                    continue;

                // 풀에 든 잔상은 전부 같은 설정입니다. 하나씩만 봐도 충분합니다.
                bool isHead = tr.gameObject.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0;
                sb.AppendLine($"  [{(isHead ? "머리" : "꼬리")}] {tr.gameObject.name} " +
                              $"enabled={tr.enabled} emitting={tr.emitting}");
                DumpTrailDetail(sb, "      ", tr);

                if (++shown >= 2)
                    break;
            }

            if (shown == 0)
                sb.AppendLine("잔상 오브젝트가 아직 없습니다. 한 발 쏜 뒤 다시 눌러 보세요.");
        }

        /// <summary>
        /// TrailRenderer 하나를 그대로 재현하는 데 필요한 값을 전부 찍습니다.
        ///
        /// 굵기·지속시간만으로는 왜 원본이 더 또렷한지 알 수 없습니다. 실제 밝기는
        /// 머티리얼의 틴트 색과 셰이더 합성 방식이 정하고, 모양은 굵기 곡선과 텍스처가
        /// 정합니다. 그래서 그 값들까지 같이 남깁니다.
        /// </summary>
        private static void DumpTrailDetail(StringBuilder sb, string pad, TrailRenderer tr)
        {
            sb.AppendLine($"{pad}time={tr.time} widthMultiplier={tr.widthMultiplier} " +
                          $"start/endWidth={tr.startWidth}/{tr.endWidth}");
            sb.AppendLine($"{pad}color={Fmt(tr.startColor)} → {Fmt(tr.endColor)}");

            var widthCurve = tr.widthCurve;
            if (widthCurve != null && widthCurve.keys != null && widthCurve.keys.Length > 0)
            {
                var parts = new List<string>();
                foreach (var key in widthCurve.keys)
                    parts.Add($"({key.time:0.##}, {key.value:0.###})");
                sb.AppendLine($"{pad}widthCurve={string.Join(" ", parts)}");
            }

            var gradient = tr.colorGradient;
            if (gradient != null)
            {
                var colorParts = new List<string>();
                foreach (var key in gradient.colorKeys)
                    colorParts.Add($"({key.time:0.##}, {Fmt(key.color)})");

                var alphaParts = new List<string>();
                foreach (var key in gradient.alphaKeys)
                    alphaParts.Add($"({key.time:0.##}, {key.alpha:0.###})");

                sb.AppendLine($"{pad}gradient color={string.Join(" ", colorParts)}");
                sb.AppendLine($"{pad}gradient alpha={string.Join(" ", alphaParts)}");
            }

            sb.AppendLine($"{pad}alignment={tr.alignment} textureMode={tr.textureMode} " +
                          $"minVertexDistance={tr.minVertexDistance} " +
                          $"caps={tr.numCapVertices} corners={tr.numCornerVertices}");
            sb.AppendLine($"{pad}sortingLayer={tr.sortingLayerName} order={tr.sortingOrder}");

            DumpMaterial(sb, pad, tr.sharedMaterial);
            DumpPropertyBlock(sb, pad, tr);
        }

        private static void DumpMaterial(StringBuilder sb, string pad, Material? material)
        {
            if (material == null)
            {
                sb.AppendLine($"{pad}material=(없음)");
                return;
            }

            sb.AppendLine($"{pad}material={material.name} shader={(material.shader != null ? material.shader.name : "(없음)")} " +
                          $"queue={material.renderQueue}");

            var texture = material.mainTexture;
            sb.AppendLine($"{pad}  mainTexture={(texture != null ? $"{texture.name} {texture.width}x{texture.height}" : "(없음)")}");

            // 실제 밝기를 정하는 값들. 이름이 셰이더마다 달라서 후보를 모두 훑습니다.
            foreach (string name in new[] { "_TintColor", "_Color", "_BaseColor", "_EmissionColor" })
            {
                if (material.HasProperty(name))
                    sb.AppendLine($"{pad}  {name}={Fmt(material.GetColor(name))}");
            }

            foreach (string name in new[] { "_SrcBlend", "_DstBlend", "_ZWrite", "_Cull", "_Surface", "_Blend" })
            {
                if (material.HasProperty(name))
                    sb.AppendLine($"{pad}  {name}={material.GetFloat(name)}");
            }

            string[] keywords = material.shaderKeywords;
            if (keywords != null && keywords.Length > 0)
                sb.AppendLine($"{pad}  keywords={string.Join(", ", keywords)}");
        }

        /// <summary>
        /// 렌더러별로 덮어쓴 값. 머리 밝기는 머티리얼이 아니라 여기 들어가므로,
        /// 이걸 안 찍으면 발광이 실제로 걸렸는지 알 수 없습니다.
        /// </summary>
        private static void DumpPropertyBlock(StringBuilder sb, string pad, Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            if (block.isEmpty)
            {
                sb.AppendLine($"{pad}propertyBlock=(없음)");
                return;
            }

            sb.AppendLine($"{pad}propertyBlock _EmissionColor={Fmt(block.GetColor(EmissionColorId))}");
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>HDR 색은 채널이 1을 넘을 수 있어서 Color.ToString의 반올림으로는 판단이 안 됩니다.</summary>
        private static string Fmt(Color c)
            => $"({c.r:0.###}, {c.g:0.###}, {c.b:0.###}, a={c.a:0.###})";

        private static string Describe(TrailRenderer? tr)
        {
            if (tr == null)
                return "null";
            return $"{tr.gameObject.name} (enabled={tr.enabled}, time={tr.time}, width={tr.widthMultiplier})";
        }

        private static string ShaderOf(Renderer r)
        {
            var mat = r.sharedMaterial;
            return mat != null && mat.shader != null ? mat.shader.name : "(없음)";
        }

        private static string Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            UnityEngine.Debug.Log(text);
            return text;
        }
    }
}
