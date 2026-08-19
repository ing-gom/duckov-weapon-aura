using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WeaponAura.Helpers;
using WeaponAura.Systems;
using Ducky.Sdk.Localizations;

namespace WeaponAura.UI
{
    /// <summary>
    /// 설정 창의 왼쪽 미리보기.
    ///
    /// 게임 화면을 그대로 찍으면 지형·다른 캐릭터·현재 무기에 붙은 진짜 오라까지 같이 들어와서
    /// 편집 중인 티어의 색을 확인할 수 없습니다. 그래서 <b>플레이어 모델과 무기만 복제한
    /// 전용 무대</b>를 만들고, 그 위에 편집 중인 프로필로 오라를 따로 세웁니다.
    ///
    /// 격리 방법:
    /// - 이름이 비어 있는 레이어 하나를 골라 무대 전체를 거기에 올립니다.
    ///   미리보기 카메라는 그 레이어만, 게임 카메라는 그 레이어를 절대 보지 않습니다.
    /// - 무대는 플레이어 위치에 그대로 둡니다. 멀리 옮기면 그 자리의 조명이 없어서
    ///   모델이 새까맣게 나옵니다.
    /// </summary>
    public class WeaponAuraPreviewStage
    {
        /// <summary>
        /// 미리보기 렌더 해상도.
        ///
        /// 패널의 표시 영역은 1920 기준 388px인데, 캔버스 스케일러가 화면 크기에 맞춰
        /// 늘리기 때문에 2560 화면에서는 500px, 4K에서는 700px 넘게 그려집니다.
        /// 384로 찍어서 늘리면 그 배율만큼 뭉개집니다. 넉넉히 잡아 둡니다 —
        /// 한 장짜리 작은 씬이라 해상도를 올려도 비용이 거의 없습니다.
        /// </summary>
        private const int TextureSize = 1024;

        /// <summary>복제할 때 같이 딸려오는 진짜 오라 오브젝트 접두사</summary>
        private const string AuraNamePrefix = "WeaponAura_";

        private Camera? _camera;
        private RenderTexture? _texture;
        private GameObject? _stage;
        private Transform? _pivot;

        /// <summary>미리보기 무대 위의 오라. 게임에서 쓰는 것과 같은 컨트롤러입니다.</summary>
        private WeaponAuraController? _controller;

        /// <summary>무대를 만들 때 기준이 된 무기 — 바뀌면 무대를 다시 만듭니다.</summary>
        private Component? _sourceAgent;

        /// <summary>카탈로그에서 만든 모델 (손에 없는 무기를 볼 때). 무대와 함께 정리됩니다.</summary>
        private WeaponModelHandle? _catalogModel;

        /// <summary>무대를 만들 때 기준이 된 카탈로그 무기 TypeID</summary>
        private int _sourceTypeId;

        /// <summary>
        /// 이 무기를 대신 보여 줍니다 (0이면 지금 든 무기).
        ///
        /// 무기별 설정을 편집할 때는 그 무기가 보여야 합니다 — 손에 든 다른 총에
        /// 편집 중인 색이 칠해져 있으면 무엇을 고치고 있는지 알 수 없습니다.
        /// </summary>
        public int TargetWeaponTypeId { get; set; }

        /// <summary>
        /// 미리보기에 세운 겹. 값만 바뀔 때 무대를 다시 세우지 않고 여기에 바로 씁니다.
        ///
        /// 슬라이더를 끄는 동안 무대를 매번 다시 세우면 모델 복제가 프레임마다 일어납니다.
        /// </summary>
        private readonly List<(ParticleSystem Particles, WeaponEffectLayer Source)> _extraLayers =
            new List<(ParticleSystem, WeaponEffectLayer)>();

        private Vector3 _extraLayerBounds = Vector3.one * 0.2f;

        /// <summary>겹을 세운 뒤 실측을 남길 시각 (0이면 예약 없음)</summary>
        private float _layerDiagnoseAt;

        /// <summary>복제본에서 되찾은 총구. 없으면 바운즈 앞쪽으로 대신합니다.</summary>
        private Transform? _muzzle;

        /// <summary>지금 뿜는 자리를 보여 주는 표식들. 위치 조정 모드에서만 켭니다.</summary>
        private readonly List<GameObject> _gizmos = new List<GameObject>();

        /// <summary>
        /// 표식을 보여 줄지.
        ///
        /// 늘 켜 두지 않습니다 — 연출을 확인하는 화면에 조준선이 겹쳐 있으면 색과 밝기를
        /// 판단할 수 없습니다. 위치를 만질 때만 켭니다.
        /// </summary>
        public bool ShowGizmos { get; set; }

        private int _layer = -1;
        private Bounds _bounds;
        private int _builtLayers;

