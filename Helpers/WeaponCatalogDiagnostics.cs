using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ItemStatsSystem;
using UnityEngine;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// "무기 전체 목록"과 "안 가진 무기의 모델"이 실제로 되는지 확인하는 실측 덤프.
    ///
    /// 확인해야 하는 것이 둘입니다.
    ///  1. 열거 — ItemAssetsCollection의 네 경로 중 무엇이 실제로 무기를 돌려주는가
    ///  2. 모델 — 오라 미리보기는 지금 <b>손에 든</b> ItemAgent의 렌더러를 복제합니다.
    ///     안 가진 무기는 그 경로가 없으므로 프리팹에서 같은 품질의 메시가 나와야 합니다.
    ///     프리팹의 렌더러가 손에 든 것과 일치하는지를 같은 무기로 나란히 찍어서 비교합니다.
    /// </summary>
    public static class WeaponCatalogDiagnostics
    {
        /// <summary>프리팹 모델을 자세히 찍어 볼 무기 수 (총기·근접 각각)</summary>
        private const int SampleCount = 5;

        public static string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WeaponAura 무기 카탈로그 진단 ===");

            DumpEnumeration(sb);
            sb.AppendLine();
            DumpCatalog(sb);
            sb.AppendLine();
            DumpHeldWeaponComparison(sb);
            sb.AppendLine();
            DumpPrefabModels(sb);

            string text = sb.ToString();
            UnityEngine.Debug.Log(text);
            return text;
        }

        // ── 1. 열거 경로 ────────────────────────────────────────────

        private static void DumpEnumeration(StringBuilder sb)
        {
            sb.AppendLine("--- 열거 경로별 결과 ---");

            try
            {
                var collection = ItemAssetsCollection.Instance;
                sb.AppendLine($"Instance: {(collection == null ? "null" : "있음")}");
                if (collection != null)
                    sb.AppendLine($"NextTypeID: {collection.NextTypeID}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Instance 접근 실패: {ex.Message}");
            }

            foreach (WeaponCatalog.CatalogSource source in Enum.GetValues(typeof(WeaponCatalog.CatalogSource)))
            {
                if (source == WeaponCatalog.CatalogSource.None)
                    continue;

                int[] ids;
                try
                {
                    ids = WeaponCatalog.EnumerateTypeIds(source);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  {source,-18} 예외: {ex.Message}");
                    continue;
                }

                int guns = 0, melee = 0;
                foreach (int id in ids)
                {
                    if (WeaponHelper.IsGun(id)) guns++;
                    else if (WeaponHelper.IsMeleeWeapon(id)) melee++;
                }

                sb.AppendLine($"  {source,-18} 아이템 {ids.Length,5}개 → 총기 {guns,4} · 근접 {melee,4}");
            }
        }

        // ── 2. 카탈로그 구성 ────────────────────────────────────────

        private static void DumpCatalog(StringBuilder sb)
        {
            sb.AppendLine("--- 카탈로그 ---");

            WeaponCatalog.Invalidate();
            var all = WeaponCatalog.All;

            sb.AppendLine($"채택 경로: {WeaponCatalog.ResolvedSource}");
            sb.AppendLine($"무기 총계: {all.Count}");

            var byClass = new Dictionary<string, int>();
            int meleeCount = 0;

            foreach (var entry in all)
            {
                if (entry.Kind == WeaponKind.Melee)
                {
                    meleeCount++;
                    continue;
                }

                string key = entry.GunClass ?? "(분류없음)";
                byClass.TryGetValue(key, out int count);
                byClass[key] = count + 1;
            }

            sb.AppendLine($"근접무기: {meleeCount}개");
            sb.AppendLine($"총기 분류: {byClass.Count}종");

            foreach (var pair in byClass)
                sb.AppendLine($"    {pair.Key,-10} {pair.Value,4}개");

            // 아이콘이 실제로 들어 있는지 — 목록 UI를 아이콘 격자로 만들 수 있는지가 여기 달렸습니다.
            int withIcon = 0;
            foreach (var entry in all)
            {
                if (entry.Icon != null)
                    withIcon++;
            }

            sb.AppendLine($"아이콘 보유: {withIcon}/{all.Count}");
        }

        // ── 3. 든 무기: 프리팹 vs 실제 에이전트 ──────────────────────

        /// <summary>
        /// 같은 무기를 두 경로로 재서 나란히 놓습니다.
        ///
        /// 이게 이 진단의 핵심입니다. 프리팹 쪽 정점 수·바운즈가 에이전트 쪽과 같으면
        /// 안 가진 무기도 같은 미리보기를 만들 수 있다는 뜻이고, 다르면 프리팹에는
        /// 손에 드는 모델이 없다는 뜻입니다.
        /// </summary>
        private static void DumpHeldWeaponComparison(StringBuilder sb)
        {
            sb.AppendLine("--- 든 무기: 에이전트 vs 프리팹 ---");

            var player = CharacterMainControl.Main;
            var holder = player != null ? player.agentHolder : null;
            var agent = holder != null ? holder.CurrentHoldItemAgent : null;
            var item = agent != null ? agent.Item : null;

            if (agent == null || item == null)
            {
                sb.AppendLine("들고 있는 무기가 없습니다 — 무기를 든 상태로 다시 눌러 주세요.");
                return;
            }

            int typeId = item.TypeID;
            sb.AppendLine($"무기: {WeaponHelper.GetDisplayName(typeId)} (TypeID {typeId})");

            var fromAgent = Measure(agent.gameObject);
            sb.AppendLine($"  에이전트: {fromAgent}");

            Item? prefab = null;
            try
            {
                prefab = ItemAssetsCollection.GetPrefab(typeId);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  GetPrefab 예외: {ex.Message}");
            }

            if (prefab == null)
            {
                sb.AppendLine("  프리팹: null — 이 경로로는 모델을 못 얻습니다.");
            }
            else
            {
                sb.AppendLine($"  프리팹  : {Measure(prefab.gameObject)}");
                DumpAgentPrefabs(sb, prefab, "  ");
            }
        }

        // ── 4. 안 가진 무기 표본 ────────────────────────────────────

        private static void DumpPrefabModels(StringBuilder sb)
        {
            sb.AppendLine("--- 표본 무기의 프리팹 모델 ---");

            var guns = new List<WeaponCatalogEntry>();
            var melee = new List<WeaponCatalogEntry>();

            foreach (var entry in WeaponCatalog.All)
            {
                if (entry.Kind == WeaponKind.Gun && guns.Count < SampleCount)
                    guns.Add(entry);
                else if (entry.Kind == WeaponKind.Melee && melee.Count < SampleCount)
                    melee.Add(entry);

                if (guns.Count >= SampleCount && melee.Count >= SampleCount)
                    break;
            }

            DumpSample(sb, "총기", guns);
            DumpSample(sb, "근접", melee);

            // 표본만 보고 "된다"고 결론 내면 안 됩니다 — 전체에서 몇 개가 되는지 셉니다.
            int ok = 0, noPrefab = 0, noMesh = 0;

            foreach (var entry in WeaponCatalog.All)
            {
                Item? prefab;
                try
                {
                    prefab = ItemAssetsCollection.GetPrefab(entry.TypeId);
                }
                catch
                {
                    prefab = null;
                }

                if (prefab == null)
                {
                    noPrefab++;
                    continue;
                }

                // Item 프리팹 본체가 아니라 그 아래 ItemAgent 프리팹에 모델이 있습니다.
                if (Measure(prefab.gameObject).Vertices > 0 || AgentVertices(prefab) > 0)
                    ok++;
                else
                    noMesh++;
            }

            sb.AppendLine();
            sb.AppendLine($"전체 집계: 메시 있음 {ok} · 메시 없음 {noMesh} · 프리팹 없음 {noPrefab}");
        }

        private static void DumpSample(StringBuilder sb, string label, List<WeaponCatalogEntry> entries)
        {
            foreach (var entry in entries)
            {
                sb.AppendLine($"[{label}] {entry.Name} (TypeID {entry.TypeId}, 등급 {entry.Quality}, " +
                              $"분류 {entry.GunClass ?? "-"}, 아이콘 {(entry.Icon != null ? "O" : "X")})");

                Item? prefab;
                try
                {
                    prefab = ItemAssetsCollection.GetPrefab(entry.TypeId);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    GetPrefab 예외: {ex.Message}");
                    continue;
                }

                if (prefab == null)
                {
                    sb.AppendLine("    프리팹: null");
                    continue;
                }

                sb.AppendLine($"    프리팹: {Measure(prefab.gameObject)}");
                DumpAgentPrefabs(sb, prefab, "    ");
            }
        }

        /// <summary>
        /// Item 프리팹이 들고 있는 ItemAgent 프리팹들.
        ///
        /// 손에 드는 모델이 Item 본체가 아니라 이쪽에 붙어 있을 가능성이 있습니다.
        /// <c>agents</c>는 비공개 필드라 리플렉션으로 봅니다 — 진단 전용이므로 여기서만 씁니다.
        /// </summary>
        private static void DumpAgentPrefabs(StringBuilder sb, Item prefab, string indent)
        {
            object? utilities;
            try
            {
                utilities = prefab.AgentUtilities;
            }
            catch (Exception ex)
            {
                sb.AppendLine($"{indent}AgentUtilities 예외: {ex.Message}");
                return;
            }

            if (utilities == null)
            {
                sb.AppendLine($"{indent}AgentUtilities: null");
                return;
            }

            var field = utilities.GetType().GetField("agents",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field?.GetValue(utilities) is not System.Collections.IEnumerable agents)
            {
                sb.AppendLine($"{indent}agents 필드를 읽지 못했습니다.");
                return;
            }

            int index = 0;
            FieldInfo? keyField = null;
            FieldInfo? prefabField = null;

            foreach (var pair in agents)
            {
                index++;

                if (pair == null)
                {
                    sb.AppendLine($"{indent}agent[{index}]: null");
                    continue;
                }

                // 원소는 ItemAgent가 아니라 AgentKeyPair(key + agentPrefab) 래퍼입니다.
                // 타입 자체는 비공개 중첩 클래스지만 두 필드는 public이라 이름으로 꺼냅니다.
                var pairType = pair.GetType();
                keyField ??= pairType.GetField("key",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                prefabField ??= pairType.GetField("agentPrefab",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                string key = keyField?.GetValue(pair) as string ?? "?";
                var agentPrefab = prefabField?.GetValue(pair) as Component;

                if (agentPrefab == null)
                {
                    sb.AppendLine($"{indent}agent[{index}] key=\"{key}\" → agentPrefab: null");
                    continue;
                }

                sb.AppendLine($"{indent}agent[{index}] key=\"{key}\" {agentPrefab.GetType().Name} " +
                              $"({agentPrefab.gameObject.name}): {Measure(agentPrefab.gameObject)}");
            }

            if (index == 0)
                sb.AppendLine($"{indent}agents: 비어 있음");
        }

        /// <summary>이 Item의 ItemAgent 프리팹들에서 나오는 정점 총합 (집계용, 조용히 실패)</summary>
        private static int AgentVertices(Item prefab)
        {
            try
            {
                object? utilities = prefab.AgentUtilities;
                if (utilities == null)
                    return 0;

                var field = utilities.GetType().GetField("agents",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (field?.GetValue(utilities) is not System.Collections.IEnumerable agents)
                    return 0;

                int total = 0;
                FieldInfo? prefabField = null;

                foreach (var pair in agents)
                {
                    if (pair == null)
                        continue;

                    prefabField ??= pair.GetType().GetField("agentPrefab",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (prefabField?.GetValue(pair) is Component agentPrefab)
                        total += Measure(agentPrefab.gameObject).Vertices;
                }

                return total;
            }
            catch
            {
                return 0;
            }
        }

        // ── 측정 ────────────────────────────────────────────────────

        private readonly struct MeshMeasure
        {
            public readonly int Renderers;
            public readonly int Vertices;
            public readonly Vector3 Size;
            public readonly int Unreadable;

            public MeshMeasure(int renderers, int vertices, Vector3 size, int unreadable)
            {
                Renderers = renderers;
                Vertices = vertices;
                Size = size;
                Unreadable = unreadable;
            }

            public override string ToString()
            {
                if (Renderers == 0)
                    return "렌더러 없음";

                return $"렌더러 {Renderers}개 · 정점 {Vertices} · 메시바운즈 {Size} " +
                       $"· 읽기불가 {Unreadable}";
            }
        }

        /// <summary>
        /// 모델의 크기를 잽니다.
        ///
        /// 월드 바운즈(<c>renderer.bounds</c>)가 아니라 메시 바운즈를 씁니다 — 프리팹은
        /// 씬에 없어서 월드 바운즈가 의미 없는 값으로 나옵니다. 파티클·트레일은 무기
        /// 실루엣이 아니므로 셈에서 뺍니다.
        /// </summary>
        private static MeshMeasure Measure(GameObject root)
        {
            int renderers = 0, vertices = 0, unreadable = 0;
            bool hasBounds = false;
            Bounds bounds = default;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                    continue;

                Mesh? mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null)
                        mesh = filter.sharedMesh;
                }

                if (mesh == null || mesh.vertexCount <= 0)
                    continue;

                renderers++;
                vertices += mesh.vertexCount;

                if (!mesh.isReadable)
                    unreadable++;

                if (!hasBounds)
                {
                    bounds = mesh.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(mesh.bounds);
                }
            }

            return new MeshMeasure(renderers, vertices, hasBounds ? bounds.size : Vector3.zero, unreadable);
        }
    }
}
