using System;
using ItemStatsSystem;
using UnityEngine;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// 만들어진 무기 모델 한 벌. 다 쓰면 <see cref="Dispose"/>로 통째로 지웁니다.
    /// </summary>
    public sealed class WeaponModelHandle : IDisposable
    {
        /// <summary>임시로 만든 모든 것의 부모. 이것만 지우면 전부 정리됩니다.</summary>
        public GameObject Root = null!;

        /// <summary>렌더러가 달린 실제 모델 루트</summary>
        public GameObject Model = null!;

        /// <summary>
        /// 진짜 무기 모델이 아니라 게임의 자리표시자(TestGunItemGraphic)인지.
        ///
        /// 등급 999 같은 특수·제작 무기는 전용 그래픽이 없어서 큐브 뭉치가 나옵니다.
        /// 그걸 무기 실루엣이라고 믿고 오라를 씌우면 네모난 덩어리가 빛납니다.
        /// </summary>
        public bool IsPlaceholder;

        /// <summary>어느 경로로 얻었는지 (로그·진단용)</summary>
        public string Source = "-";

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root);
                Root = null!;
            }

            Model = null!;
        }
    }

    /// <summary>
    /// 무기 TypeID 하나로 미리보기용 모델을 만듭니다 — <b>손에 없는 무기도</b>.
    ///
    /// 실측으로 확인된 사실(게임 1.x 기준):
    ///  - <c>ItemAssetsCollection.GetPrefab(typeId)</c>의 Item 프리팹에는 렌더러가 없습니다.
    ///    데이터 껍데기입니다.
    ///  - 근접무기 48정은 <c>AgentUtilities</c>에 key="Handheld" ItemAgent 프리팹을 들고
    ///    있고, 거기에 모델이 있습니다. 인스턴스를 만들 필요가 없어 가장 쌉니다.
    ///  - 총기 94정 중 93정은 그 목록이 비어 있습니다. 대신 게임의 팩토리
    ///    <c>ItemGraphicInfo.CreateAGraphic</c>가 부착물 소켓까지 포함해 조립해 줍니다.
    ///    이 경로는 <b>동기</b>로 완성됩니다(1차·2차 측정이 동일했습니다).
    ///
    /// 그래서 싼 경로를 먼저 시도하고 안 되면 팩토리로 넘어갑니다. 무기 종류로 분기하지
    /// 않는 이유 — 어느 쪽이든 "렌더러가 실제로 나왔는가"만 보면 되고, 게임이 바뀌어
    /// 총기가 에이전트를 갖게 되어도 코드가 그대로 맞습니다.
    /// </summary>
    public static class WeaponModelSource
    {
        /// <summary>자리표시자 그래픽의 이름. 게임이 전용 모델이 없을 때 쓰는 것입니다.</summary>
        private const string PlaceholderName = "TestGunItemGraphic";

        /// <summary>
        /// 무기 모델을 만듭니다. 실패하면 null.
        /// </summary>
        /// <param name="typeId">무기 TypeID</param>
        /// <param name="parent">붙일 부모 (null이면 씬 루트)</param>
        public static WeaponModelHandle? Create(int typeId, Transform? parent)
        {
            if (typeId <= 0)
                return null;

            var root = new GameObject($"WeaponModel_{typeId}");

            if (parent != null)
                root.transform.SetParent(parent, false);

            try
            {
                var handle = FromAgentPrefab(typeId, root) ?? FromGraphicFactory(typeId, root);

                if (handle == null)
                {
                    UnityEngine.Object.Destroy(root);
                    return null;
                }

                return handle;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 무기 모델 생성 실패(TypeID {typeId}): {ex.Message}");
                UnityEngine.Object.Destroy(root);
                return null;
            }
        }

        /// <summary>
        /// 싼 경로 — Item 프리팹이 들고 있는 key="Handheld" ItemAgent 프리팹을 복제합니다.
        /// 근접무기가 여기서 나옵니다.
        /// </summary>
        private static WeaponModelHandle? FromAgentPrefab(int typeId, GameObject root)
        {
            Item? itemPrefab;
            try
            {
                itemPrefab = ItemAssetsCollection.GetPrefab(typeId);
            }
            catch
            {
                return null;
            }

            if (itemPrefab == null)
                return null;

            var agentPrefab = HandheldAgentPrefab(itemPrefab);
            if (agentPrefab == null)
                return null;

            var model = UnityEngine.Object.Instantiate(agentPrefab.gameObject, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            if (!HasRenderer(model))
            {
                UnityEngine.Object.Destroy(model);
                return null;
            }

            return new WeaponModelHandle
            {
                Root = root,
                Model = model,
                IsPlaceholder = false,
                Source = "Handheld 에이전트 프리팹",
            };
        }

        /// <summary>
        /// 팩토리 경로 — 아이템을 하나 만들고 게임에게 비주얼을 조립하게 합니다.
        /// 총기가 여기서 나옵니다(부착물 소켓 포함).
        /// </summary>
        private static WeaponModelHandle? FromGraphicFactory(int typeId, GameObject root)
        {
            Item? item;
            try
            {
                item = ItemAssetsCollection.InstantiateSync(typeId);
            }
            catch
            {
                return null;
            }

            if (item == null)
                return null;

            // 만든 아이템도 임시물입니다. root 밑에 넣어 두면 Dispose 한 번으로 같이 사라집니다.
            try
            {
                item.transform.SetParent(root.transform, false);
            }
            catch
            {
                // 부모를 못 바꿔도 그래픽 생성 자체는 시도합니다.
            }

            ItemGraphicInfo? graphic;
            try
            {
                graphic = ItemGraphicInfo.CreateAGraphic(item, root.transform, false, false);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] CreateAGraphic 실패(TypeID {typeId}): {ex.Message}");
                return null;
            }

            if (graphic == null || !HasRenderer(graphic.gameObject))
                return null;

            var model = graphic.gameObject;

            return new WeaponModelHandle
            {
                Root = root,
                Model = model,
                IsPlaceholder = model.name.StartsWith(PlaceholderName, StringComparison.Ordinal),
                Source = "ItemGraphicInfo 팩토리",
            };
        }

        /// <summary>
        /// Item 프리팹에서 손에 드는 ItemAgent 프리팹을 꺼냅니다.
        ///
        /// <c>AgentUtilities.agents</c>는 비공개 필드이고 원소도 비공개 중첩 타입
        /// (<c>AgentKeyPair</c>)이라 리플렉션으로 봅니다. 다만 그 두 필드(key·agentPrefab)는
        /// public이라 이름으로 꺼낼 수 있습니다.
        /// </summary>
        private static Component? HandheldAgentPrefab(Item itemPrefab)
        {
            object? utilities;
            try
            {
                utilities = itemPrefab.AgentUtilities;
            }
            catch
            {
                return null;
            }

            if (utilities == null)
                return null;

            var field = utilities.GetType().GetField("agents",
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);

            if (field?.GetValue(utilities) is not System.Collections.IEnumerable agents)
                return null;

            System.Reflection.FieldInfo? prefabField = null;

            foreach (var pair in agents)
            {
                if (pair == null)
                    continue;

                prefabField ??= pair.GetType().GetField("agentPrefab",
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance);

                // 키는 보지 않습니다. 무기 프리팹의 에이전트 목록에는 손에 드는 것 하나만
                // 들어 있고, 이름을 문자열로 고정해 두면 게임이 키를 바꿀 때 조용히 깨집니다.
                if (prefabField?.GetValue(pair) is Component agentPrefab && HasRenderer(agentPrefab.gameObject))
                    return agentPrefab;
            }

            return null;
        }

        /// <summary>메시가 달린 렌더러가 하나라도 있는지 (파티클·트레일은 무기 실루엣이 아닙니다)</summary>
        private static bool HasRenderer(GameObject root)
        {
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

                if (mesh != null && mesh.vertexCount > 0)
                    return true;
            }

            return false;
        }
    }
}