        /// <summary>구조가 바뀌어 무대를 다시 세워야 하는지.</summary>
        private bool _rebuildRequested;

        /// <summary>
        /// 다음 프레임에 무대를 다시 세웁니다.
        ///
        /// 링·플립북처럼 <b>만들 때 한 번만</b> 반영되는 값이 있습니다. 예전에는 겹 수만
        /// 지켜보다가, 링을 켜도 미리보기가 그대로였습니다(게임 쪽은 RebuildNow로 다시
        /// 만들어져서 나오는데 미리보기만 안 나옴). 구조가 바뀌는 편집은 전부 여기로 옵니다.
        /// </summary>
        public void RequestRebuild()
        {
            _rebuildRequested = true;
        }

        public float Yaw { get; private set; } = 35f;
        public float Pitch { get; private set; } = 14f;
        /// <summary>
        /// 1보다 작으면 더 멀리서 봅니다. 창의 "확대" 슬라이더가 조절합니다.
        ///
        /// 기본을 최소치로 둡니다. 가까이 당길수록 무기가 화면을 채우는데, 게임은 탑다운이라
        /// 훨씬 멀리서 보기 때문에 당겨 놓으면 같은 오라가 전혀 다른 크기·밝기로 읽힙니다.
        /// 멀리서 보는 쪽이 실제와 가깝습니다.
        /// </summary>
        public float Zoom { get; set; } = MinZoom;

        /// <summary>창의 확대 슬라이더 하한과 같은 값이어야 합니다.</summary>
        internal const float MinZoom = 0.4f;
        public bool AutoRotate { get; set; } = true;

        /// <summary>무대를 세우지 못한 이유 (상태 표시용)</summary>
        public string Status { get; private set; } = L.Preview.NoWeapon;

        public void Rotate(float deltaYaw, float deltaPitch)
        {
            Yaw += deltaYaw;
            Pitch = Mathf.Clamp(Pitch + deltaPitch, -60f, 60f);
        }

        public void AdjustZoom(float delta)
        {
            Zoom = Mathf.Clamp(Zoom * (1f + delta), MinZoom, 2.5f);
        }

        /// <summary>
        /// 각도만 처음 상태로 되돌리고 자동 회전을 다시 켭니다.
        /// 확대는 슬라이더가 갖고 있으므로 건드리지 않습니다 (건드리면 슬라이더와 어긋납니다).
        /// </summary>
        public void ResetView()
        {
            Yaw = 35f;
            Pitch = 14f;
            AutoRotate = true;
        }

