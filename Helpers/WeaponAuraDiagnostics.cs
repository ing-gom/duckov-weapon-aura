using System;
using System.Text;
using UnityEngine;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// 오라가 왜 그렇게 보이는지 판단하기 위한 실측 덤프.
    /// 추측 대신 실제 트랜스폼·스케일·바운즈·머티리얼 값을 로그로 남깁니다.
    /// </summary>
    public static class WeaponAuraDiagnostics
    {
        public static string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WeaponAura 진단 ===");

            try
            {
                var player = CharacterMainControl.Main;
                if (player == null) { sb.AppendLine("플레이어 없음"); return Finish(sb); }

                var holder = player.agentHolder;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;
                if (agent == null) { sb.AppendLine("들고 있는 무기 없음"); return Finish(sb); }

                sb.AppendLine($"무기: {WeaponHelper.GetDisplayName(agent.Item)} (TypeID {agent.Item?.TypeID})");
                sb.AppendLine($"등급(quality): {WeaponHelper.GetQuality(agent.Item)}");
                sb.AppendLine($"agent  : {Path(agent.transform)}");
                sb.AppendLine($"         localScale={agent.transform.localScale}  lossyScale={agent.transform.lossyScale}");
                sb.AppendLine($"         activeInHierarchy={agent.gameObject.activeInHierarchy}");

                sb.AppendLine("--- 렌더러 ---");
                var renderers = agent.GetComponentsInChildren<Renderer>(true);
                sb.AppendLine($"총 {renderers.Length}개");

                foreach (var r in renderers)
                {
                    if (r == null) continue;

                    string kind = r.GetType().Name;
                    Mesh? mesh = null;
                    if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                    else
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null) mesh = mf.sharedMesh;
                    }

                    sb.AppendLine($"  [{kind}] {r.gameObject.name}  enabled={r.enabled}");
                    sb.AppendLine($"     lossyScale={r.transform.lossyScale}");
                    sb.AppendLine($"     worldBounds.size={r.bounds.size}  center={r.bounds.center}");

                    if (mesh == null)
                    {
                        sb.AppendLine("     mesh=null");
                    }
                    else
                    {
                        sb.AppendLine($"     mesh={mesh.name} verts={mesh.vertexCount} readable={mesh.isReadable}");
                        sb.AppendLine($"     mesh.bounds.size={mesh.bounds.size}");
                    }

                    var mat = r.sharedMaterial;
                    sb.AppendLine($"     shader={(mat != null && mat.shader != null ? mat.shader.name : "(없음)")}");
                }

                sb.AppendLine("--- 생성된 오라 ---");
                var auraRoot = FindAuraRoot(agent.transform);
                if (auraRoot == null)
                {
                    sb.AppendLine("오라 루트 없음");
                }
                else
                {
                    sb.AppendLine($"root  : {Path(auraRoot)}");
                    sb.AppendLine($"         localScale={auraRoot.localScale}  lossyScale={auraRoot.lossyScale}");

                    foreach (var r in auraRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        sb.AppendLine($"  [{r.GetType().Name}] {r.gameObject.name}");
                        sb.AppendLine($"     worldBounds.size={r.bounds.size}");

                        if (r is ParticleSystemRenderer psr)
                        {
                            var ps = r.GetComponent<ParticleSystem>();
                            if (ps != null)
                            {
                                var main = ps.main;
                                sb.AppendLine($"     startSize={main.startSize.constant} scalingMode={main.scalingMode}");
                                sb.AppendLine($"     maxParticleSize={psr.maxParticleSize} renderMode={psr.renderMode}");
                                sb.AppendLine($"     alive={ps.particleCount}");
                            }
                        }

                        // 왜 안 보이는지 판단하는 데 필요한 값들
                        sb.AppendLine($"     enabled={r.enabled} active={r.gameObject.activeInHierarchy} " +
                                      $"isVisible={r.isVisible} layer={LayerMask.LayerToName(r.gameObject.layer)}({r.gameObject.layer})");
                        sb.AppendLine($"     scale={r.transform.lossyScale}");

                        var block = new MaterialPropertyBlock();
                        r.GetPropertyBlock(block);
                        if (!block.isEmpty)
                            sb.AppendLine($"     MPB _TintColor={block.GetColor(Shader.PropertyToID("_TintColor"))}");

                        var mat = r.sharedMaterial;
                        if (mat != null)
                        {
                            sb.AppendLine($"     shader={(mat.shader != null ? mat.shader.name : "(없음)")} queue={mat.renderQueue}");
                            sb.AppendLine($"     tex={(mat.mainTexture != null ? mat.mainTexture.name : "(없음)")}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"진단 중 오류: {ex.Message}");
            }

            return Finish(sb);
        }

        private static Transform? FindAuraRoot(Transform agentRoot)
        {
            foreach (var t in agentRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.StartsWith("WeaponAura_"))
                    return t;
            }
            return null;
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 12)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }

        private static string Finish(StringBuilder sb)
        {
            string text = sb.ToString();
            UnityEngine.Debug.Log(text);
            return text;
        }
    }
}
