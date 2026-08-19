using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using WeaponAura.Systems;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// 현재 들고 있는 무기의 메시를 OBJ 파일로 내보냅니다.
    /// 오라 튜닝 벤치(웹 대시보드)에 실제 총 모델을 띄워 미리보기 위한 용도입니다.
    ///
    /// 주의: Unity는 Read/Write가 꺼진 메시(<c>Mesh.isReadable == false</c>)의 정점 데이터에
    /// 접근할 수 없습니다. 그런 경우 내보내기는 실패하고, 사유를 그대로 돌려줍니다.
    /// </summary>
    public static class WeaponMeshExporter
    {
        private const string FolderName = "exported_meshes";

        /// <summary>내보내기 결과</summary>
        public struct Result
        {
            public bool success;
            public string message;
            public string path;
        }

        /// <summary>
        /// 지금 들고 있는 무기를 OBJ로 저장합니다.
        /// 여러 렌더러(본체 + 부착물)는 하나의 OBJ에 그룹으로 합칩니다.
        /// </summary>
        public static Result ExportHeldWeapon()
        {
            try
            {
                var player = CharacterMainControl.Main;
                var agent = player != null && player.agentHolder != null
                    ? player.agentHolder.CurrentHoldItemAgent
                    : null;

                if (agent == null || agent.Item == null)
                    return Fail("무기를 들고 있지 않습니다.");

                if (!WeaponHelper.IsWeapon(agent.Item))
                    return Fail("무기가 아닙니다.");

                var renderers = agent.GetComponentsInChildren<Renderer>(true);
                var parts = new List<(string name, Mesh mesh, Transform transform)>();
                int skippedUnreadable = 0;

                foreach (var renderer in renderers)
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

                    if (!mesh.isReadable)
                    {
                        skippedUnreadable++;
                        continue;
                    }

                    parts.Add((renderer.gameObject.name, mesh, renderer.transform));
                }

                if (parts.Count == 0)
                {
                    return Fail(skippedUnreadable > 0
                        ? $"메시 {skippedUnreadable}개가 모두 읽기 불가(Read/Write 꺼짐)입니다. 게임 내에서는 추출할 수 없습니다."
                        : "내보낼 메시를 찾지 못했습니다.");
                }

                string safeName = SanitizeFileName(GetWeaponName(agent.Item));
                string folder = GetExportFolder();
                if (folder == null)
                    return Fail("모드 폴더를 찾지 못했습니다.");

                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, safeName + ".obj");

                // 에이전트 로컬 공간 기준으로 저장 — 오라가 쓰는 좌표계와 동일하게 맞춥니다.
                File.WriteAllText(path, BuildObj(parts, agent.transform, safeName), new UTF8Encoding(false));

                string note = skippedUnreadable > 0 ? $" (읽기 불가 {skippedUnreadable}개 제외)" : "";
                return new Result
                {
                    success = true,
                    path = path,
                    message = $"저장됨: {path}{note}",
                };
            }
            catch (Exception ex)
            {
                return Fail($"내보내기 오류: {ex.Message}");
            }
        }

        private static string BuildObj(List<(string name, Mesh mesh, Transform transform)> parts, Transform root, string objectName)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;

            sb.AppendLine("# GunMaster weapon mesh export");
            sb.AppendLine($"# object: {objectName}");
            sb.AppendLine("# space: item agent local (Unity 좌표계, 단위=m)");
            sb.AppendLine();

            int vertexOffset = 1; // OBJ 인덱스는 1부터

            foreach (var (name, mesh, transform) in parts)
            {
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                bool hasNormals = normals != null && normals.Length == vertices.Length;

                sb.AppendLine($"g {SanitizeFileName(name)}");

                foreach (var v in vertices)
                {
                    Vector3 local = root.InverseTransformPoint(transform.TransformPoint(v));
                    sb.AppendLine($"v {local.x.ToString("0.######", ci)} {local.y.ToString("0.######", ci)} {local.z.ToString("0.######", ci)}");
                }

                if (hasNormals)
                {
                    foreach (var n in normals)
                    {
                        Vector3 world = transform.TransformDirection(n);
                        Vector3 local = root.InverseTransformDirection(world).normalized;
                        sb.AppendLine($"vn {local.x.ToString("0.####", ci)} {local.y.ToString("0.####", ci)} {local.z.ToString("0.####", ci)}");
                    }
                }

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var triangles = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        int a = triangles[i] + vertexOffset;
                        int b = triangles[i + 1] + vertexOffset;
                        int c = triangles[i + 2] + vertexOffset;

                        if (hasNormals)
                            sb.AppendLine($"f {a}//{a} {b}//{b} {c}//{c}");
                        else
                            sb.AppendLine($"f {a} {b} {c}");
                    }
                }

                sb.AppendLine();
                vertexOffset += vertices.Length;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 게임의 모든 무기 메시를 한 번에 내보냅니다.
        ///
        /// 아이템 프리팹 목록을 얻는 API가 게임 버전마다 다를 수 있어, ItemAssetsCollection의
        /// 정적 멤버를 리플렉션으로 훑어서 "Item을 돌려주는 것"을 찾습니다.
        /// 찾지 못하면 어떤 멤버가 있었는지 로그로 남겨서 다음 시도에 쓸 수 있게 합니다.
        /// </summary>
        public static Result ExportAllWeapons()
        {
            try
            {
                string? folder = GetExportFolder();
                if (folder == null)
                    return Fail("모드 폴더를 찾지 못했습니다.");

                var collectionType = FindType("ItemAssetsCollection");
                if (collectionType == null)
                    return Fail("ItemAssetsCollection 타입을 찾지 못했습니다.");

                var items = EnumerateAllItems(collectionType, out string discovery);
                if (items == null || items.Count == 0)
                {
                    UnityEngine.Debug.Log($"[WeaponAura] ItemAssetsCollection 멤버 목록:\n{discovery}");
                    return Fail("아이템 목록 API를 찾지 못했습니다. 멤버 목록을 로그로 남겼습니다.");
                }

                Directory.CreateDirectory(folder);

                int exported = 0, unreadable = 0, skipped = 0;
                var index = new List<string>();

                foreach (var item in items)
                {
                    if (item == null || !WeaponHelper.IsWeapon(item)) { skipped++; continue; }

                    var parts = CollectParts(item.gameObject, out int partUnreadable);
                    if (parts.Count == 0)
                    {
                        if (partUnreadable > 0) unreadable++;
                        continue;
                    }

                    string safeName = SanitizeFileName($"{item.TypeID}_{GetWeaponName(item)}");
                    string path = Path.Combine(folder, safeName + ".obj");
                    File.WriteAllText(path, BuildObj(parts, item.transform, safeName), new UTF8Encoding(false));

                    index.Add($"  {{ \"typeId\": {item.TypeID}, \"name\": \"{EscapeJson(GetWeaponName(item))}\", \"file\": \"{safeName}.obj\" }}");
                    exported++;
                }

                File.WriteAllText(
                    Path.Combine(folder, "index.json"),
                    "{\n \"weapons\": [\n" + string.Join(",\n", index) + "\n ]\n}\n",
                    new UTF8Encoding(false));

                if (exported == 0)
                {
                    return Fail(unreadable > 0
                        ? $"무기 {unreadable}개의 메시가 모두 읽기 불가(Read/Write 꺼짐)입니다."
                        : "내보낼 무기 메시를 찾지 못했습니다.");
                }

                return new Result
                {
                    success = true,
                    path = folder,
                    message = $"무기 {exported}개 내보냄 (읽기불가 {unreadable}, 비무기 {skipped}) → {folder}",
                };
            }
            catch (Exception ex)
            {
                return Fail($"전체 내보내기 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// ItemAssetsCollection의 정적 멤버 중 Item 컬렉션을 돌려주는 것을 찾아 반환합니다.
        /// </summary>
        private static List<ItemStatsSystem.Item>? EnumerateAllItems(Type collectionType, out string discovery)
        {
            var log = new StringBuilder();
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                      | System.Reflection.BindingFlags.Static;

            var candidates = new List<object?>();

            foreach (var prop in collectionType.GetProperties(flags))
            {
                log.AppendLine($"  prop {prop.Name} : {prop.PropertyType.Name}");
                if (prop.GetIndexParameters().Length > 0) continue;
                try { candidates.Add(prop.GetValue(null)); } catch { }
            }

            foreach (var field in collectionType.GetFields(flags))
            {
                log.AppendLine($"  field {field.Name} : {field.FieldType.Name}");
                try { candidates.Add(field.GetValue(null)); } catch { }
            }

            discovery = log.ToString();

            var result = new List<ItemStatsSystem.Item>();
            var seen = new HashSet<int>();

            foreach (var candidate in candidates)
            {
                if (candidate is System.Collections.IEnumerable seq && !(candidate is string))
                {
                    foreach (var entry in seq)
                    {
                        var item = AsItem(entry);
                        if (item != null && seen.Add(item.GetInstanceID()))
                            result.Add(item);
                    }
                }
            }

            return result;
        }

        /// <summary>컬렉션 원소에서 Item을 꺼냅니다 (Item 자체 / GameObject / KeyValuePair 값)</summary>
        private static ItemStatsSystem.Item? AsItem(object? entry)
        {
            if (entry == null) return null;

            if (entry is ItemStatsSystem.Item item) return item;
            if (entry is GameObject go) return go.GetComponent<ItemStatsSystem.Item>();
            if (entry is Component comp) return comp.GetComponent<ItemStatsSystem.Item>();

            var type = entry.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var valueProp = type.GetProperty("Value");
                if (valueProp != null)
                    return AsItem(valueProp.GetValue(entry));
            }

            return null;
        }

        /// <summary>루트 아래의 내보낼 수 있는 메시를 모읍니다.</summary>
        private static List<(string name, Mesh mesh, Transform transform)> CollectParts(GameObject root, out int unreadable)
        {
            var parts = new List<(string, Mesh, Transform)>();
            unreadable = 0;

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
                    if (filter != null) mesh = filter.sharedMesh;
                }

                if (mesh == null || mesh.vertexCount <= 0) continue;
                if (!mesh.isReadable) { unreadable++; continue; }

                parts.Add((renderer.gameObject.name, mesh, renderer.transform));
            }

            return parts;
        }

        /// <summary>로드된 어셈블리에서 타입을 이름으로 찾습니다 (결과 캐시).</summary>
        private static readonly Dictionary<string, Type?> _typeCache = new Dictionary<string, Type?>();

        private static Type? FindType(string name)
        {
            if (_typeCache.TryGetValue(name, out var cached))
                return cached;

            Type? found = null;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    found = assembly.GetType(name, false);
                    if (found != null)
                        break;
                }
            }
            catch
            {
                found = null;
            }

            _typeCache[name] = found;
            return found;
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>내보내기 폴더 (모드 루트/exported_meshes)</summary>
        public static string? GetExportFolder()
        {
            try
            {
                string? root = ProficiencySettingsRoot();
                if (string.IsNullOrEmpty(root))
                    return null;
                return Path.Combine(root!, FolderName);
            }
            catch
            {
                return null;
            }
        }

        private static string? ProficiencySettingsRoot()
        {
            string? folder = WeaponAuraResources.GetUserTextureFolder();
            if (string.IsNullOrEmpty(folder))
                return null;

            // .../assets/vfx_textures → 모드 루트
            var assets = Directory.GetParent(folder!);
            var root = assets?.Parent;
            return root?.FullName;
        }

        private static string GetWeaponName(ItemStatsSystem.Item item)
        {
            try
            {
                string name = item.DisplayName ?? "";
                if (string.IsNullOrEmpty(name))
                    name = item.name;
                return string.IsNullOrEmpty(name) ? "weapon" : name;
            }
            catch
            {
                return "weapon";
            }
        }

        private static string SanitizeFileName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == ' ' ? '_' : c);
            }
            string result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "weapon" : result;
        }

        private static Result Fail(string message)
        {
            return new Result { success = false, message = message, path = "" };
        }
    }
}