        /// <summary>
        /// 무대를 갱신하고 한 장 찍습니다. OnGUI가 아니라 Update에서 불러도 됩니다.
        /// </summary>
        /// <param name="profile">지금 편집 중인 티어의 프로필 — 이 색이 바로 반영됩니다.</param>
        public Texture? Render(WeaponAuraProfile? profile, float intensity)
        {
            try
            {
                if (profile == null)
                    return null;

                EnsureLayer();
                EnsureResources();

                if (!EnsureStage(profile, intensity))
                    return null;

                if (_camera == null || _texture == null || _pivot == null)
                    return null;

                // 겹 수는 껍질 개수라서 슬라이더로 바뀌면 무대를 다시 세워야 합니다.
                // 다음 프레임에 EnsureStage가 새로 만듭니다.
                if (_controller != null &&
                    (_rebuildRequested || _builtLayers != Mathf.Max(1, profile.sheetLayers)))
                {
                    _rebuildRequested = false;
                    DestroyStage();
                    return null;
                }

                RunLayerDiagnoseIfDue();

                if (AutoRotate)
                    Yaw += Time.unscaledDeltaTime * 22f;

                _pivot.localRotation = Quaternion.Euler(0f, Yaw, 0f);

                // 색·투명도 같은 값은 매 프레임 넘겨서 슬라이더를 움직이는 즉시 반영합니다.
                // 셸 애니메이션과 파티클은 컨트롤러가 스스로 돌립니다.
                if (_controller != null)
                    _controller.ApplyLive(profile, intensity);

                PlaceCamera();
                _camera.Render();
                return _texture;
            }
            catch (Exception ex)
            {
                Status = L.Preview.Error;
                UnityEngine.Debug.LogWarning($"[WeaponAura] 미리보기 무대 오류: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            DestroyStage();

            if (_camera != null)
            {
                UnityEngine.Object.Destroy(_camera.gameObject);
                _camera = null;
            }

            if (_texture != null)
            {
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
        }

        // ── 레이어 · 카메라 ──────────────────────────────────────────

        /// <summary>
        /// 이름이 없는(=게임이 쓰지 않는) 레이어를 하나 빌립니다.
        /// 빈 레이어가 없으면 격리를 포기하고 Character 레이어를 씁니다
        /// (이때는 배경에 다른 캐릭터가 같이 보일 수 있습니다).
        /// </summary>
        private void EnsureLayer()
        {
            if (_layer >= 0)
                return;

            for (int i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    _layer = i;
                    return;
                }
            }

            _layer = Mathf.Max(0, LayerMask.NameToLayer("Character"));
            UnityEngine.Debug.LogWarning("[WeaponAura] 비어 있는 레이어가 없어 미리보기를 격리하지 못했습니다.");
        }

        private void EnsureResources()
        {
            if (_texture == null)
            {
                // 게임이 HDR로 그리면 미리보기도 HDR이어야 합니다. LDR 타깃에 담으면
                // 가산 합성으로 1을 넘긴 밝기가 먼저 잘려서 블룸이 걸릴 거리가 없어집니다.
                var format = PreviewCameraSetup.GameUsesHdr()
                    ? RenderTextureFormat.DefaultHDR
                    : RenderTextureFormat.ARGB32;

                _texture = new RenderTexture(TextureSize, TextureSize, 24, format)
                {
                    name = "WeaponAura_PreviewRT",
                    antiAliasing = 2,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _texture.Create();
            }

            if (_camera == null)
            {
                var go = new GameObject("WeaponAura_PreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);

                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;               // 수동 Render()만
                _camera.targetTexture = _texture;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                // 오라는 가산 합성이라 "더하기"만 합니다 — 어두워질 수가 없습니다.
                // 배경이 거의 검정이면 오라가 옅어지는 바깥쪽이 그대로 검게 보여서
                // 마치 검은 테두리가 생긴 것처럼 읽힙니다. 실제 게임 화면은 밝아서 그런 일이 없습니다.
                // 중간 밝기 배경을 쓰면 미리보기와 게임에서 같은 모양으로 보입니다.
                _camera.backgroundColor = new Color(0.20f, 0.23f, 0.28f, 1f);
                _camera.fieldOfView = 32f;
                _camera.cullingMask = 1 << _layer;     // 무대만 찍습니다
            }
            else
            {
                _camera.cullingMask = 1 << _layer;
            }

            // 게임 화면의 후처리(블룸 등)를 그대로 받게 맞춥니다.
            // 이걸 안 하면 미리보기만 맨 카메라라, 같은 이펙트가 덜 밝고 작게 보입니다.
            PreviewCameraSetup.Match(_camera, "오라", _layer);
        }

        private void PlaceCamera()
        {
            if (_camera == null || _stage == null)
                return;

            // 4.6배 — 총기 바운즈만 잡으면 화면에 꽉 차서 형태가 안 보입니다.
            // 오라 껍질이 바운즈 밖으로 뻗는 것까지 감안해 넉넉하게 물러납니다.
            float radius = Mathf.Max(0.2f, _bounds.extents.magnitude);
            float distance = radius * 4.6f / Zoom;

            var center = _stage.transform.TransformPoint(_bounds.center);
            var rotation = Quaternion.Euler(Pitch, 0f, 0f);

            // 회전은 무대(_pivot)가 담당하므로 카메라는 앞에서 내려다보기만 합니다.
            _camera.transform.position = center + rotation * new Vector3(0f, 0f, -distance);
            _camera.transform.rotation = rotation;
            _camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 3f);
            _camera.farClipPlane = distance + radius * 8f;
        }

        // ── 무대 ────────────────────────────────────────────────────

        private bool EnsureStage(WeaponAuraProfile profile, float intensity)
        {
            if (TargetWeaponTypeId > 0)
                return EnsureCatalogStage(profile, intensity);

            var player = CharacterMainControl.Main;
            var holder = player != null ? player.agentHolder : null;
            var agent = holder != null ? holder.CurrentHoldItemAgent : null;

            if (agent == null)
            {
                DestroyStage();
                Status = L.Preview.NoWeapon;
                return false;
            }

            if (_stage != null && ReferenceEquals(_sourceAgent, agent))
                return true;

            DestroyStage();
            return BuildStage(player!, agent, profile, intensity);
        }

        private bool BuildStage(CharacterMainControl player, Component agent, WeaponAuraProfile profile,
            float intensity)
        {
            var model = FindModelRoot(player, agent);
            if (model == null)
            {
                Status = L.Preview.NoModel;
                return false;
            }

            // 복제 전에 "어떤 렌더러가 실루엣 원본인지"를 경로로 기억해 둡니다.
            // 복제본에는 ItemAgent가 없어서 선택 로직을 다시 돌릴 수 없습니다.
            var sourcePaths = new List<string>();
            foreach (var renderer in WeaponAuraSystem.SilhouetteSources)
            {
                if (renderer == null)
                    continue;

                string? path = PathFrom(model, renderer.transform);
                if (path != null)
                    sourcePaths.Add(path);
            }

            // 총구도 같은 방법으로 기억합니다.
            //
            // 복제본에는 총구 <b>오브젝트</b>가 그대로 남아 있습니다. 사라지는 것은
            // ItemAgent_Gun.muzzle이라는 <b>참조</b>뿐입니다(StripClone이 스크립트를 지웁니다).
            // 그래서 실루엣과 똑같이 경로로 적어 두었다가 복제본에서 다시 찾으면
            // 총구 자리가 근사치가 아니라 실제 위치가 됩니다.
            string? muzzlePath = null;
            if (agent is ItemAgent_Gun gun && gun.muzzle != null)
                muzzlePath = PathFrom(model, gun.muzzle);

            var root = new GameObject("WeaponAura_PreviewStage");
            UnityEngine.Object.DontDestroyOnLoad(root);

            // 플레이어가 서 있는 자리의 조명을 그대로 받도록 위치를 맞춥니다.
            root.transform.position = model.position;
            root.transform.rotation = Quaternion.identity;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);

            var clone = UnityEngine.Object.Instantiate(model.gameObject, pivot);
            clone.name = "Model";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            _stage = root;
            _pivot = pivot;
            _sourceAgent = agent;

            StripClone(clone);
            SetLayerRecursive(clone.transform, _layer);

            _muzzle = string.IsNullOrEmpty(muzzlePath) ? null : clone.transform.Find(muzzlePath);

            // 모델 기준으로 다시 가운데를 맞춥니다 (발밑이 아니라 무기 근처가 중심이 되도록)
            var weaponRenderers = ResolveRenderers(clone.transform, sourcePaths);

            _bounds = MeasureBounds(root.transform, weaponRenderers, clone.transform);

            if (weaponRenderers.Count == 0)
            {
                Status = L.Preview.NoParts;
                _builtLayers = 0;
                return true;
            }

            BuildAura(weaponRenderers, profile, intensity);
            Status = "-";
            return true;
        }

        /// <summary>
        /// 손에 없는 무기의 무대.
        ///
        /// 든 무기 쪽은 플레이어 모델을 통째로 복제해서 그중 무기 부품을 골라내지만,
        /// 여기서는 <see cref="WeaponModelSource"/>가 무기 모델만 만들어 주므로
        /// 그 안의 렌더러가 곧 무기 실루엣입니다.
        /// </summary>
        private bool EnsureCatalogStage(WeaponAuraProfile profile, float intensity)
        {
            if (_stage != null && _sourceTypeId == TargetWeaponTypeId && _catalogModel != null)
                return true;

            DestroyStage();

            var root = new GameObject("WeaponAura_PreviewStage");
            UnityEngine.Object.DontDestroyOnLoad(root);

            // 플레이어가 있으면 그 자리의 조명을 그대로 받게 두고, 없으면 원점에 세웁니다.
            var player = CharacterMainControl.Main;
            root.transform.position = player != null ? player.transform.position : Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);

            var handle = WeaponModelSource.Create(TargetWeaponTypeId, pivot);
            if (handle == null)
            {
                UnityEngine.Object.Destroy(root);
                Status = L.Preview.NoModel;

                UnityEngine.Debug.LogWarning(
                    $"[WeaponAura] 개별 무기 미리보기 실패: TypeID {TargetWeaponTypeId} " +
                    $"({WeaponAura.Helpers.WeaponHelper.GetDisplayName(TargetWeaponTypeId)}) — 모델을 못 만들었습니다.");
                return false;
            }

            UnityEngine.Debug.Log(
                $"[WeaponAura] 개별 무기 미리보기: " +
                $"{WeaponAura.Helpers.WeaponHelper.GetDisplayName(TargetWeaponTypeId)} " +
                $"(TypeID {TargetWeaponTypeId}) 경로={handle.Source} " +
                $"자리표시자={handle.IsPlaceholder}");

            _stage = root;
            _pivot = pivot;
            _catalogModel = handle;
            _sourceTypeId = TargetWeaponTypeId;
            _sourceAgent = null;

            var model = handle.Model;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // 임시로 만든 아이템 본체는 화면에 안 나오지만 로직이 돌 수 있습니다.
            // 모델 쪽만 정리하고 아이템은 Dispose가 통째로 걷어 갑니다.
            StripClone(model);
            SetLayerRecursive(model.transform, _layer);

            var weaponRenderers = CollectRenderers(model.transform);

            _bounds = MeasureBounds(root.transform, weaponRenderers, model.transform);

            if (weaponRenderers.Count == 0)
            {
                Status = L.Preview.NoParts;
                _builtLayers = 0;
                return true;
            }

            BuildAura(weaponRenderers, profile, intensity);
            Status = "-";
            return true;
        }

        /// <summary>모델 안의 무기 렌더러 전부 (파티클·트레일 제외)</summary>
        private static List<Renderer> CollectRenderers(Transform root)
        {
            var result = new List<Renderer>();

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
                    result.Add(renderer);
            }

            return result;
        }

        /// <summary>플레이어 캐릭터 모델의 루트. 무기는 손 소켓에 붙어 있어 같이 복제됩니다.</summary>
        private static Transform? FindModelRoot(CharacterMainControl player, Component agent)
        {
            // 무기에서 위로 올라가다 CharacterMainControl 바로 아래 자식을 모델 루트로 봅니다.
            var playerTransform = player.transform;

            for (var t = agent.transform; t != null; t = t.parent)
            {
                if (t.parent == playerTransform)
                    return t;
            }

            // 못 찾으면 플레이어 전체를 복제합니다.
            return playerTransform;
        }

        /// <summary>
        /// 복제본에서 게임 스크립트를 전부 떼어냅니다.
        /// 남겨 두면 매니저에 자기를 등록하거나 플레이어를 따라다니는 등 본편에 영향을 줍니다.
        /// 렌더러·메시필터·본 트랜스폼만 남으므로 복제 시점의 자세로 굳습니다.
        /// </summary>
        private static void StripClone(GameObject clone)
        {
            // 진짜 무기에 붙어 있던 오라가 같이 복제됩니다.
            // 그대로 두면 "현재 티어 색"과 "편집 중인 색"이 겹쳐 보입니다.
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.gameObject.name.StartsWith(AuraNamePrefix, StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null)
                    UnityEngine.Object.DestroyImmediate(behaviour);
            }

            foreach (var animator in clone.GetComponentsInChildren<Animator>(true))
            {
                if (animator != null)
                    UnityEngine.Object.DestroyImmediate(animator);
            }

            foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body != null)
                    UnityEngine.Object.DestroyImmediate(body);
            }

            // 총구 화염·탄피 같은 파티클은 미리보기에서 필요 없습니다.
            foreach (var particles in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles != null)
                    UnityEngine.Object.DestroyImmediate(particles.gameObject);
            }

            foreach (var line in clone.GetComponentsInChildren<LineRenderer>(true))
            {
                if (line != null)
                    UnityEngine.Object.DestroyImmediate(line);
            }

            foreach (var trail in clone.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (trail != null)
                    UnityEngine.Object.DestroyImmediate(trail);
            }
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;

            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        // ── 실루엣 껍질 ─────────────────────────────────────────────

        /// <summary>
        /// 미리보기용 오라를 세웁니다.
        ///
        /// 예전에는 면(셸)만 직접 만들었는데, 그러면 표면 파티클과 링이 빠져서
        /// 게임에서 보이는 것과 다른 그림이 나왔습니다. 이제 실제 게임과 같은
        /// <see cref="WeaponAuraController"/>를 그대로 씁니다. 다른 점은 두 가지뿐입니다.
        /// - 복제본에는 ItemAgent가 없으므로 실루엣 원본을 직접 지정합니다
        /// - 설정 창은 게임이 멈춘 상태에서 열리므로 unscaled 시간으로 돌립니다
        /// </summary>
        private void BuildAura(List<Renderer> weaponRenderers, WeaponAuraProfile profile, float intensity)
        {
            var host = weaponRenderers[0].transform;

            // 본편과 같은 방식으로, 호스트 스케일을 상쇄해 오라 루트의 월드 스케일을 1로 만듭니다.
            // 이걸 빼먹으면 뻗는 거리(m 단위)가 무기 모델 스케일만큼 곱해집니다.
            var rootObject = new GameObject(AuraNamePrefix + "PreviewRoot");
            rootObject.SetActive(false);
            rootObject.transform.SetParent(host, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;

            Vector3 hostScale = host.lossyScale;
            rootObject.transform.localScale = new Vector3(
                Mathf.Approximately(hostScale.x, 0f) ? 1f : 1f / hostScale.x,
                Mathf.Approximately(hostScale.y, 0f) ? 1f : 1f / hostScale.y,
                Mathf.Approximately(hostScale.z, 0f) ? 1f : 1f / hostScale.z);

            Bounds localBounds = LocalBoundsOf(weaponRenderers, rootObject.transform);

            // 방출 영역은 <b>게임이 방금 잰 값</b>을 그대로 씁니다.
            //
            // 미리보기가 스스로 재면 대상이 달라집니다 — 복제본에는 ItemAgent가 없어서
            // 실루엣 부품 하나만 잡히고, 게임은 부착물까지 포함한 무기 전체를 잡습니다.
            // 실제로 Z축이 0.037 대 0.331로 9배까지 벌어졌고, 그만큼 인게임 알갱이가
            // 더 넓게 퍼져 보였습니다. 같은 규칙을 두 번 구현하는 대신 결과를 넘겨받습니다.
            var measured = WeaponAuraSystem.WeaponBoundsSize;
            if (measured.sqrMagnitude > 0.000001f)
                localBounds = new Bounds(localBounds.center, measured);

            var controller = rootObject.AddComponent<WeaponAuraController>();
            controller.SilhouetteOverride = weaponRenderers;
            controller.UseUnscaledTime = true;

            // 무대는 모델을 제자리에서 돌립니다. "지나간 자리에 남기기"를 그대로 두면
            // 알갱이가 그 회전을 따라 고리로 번져서, 실제 게임에서 보게 될 궤적과
            // 전혀 다른 그림이 됩니다.
            controller.ForceLocalParticles = true;
            controller.Origin = "미리보기";

            // holder는 null — CharacterSubVisuals에 등록하면 안 됩니다.
            // 미리보기는 캐릭터 은폐 상태를 따라갈 이유가 없고, 본편 캐릭터를 건드리게 됩니다.
            bool built = controller.Build(null, weaponRenderers[0], localBounds.center,
                localBounds.size, hostScale, profile, intensity);

            if (!built)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                Status = L.Preview.NoSilhouette;
                return;
            }

            rootObject.SetActive(true);

            // 컨트롤러가 만든 파티클·셸까지 전부 미리보기 레이어로 옮겨야
            // 미리보기 카메라에 잡히고, 게임 화면에는 안 보입니다.
            SetLayerRecursive(rootObject.transform, _layer);

            _controller = controller;
            _builtLayers = Mathf.Max(1, profile.sheetLayers);

            BuildExtraLayers(rootObject.transform, profile, localBounds);
        }

        /// <summary>
        /// 오라 위에 얹은 겹을 미리보기에도 세웁니다.
        ///
        /// 겹은 게임에서 <see cref="WeaponAuraLayerSystem"/>이 <b>든 무기</b>에 붙입니다.
        /// 미리보기는 자기 무대를 따로 세우므로 그것이 따라오지 않아서, 겹을 추가해도
        /// 미리보기에는 아무 변화가 없었습니다. 같은 함수로 여기서도 만듭니다.
        ///
        /// 자리(총구·총열)는 무대에 없습니다 — 복제본에는 ItemAgent도 소켓도 없습니다.
        /// 그래서 전부 무기 바운즈 기준으로 놓습니다. 무기 전체는 그대로 맞고, 총구·총열은
        /// 앞쪽 끝으로 근사합니다. 정확한 자리는 게임 화면에서 확인해야 합니다.
        /// </summary>
        private void BuildExtraLayers(Transform root, WeaponAuraProfile profile, Bounds localBounds)
        {
            _extraLayerBounds = localBounds.size;

            if (profile.layers.Length == 0)
                return;

            int made = 0;
            int skipped = 0;
            var reasons = new StringBuilder();

            foreach (var layer in profile.layers)
            {
                // 왜 건너뛰었는지 남깁니다. "0개 세움"만 보이면 원인을 알 수 없습니다.
                if (layer == null)
                {
                    skipped++;
                    reasons.Append(" [null]");
                    continue;
                }

                if (!layer.enabled)
                {
                    skipped++;
                    reasons.Append($" [{layer.anchor} 꺼짐]");
                    continue;
                }

                Transform parent = root;
                Vector3 position = localBounds.center;

                if (layer.anchor == WeaponParticleAnchor.Muzzle && _muzzle != null)
                {
                    // 되찾은 진짜 총구에 붙입니다 — 게임과 같은 자리입니다.
                    parent = _muzzle;
                    position = Vector3.zero;
                }
                else if (layer.anchor == WeaponParticleAnchor.Muzzle
                         || layer.anchor == WeaponParticleAnchor.Barrel)
                {
                    // 총열은 게임에서도 바운즈로 계산하므로 여기서도 같은 규칙입니다.
                    position += new Vector3(0f, 0f, localBounds.size.z * 0.5f);
                }

                try
                {
                    // 무대는 모델을 제자리에서 돌립니다. 월드로 두면 알갱이가 그 회전을
                    // 따라 고리로 번져서 실제 게임과 다른 그림이 됩니다.
                    var go = WeaponAuraLayerSystem.CreateEmitter(
                        layer, parent, position, localBounds.size, out var particles,
                        localSpace: true);

                    // 무대만 찍는 카메라에 잡히도록 레이어를 옮깁니다.
                    SetLayerRecursive(go.transform, _layer);

                    _extraLayers.Add((particles, layer));
                    made++;

                    BuildGizmo(parent, position + layer.offset, layer, localBounds.size);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[WeaponAura] 미리보기 겹 생성 실패: {ex.Message}");
                }
            }

            // 뿜기 시작할 시간을 준 뒤 실제 상태를 한 번 더 남깁니다.
            _layerDiagnoseAt = Time.unscaledTime + 1f;

            // "안 만들어졌다"와 "만들어졌는데 안 보인다"를 로그로 가릅니다.
            UnityEngine.Debug.Log(
                $"[WeaponAura] 미리보기 겹: {made}/{profile.layers.Length}개 세움 " +
                $"(건너뜀 {skipped}{reasons}) " +
                $"| 레이어={_layer} 바운즈={localBounds.size} 중심={localBounds.center} " +
                $"| 무대위치={root.position}");
        }

        /// <summary>지정한 렌더러들을 감싸는 바운즈를 기준 트랜스폼의 로컬 좌표로 구합니다.</summary>
        private static Bounds LocalBoundsOf(List<Renderer> renderers, Transform space)
        {
            bool initialized = false;
            var result = new Bounds(Vector3.zero, Vector3.one * 0.2f);

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                var world = renderer.bounds;
                var local = new Bounds(space.InverseTransformPoint(world.center), Vector3.zero);
                local.Encapsulate(space.InverseTransformPoint(world.min));
                local.Encapsulate(space.InverseTransformPoint(world.max));

                if (!initialized)
                {
                    result = local;
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(local);
                }
            }

            return result;
        }

        // ── 보조 ────────────────────────────────────────────────────

        /// <summary>root 기준 상대 경로 (복제본에서 같은 부품을 찾기 위함)</summary>
        private static string? PathFrom(Transform root, Transform target)
        {
            var parts = new List<string>();

            for (var t = target; t != null; t = t.parent)
            {
                if (t == root)
                {
                    parts.Reverse();
                    return string.Join("/", parts);
                }
                parts.Add(t.name);
            }

            return null;
        }

        private static List<Renderer> ResolveRenderers(Transform cloneRoot, List<string> paths)
        {
            var result = new List<Renderer>();

            foreach (string path in paths)
            {
                var found = string.IsNullOrEmpty(path) ? cloneRoot : cloneRoot.Find(path);
                var renderer = found != null ? found.GetComponent<Renderer>() : null;

                if (renderer != null)
                    result.Add(renderer);
            }

            return result;
        }

        /// <summary>
        /// 카메라가 잡을 범위. 무기 부품이 있으면 그 주변만, 없으면 모델 전체를 잡습니다.
        /// </summary>
        private static Bounds MeasureBounds(Transform stageRoot, List<Renderer> weaponRenderers, Transform model)
        {
            var targets = weaponRenderers.Count > 0
                ? weaponRenderers
                : new List<Renderer>(model.GetComponentsInChildren<Renderer>(false));

            bool initialized = false;
            var bounds = new Bounds(Vector3.zero, Vector3.one * 0.4f);

            foreach (var renderer in targets)
            {
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;

                var worldBounds = renderer.bounds;
                if (worldBounds.size.sqrMagnitude <= 1e-8f)
                    continue;

                // 레이저처럼 멀리 뻗는 렌더러는 프레이밍을 망칩니다.
                if (worldBounds.size.x > 3f || worldBounds.size.y > 3f || worldBounds.size.z > 3f)
                    continue;

                var local = new Bounds(stageRoot.InverseTransformPoint(worldBounds.center), Vector3.zero);
                local.Encapsulate(stageRoot.InverseTransformPoint(worldBounds.min));
                local.Encapsulate(stageRoot.InverseTransformPoint(worldBounds.max));

                if (!initialized)
                {
                    bounds = local;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }

            // 무기만 잡으면 너무 빡빡하니 여유를 둡니다.
            bounds.Expand(0.3f);
            return bounds;
        }

        private void DestroyStage()
        {
            _controller = null;
            _builtLayers = 0;

            if (_stage != null)
            {
                UnityEngine.Object.Destroy(_stage);
                _stage = null;
            }

            // 카탈로그 모델은 임시 아이템까지 함께 들고 있습니다. 무대만 지우고 놔두면
            // 그 아이템이 씬에 남습니다.
            if (_catalogModel != null)
            {
                _catalogModel.Dispose();
                _catalogModel = null;
            }

            _extraLayers.Clear();
            _muzzle = null;

            foreach (var gizmo in _gizmos)
            {
                if (gizmo != null)
                    UnityEngine.Object.Destroy(gizmo);
            }

            _gizmos.Clear();

            // 예약해 둔 실측을 그대로 두면 파괴된 이미터를 재서 "재생=False 살아있음=0"이
            // 찍힙니다. 실제로는 무대가 사라진 것인데 겹이 죽은 것처럼 읽힙니다.
            _layerDiagnoseAt = 0f;

            _pivot = null;
            _sourceAgent = null;
            _sourceTypeId = 0;
        }

        /// <summary>
        /// 겹의 값만 바뀌었을 때 — 무대를 다시 세우지 않고 즉시 덮어씁니다.
        ///
        /// 겹이 늘거나 자리·모양이 바뀌면 이걸로는 부족합니다. 그때는 호출부가
        /// <see cref="RequestRebuild"/>를 부릅니다.
        /// </summary>
        /// <summary>
        /// 뿜는 자리와 범위를 눈에 보이게 세웁니다.
        ///
        /// 숫자만으로는 조준이 안 됩니다 — 어디서 나오는지 안 보이는 채로 슬라이더를
        /// 옮기고 결과를 보고 다시 옮기는 식이 됩니다. 가운데 점은 <b>자리</b>,
        /// 둘레 상자·구는 <b>범위</b>입니다.
        ///
        /// 큐브 하나로 둘 다 만듭니다. 선(LineRenderer)으로 그리면 굵기·정렬을 카메라
        /// 거리마다 다시 맞춰야 하는데, 반투명 상자는 그냥 스케일만 주면 됩니다.
        /// </summary>
        private void BuildGizmo(Transform parent, Vector3 localPosition,
            WeaponEffectLayer layer, Vector3 weaponSize)
        {
            if (!ShowGizmos)
                return;

            try
            {
                // 자리 — 작은 밝은 점
                var center = GameObject.CreatePrimitive(PrimitiveType.Cube);
                center.name = "WeaponAura_GizmoCenter";
                StripGizmo(center, new Color(1f, 0.95f, 0.3f, 0.95f));
                center.transform.SetParent(parent, false);
                center.transform.localPosition = localPosition;
                center.transform.localScale = Vector3.one * 0.03f;
                SetLayerRecursive(center.transform, _layer);
                _gizmos.Add(center);

                // 범위 — 방출 영역과 같은 크기의 반투명 덩어리
                Vector3 size = layer.anchor == WeaponParticleAnchor.Whole
                    ? weaponSize * (1f + Mathf.Max(0f, layer.spread))
                    : Vector3.one * Mathf.Max(0.01f, layer.spread) * 2f;

                var range = GameObject.CreatePrimitive(PrimitiveType.Cube);
                range.name = "WeaponAura_GizmoRange";
                StripGizmo(range, new Color(0.3f, 0.8f, 1f, 0.12f));
                range.transform.SetParent(parent, false);
                range.transform.localPosition = localPosition;
                range.transform.localScale = size;
                SetLayerRecursive(range.transform, _layer);
                _gizmos.Add(range);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 표식 생성 실패: {ex.Message}");
            }
        }

        /// <summary>표식에서 충돌체를 떼고 반투명 재질을 입힙니다.</summary>
        private static void StripGizmo(GameObject go, Color color)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // 오라가 쓰는 머티리얼을 복제해서 색만 바꿉니다 — 셰이더를 새로 찾지 않아도
            // 이 빌드에 확실히 있는 것을 쓰게 됩니다.
            var source = WeaponAuraResources.SharedMaterial;
            if (source != null)
            {
                var material = new Material(source);
                material.color = color;
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>예약해 둔 겹 실측을 때가 되면 남깁니다. Render에서 부릅니다.</summary>
        private void RunLayerDiagnoseIfDue()
        {
            if (_layerDiagnoseAt <= 0f || Time.unscaledTime < _layerDiagnoseAt)
                return;

            _layerDiagnoseAt = 0f;

            var sb = new StringBuilder("[WeaponAura] 겹 실측(미리보기)");
            sb.Append(" 카메라마스크=").Append(_camera != null ? _camera.cullingMask.ToString() : "-");

            foreach (var entry in _extraLayers)
            {
                sb.AppendLine().Append(WeaponAuraLayerSystem.DescribeEmitter(
                    entry.Particles, entry.Source != null ? entry.Source.anchor.ToString() : "-"));
            }

            UnityEngine.Debug.Log(sb.ToString());
        }

        public void ApplyLayerValues()
        {
            foreach (var entry in _extraLayers)
            {
                if (entry.Particles == null || entry.Source == null)
                    continue;

                try
                {
                    WeaponAuraLayerSystem.ApplyTo(entry.Particles, entry.Source, _extraLayerBounds);
                }
                catch
                {
                    // 미리보기 한 겹 때문에 창이 멈추면 안 됩니다.
                }
            }
        }

        /// <summary>진단용 요약</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("무대=").Append(_stage != null ? "있음" : "없음");
            sb.Append(" 오라=").Append(_controller != null ? "있음" : "없음");
            sb.Append(" 레이어=").Append(_layer);
            return sb.ToString();
        }
    }
}
