using System;
using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using WeaponAura.Helpers;
using WeaponAura.Settings;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 날아가는 총알에 등급 색 잔상을 붙이는 시스템.
    ///
    /// 총알(<see cref="Projectile"/>)은 풀에서 재사용되고, 맞는 순간 GameObject가
    /// 통째로 비활성화됩니다. 그래서 잔상을 총알의 자식으로 달면 명중과 동시에 꼬리가
    /// 뚝 끊깁니다. 대신 잔상 오브젝트를 따로 두고 매 프레임 총알 위치만 따라가게 해서,
    /// 총알이 사라진 뒤에도 남은 꼬리가 제자리에서 자연스럽게 사라지도록 합니다.
    ///
    /// 잔상 오브젝트는 풀링합니다 — 연사 무기는 초당 수십 발을 쏘기 때문에
    /// 매번 만들고 버리면 그대로 GC 부담이 됩니다.
    /// </summary>
    public static class BulletTrailSystem
    {
        /// <summary>동시에 살아 있을 수 있는 잔상 수. 넘으면 새 총알은 잔상 없이 나갑니다.</summary>
        private const int MaxLiveTrails = 96;

        private const string HolderName = "WeaponAura_BulletTrails";
        private const string TrailName = "WeaponAura_BulletTrail";
        private const string HeadName = "WeaponAura_BulletHead";
        private const string StampName = "WeaponAura_BulletStamps";

        /// <summary>자국 하나가 남길 수 있는 최대 개수. 연사 무기가 화면을 덮는 것을 막습니다.</summary>
        private const int MaxStampsPerBullet = 64;

        /// <summary>1m당 자국 수 상한. 총알이 길게 날아가도 총량이 터지지 않게 잡아 둡니다.</summary>
        private const float MaxStampRate = 20f;

        /// <summary>상태 표시용 폴링 간격(초)</summary>
        private const float StatusInterval = 0.25f;

        private sealed class TrailHandle
        {
            public GameObject Go = null!;
            public TrailRenderer Trail = null!;

            /// <summary>
            /// 총알 머리. 원본 궤적을 숨겼을 때만 켭니다.
            ///
            /// <b>궤적이 아니라 판입니다.</b> 처음에는 아주 짧은 TrailRenderer로 그렸는데,
            /// 궤적은 길이가 <c>시간 × 총알 속도</c>로 정해집니다. 0.04초라도 총알이 워낙
            /// 빨라서 실제로는 굵기의 열 배가 넘는 띠가 되고, 텍스처가 그만큼 늘어나
            /// 마름모든 고리든 전부 같은 바늘 모양이 됐습니다.
            ///
            /// 그래서 가로세로비가 고정된 판에 도형을 찍고, 매 프레임 카메라를 향해
            /// 눕히면서 긴 쪽을 진행 방향에 맞춥니다.
            /// </summary>
            public GameObject HeadGo = null!;
            public MeshRenderer HeadRenderer = null!;

            /// <summary>머리 판의 월드 크기(m). 매 프레임 방향을 맞출 때 같이 씁니다.</summary>
            public float HeadLength;
            public float HeadWidth;

            /// <summary>
            /// 지나간 자리에 도형을 남기는 자국. 선 방식일 때는 꺼 둡니다.
            ///
            /// 이동 거리 기준으로 뿌립니다(<c>rateOverDistance</c>). 시간 기준이면 빠른
            /// 총알은 자국이 뜨문뜨문하고 느린 총알은 뭉칩니다.
            /// </summary>
            public ParticleSystem Stamps = null!;
            public ParticleSystemRenderer StampRenderer = null!;

            /// <summary>따라가는 총알. null이면 사라지는 중입니다.</summary>
            public Projectile? Follow;

            /// <summary>사라지는 중일 때, 이 시각이 지나면 회수합니다.</summary>
            public float RecycleAt;
        }

        private static GameObject? _holder;
        private static readonly Stack<TrailHandle> _idle = new Stack<TrailHandle>();
        private static readonly List<TrailHandle> _live = new List<TrailHandle>();

        /// <summary>지금 어떤 총알을 어떤 잔상이 따라가는지 (총알 재사용 감지용)</summary>
        private static readonly Dictionary<Projectile, TrailHandle> _followed =
            new Dictionary<Projectile, TrailHandle>();

        private static Material? _additiveMaterial;
        private static Material? _blendMaterial;
        private static Texture2D? _streakTexture;

        private static float _nextStatusTime;

        // ── 상태 조회 (설정 창 표시용) ────────────────────────────
        /// <summary>지금 든 총에 들어 있는 탄환 이름</summary>
        public static string CurrentAmmoName { get; private set; } = "-";
        /// <summary>지금 든 총에 들어 있는 탄환 등급 (없으면 -1)</summary>
        public static int CurrentAmmoQuality { get; private set; } = -1;
        /// <summary>탄약이 바뀔 때마다 올라갑니다 (설정 창이 상태 줄을 다시 그릴지 판단하는 용도).</summary>
        public static int AmmoRevision { get; private set; }
        /// <summary>지금 살아 있는 잔상 수 (진단용)</summary>
        public static int LiveCount => _live.Count;

        // ──────────────────────────────────────────────────────────

        /// <summary>
        /// 총알 하나에 잔상을 붙입니다. 발사 패치에서 총알이 초기화된 직후에 부릅니다.
        /// </summary>
        public static void Apply(Projectile? projectile, int ammoQuality)
        {
            if (projectile == null || !BulletTrailSettings.Enabled)
                return;

            try
            {
                var profile = BulletTrailProfiles.Resolve(ammoQuality);
                if (profile == null || !profile.enabled)
                {
                    // 이 등급은 모드 잔상을 안 그립니다. 원본까지 숨기면 총알이 아무
                    // 흔적 없이 날아갑니다. (같은 인스턴스가 직전에 숨겨졌을 수 있습니다)
                    VanillaTrailSuppressor.Restore(projectile);
                    BulletGlowController.Restore(projectile);
                    return;
                }

                // 원본 궤적은 모드 잔상이 실제로 대신 그려질 때만 숨깁니다.
                VanillaTrailSuppressor.SetSuppressed(projectile, BulletTrailSettings.HideVanillaTrail);

                // 발광체는 잔상을 숨기는지와 무관합니다 — 자체 옵션으로만 갈립니다.
                if (BulletTrailSettings.CustomizeGlow)
                    BulletGlowController.Apply(projectile, profile);
                else
                    BulletGlowController.Restore(projectile);

                // 같은 총알을 풀에서 곧바로 다시 꺼내 쏜 경우, 이전 잔상은 여기서 끊어 줍니다.
                // (안 그러면 새 총알을 따라가면서 지난 궤적이 이어져 화면을 가로지릅니다)
                if (_followed.TryGetValue(projectile, out var previous))
                    BeginFade(previous);

                if (_live.Count >= MaxLiveTrails)
                    return;

                var handle = Rent();
                if (handle == null)
                    return;

                Configure(handle, profile);

                handle.Go.transform.position = projectile.transform.position;
                handle.Go.transform.rotation = projectile.transform.rotation;
                handle.Go.SetActive(true);

                // 위치를 옮긴 뒤에 지워야 이전 궤적이 남지 않습니다.
                handle.Trail.Clear();
                handle.Trail.emitting = true;

                if (handle.Stamps != null && handle.StampRenderer != null && handle.StampRenderer.enabled)
                {
                    handle.Stamps.Clear();
                    handle.Stamps.Play();
                }

                // 판은 궤적처럼 쌓이는 것이 없어서 지울 것도 없습니다.
                // 방향은 첫 프레임 갱신에서 맞춥니다.
                UpdateHead(handle, projectile);

                handle.Follow = projectile;
                handle.RecycleAt = 0f;

                _live.Add(handle);
                _followed[projectile] = handle;
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 탄환 잔상 생성 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>ModBehaviour.Update에서 호출 — 설정 창에 보여 줄 탄약 정보만 읽습니다.</summary>
        public static void Tick()
        {
            TickStatus();
        }

        /// <summary>
        /// ModBehaviour.LateUpdate에서 호출 — 총알 위치를 따라갑니다.
        ///
        /// 반드시 LateUpdate여야 합니다. 총알은 자기 Update에서 움직이는데 스크립트 실행 순서는
        /// 정해져 있지 않아서, Update에서 따라가면 프레임에 따라 꼬리가 총알보다 한 프레임 뒤처집니다.
        /// 총알이 초당 수십 미터로 날아가므로 한 프레임이면 눈에 띄게 어긋납니다.
        /// </summary>
        public static void LateTick()
        {
            TickTrails();
        }

        /// <summary>잔상을 모두 걷어냅니다 (모드 비활성화·설정 끄기).</summary>
        public static void Clear()
        {
            try
            {
                foreach (var handle in _live)
                    Recycle(handle);

                _live.Clear();
                _followed.Clear();
            }
            catch
            {
                // 정리 중 예외는 무시합니다.
            }
        }

        /// <summary>모드가 내려갈 때 만들어 둔 오브젝트까지 전부 정리합니다.</summary>
        public static void Dispose()
        {
            Clear();

            _idle.Clear();

            if (_holder != null)
            {
                UnityEngine.Object.Destroy(_holder);
                _holder = null;
            }

            // 머리 머티리얼·도형 텍스처는 HideAndDontSave라 씬이 바뀌어도 남습니다.
            // 여기서 안 지우면 모드를 껐다 켤 때마다 도형 수만큼 쌓입니다.
            foreach (var material in _headMaterials.Values)
            {
                if (material != null)
                    UnityEngine.Object.Destroy(material);
            }

            _headMaterials.Clear();
            BulletHeadShapes.Dispose();

            _camera = null;

            if (_headQuad != null)
            {
                UnityEngine.Object.Destroy(_headQuad);
                _headQuad = null;
            }
        }

        // ── 매 프레임 갱신 ────────────────────────────────────────

        /// <summary>
        /// 총알을 따라가고, 총알이 사라진 잔상은 남은 꼬리가 다 없어질 때까지 두었다가 회수합니다.
        /// </summary>
        private static void TickTrails()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var handle = _live[i];

                // 씬 정리 등으로 잔상 오브젝트가 먼저 사라진 경우. 그냥 두면 매 프레임
                // 파괴된 transform을 건드리다 예외가 나면서 나머지 잔상도 갱신되지 않습니다.
                if (handle.Go == null || handle.Trail == null)
                {
                    if (!ReferenceEquals(handle.Follow, null))
                    {
                        _followed.Remove(handle.Follow);
                        handle.Follow = null;
                    }

                    _live.RemoveAt(i);
                    continue;
                }

                if (!ReferenceEquals(handle.Follow, null))
                {
                    // Unity의 == 는 파괴된 오브젝트도 null로 봅니다 — 여기서는 그게 맞는 판정입니다.
                    var projectile = handle.Follow;
                    bool alive = projectile != null && projectile.gameObject.activeInHierarchy;

                    if (alive)
                    {
                        handle.Go.transform.position = projectile!.transform.position;
                        UpdateHead(handle, projectile);
                    }
                    else
                    {
                        BeginFade(handle);
                    }
                }

                if (ReferenceEquals(handle.Follow, null) && Time.time >= handle.RecycleAt)
                {
                    Recycle(handle);
                    _live.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 지금 든 총의 탄환 정보를 설정 창에 보여 주기 위해 낮은 빈도로 읽습니다.
        ///
        /// 스케일이 걸리지 않은 시간을 씁니다. 설정 창이 떠 있는 동안 게임은 시간이 멈춘 상태라
        /// <c>Time.time</c>은 아예 흐르지 않고, 그러면 창에 뜬 탄약 정보가 영원히 갱신되지 않습니다.
        /// </summary>
        private static void TickStatus()
        {
            if (Time.unscaledTime < _nextStatusTime)
                return;

            _nextStatusTime = Time.unscaledTime + StatusInterval;

            try
            {
                var player = CharacterMainControl.Main;
                var holder = player != null ? player.agentHolder : null;
                var agent = holder != null ? holder.CurrentHoldItemAgent : null;
                var gun = agent != null ? agent.GetComponent<ItemAgent_Gun>() : null;
                Item? ammo = gun != null ? gun.BulletItem : null;

                if (ammo == null)
                {
                    SetAmmo("-", -1);
                    return;
                }

                SetAmmo(WeaponHelper.GetDisplayName(ammo), WeaponHelper.GetQuality(ammo));
            }
            catch
            {
                SetAmmo("-", -1);
            }
        }

        private static void SetAmmo(string name, int quality)
        {
            if (CurrentAmmoQuality == quality && string.Equals(CurrentAmmoName, name, StringComparison.Ordinal))
                return;

            CurrentAmmoName = name;
            CurrentAmmoQuality = quality;
            AmmoRevision++;
        }

        private static Camera? _camera;

        /// <summary>
        /// 머리 판을 그릴 기준 카메라.
        ///
        /// <c>Camera.main</c>은 태그 검색이라 매 프레임 부르면 부담이 됩니다. 한 번 찾아
        /// 두고, 씬이 바뀌어 사라지면 그때 다시 찾습니다.
        /// </summary>
        private static Camera? MainCamera()
        {
            if (_camera != null)
                return _camera;

            _camera = Camera.main;
            return _camera;
        }

        /// <summary>
        /// 머리 판을 카메라 쪽으로 눕히고, 긴 쪽을 진행 방향에 맞춥니다.
        ///
        /// 판의 로컬 x가 진행 방향, y가 굵기 방향입니다(<see cref="HeadQuad"/>).
        /// 진행 방향을 카메라 평면에 투영해서 x로 삼으면, 총알이 화면 어느 쪽으로 날아가든
        /// 도형이 찌그러지지 않고 방향만 따라 돕니다.
        /// </summary>
        private static void UpdateHead(TrailHandle handle, Projectile projectile)
        {
            var head = handle.HeadRenderer;
            if (head == null || !head.enabled || handle.HeadGo == null)
                return;

            var camera = MainCamera();
            if (camera == null)
                return;

            // 판이 바라볼 방향 = 카메라 쪽
            Vector3 toCamera = -camera.transform.forward;

            Vector3 along = Vector3.ProjectOnPlane(projectile.transform.forward, toCamera);

            // 총알이 카메라를 정면으로 향해 날아오면 투영이 0이 됩니다. 그때는
            // 방향이랄 것이 없으므로 화면 가로를 씁니다.
            if (along.sqrMagnitude < 0.000001f)
                along = camera.transform.right;
            else
                along.Normalize();

            // LookRotation(forward, up)은 right = cross(up, forward)로 만듭니다.
            // up을 이렇게 잡으면 right가 정확히 along이 됩니다.
            Vector3 up = Vector3.Cross(toCamera, along);

            var t = handle.HeadGo.transform;
            t.rotation = Quaternion.LookRotation(toCamera, up);
            t.localScale = new Vector3(handle.HeadLength, handle.HeadWidth, 1f);
        }

        // ── 잔상 풀 ──────────────────────────────────────────────

        private static TrailHandle? Rent()
        {
            EnsureHolder();
            if (_holder == null)
                return null;

            while (_idle.Count > 0)
            {
                var reused = _idle.Pop();
                if (reused.Go != null && reused.Trail != null)
                    return reused;
            }

            var go = new GameObject(TrailName);
            go.transform.SetParent(_holder.transform, false);
            go.SetActive(false);

            var trail = go.AddComponent<TrailRenderer>();
            SetupTrail(trail);
            trail.minVertexDistance = 0.02f;

            // 머리는 자식 오브젝트에 답니다. 부모(=총알 위치)를 따라가므로 위치는
            // 저절로 맞고, 회전만 매 프레임 카메라 쪽으로 돌리면 됩니다.
            var headGo = new GameObject(HeadName);
            headGo.transform.SetParent(go.transform, false);

            headGo.AddComponent<MeshFilter>().sharedMesh = HeadQuad;

            var headRenderer = headGo.AddComponent<MeshRenderer>();
            headRenderer.receiveShadows = false;
            headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            headRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            headRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // 같은 자리에 겹쳐 그리므로 머리를 위로 올립니다. 가산 합성에서는 순서가
            // 결과에 영향을 주지 않지만, 일반 알파 합성에서는 꼬리가 머리를 덮습니다.
            headRenderer.sortingOrder = 1;

            var stampGo = new GameObject(StampName);
            stampGo.transform.SetParent(go.transform, false);

            var stamps = stampGo.AddComponent<ParticleSystem>();
            var stampRenderer = stampGo.GetComponent<ParticleSystemRenderer>();
            SetupStamps(stamps, stampRenderer);

            return new TrailHandle
            {
                Go = go,
                Trail = trail,
                HeadGo = headGo,
                HeadRenderer = headRenderer,
                Stamps = stamps,
                StampRenderer = stampRenderer,
            };
        }

        /// <summary>자국 파티클의 고정 설정. 매 발 바뀌지 않는 것만 여기서 잡습니다.</summary>
        private static void SetupStamps(ParticleSystem stamps, ParticleSystemRenderer renderer)
        {
            var main = stamps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = MaxStampsPerBullet;

            // 반드시 월드입니다. 로컬이면 자국이 총알을 따라다녀서
            // "지나간 자리에 남는다"가 성립하지 않습니다.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            var emission = stamps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = stamps.shape;
            shape.enabled = false;

            // 시간이 지나며 사라지도록. 색은 매 발 프로필 색으로 덮어씁니다.
            var overLifetime = stamps.colorOverLifetime;
            overLifetime.enabled = true;

            var sizeOverLifetime = stamps.sizeOverLifetime;
            sizeOverLifetime.enabled = false;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.sortingOrder = 0;
        }

        private static Mesh? _headQuad;

        /// <summary>
        /// 머리 판 메시.
        ///
        /// 기준점을 <b>앞쪽 끝</b>에 둡니다(x는 -1~0). 이러면 길이를 늘려도 총알이 있는
        /// 앞머리는 제자리에 있고 뒤로만 자랍니다 — 가운데 기준이면 길이를 키울 때
        /// 머리가 총알보다 앞으로 튀어나갑니다.
        ///
        /// UV는 도형 텍스처와 같은 약속입니다 — u가 진행 방향(1이 앞), v가 굵기.
        /// </summary>
        private static Mesh HeadQuad
        {
            get
            {
                if (_headQuad != null)
                    return _headQuad;

                var mesh = new Mesh
                {
                    name = "WeaponAura_BulletHeadQuad",
                    hideFlags = HideFlags.HideAndDontSave,
                };

                mesh.vertices = new[]
                {
                    new Vector3(-1f, -0.5f, 0f),
                    new Vector3(0f, -0.5f, 0f),
                    new Vector3(0f, 0.5f, 0f),
                    new Vector3(-1f, 0.5f, 0f),
                };

                mesh.uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                };

                // 정점 색을 무시하는 셰이더가 섞여 있어도 흰색이면 결과가 같습니다.
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();

                _headQuad = mesh;
                return _headQuad;
            }
        }

        /// <summary>꼬리와 머리가 공유하는 TrailRenderer 기본 설정.</summary>
        private static void SetupTrail(TrailRenderer trail)
        {
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.autodestruct = false;
            trail.receiveShadows = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            trail.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static void Configure(TrailHandle handle, BulletTrailProfile profile)
        {
            var trail = handle.Trail;

            // sharedMaterial로 넣습니다. material로 읽으면 Unity가 사본을 만들어서
            // 총알 한 발마다 머티리얼이 하나씩 쌓입니다.
            trail.sharedMaterial = profile.additive ? GetAdditiveMaterial() : GetBlendMaterial();

            // 꼬리가 0초면 TrailRenderer가 아무것도 그리지 않습니다.
            trail.time = Mathf.Max(0.02f, profile.length);
            trail.startWidth = Mathf.Max(0.001f, profile.startWidth);
            trail.endWidth = Mathf.Max(0f, profile.endWidth);

            // TrailRenderer는 값을 자기 쪽으로 복사하므로 같은 Gradient 인스턴스를 돌려 써도 됩니다.
            trail.colorGradient = GetGradient(profile);

            ConfigureHead(handle, profile);
            ConfigureStamps(handle, profile);
        }

        /// <summary>
        /// 자국 설정.
        ///
        /// 선 방식이면 꼬리가 그리고 자국은 쉽니다. 자국 방식이면 반대로 꼬리를 끕니다 —
        /// 둘 다 켜면 선 위에 도형이 겹쳐서 무엇을 고른 것인지 알 수 없습니다.
        /// </summary>
        private static void ConfigureStamps(TrailHandle handle, BulletTrailProfile profile)
        {
            var stamps = handle.Stamps;
            if (stamps == null || handle.StampRenderer == null)
                return;

            bool stamp = profile.style == BulletTrailStyle.Stamp && profile.stampSize > 0.0001f;

            handle.Trail.enabled = !stamp;
            handle.StampRenderer.enabled = stamp;

            if (!stamp)
            {
                if (stamps.isPlaying)
                    stamps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            handle.StampRenderer.sharedMaterial =
                GetHeadMaterial(BulletHeadShapes.Resolve(profile.stampShape, profile.stampTextureName),
                    profile.additive);

            var main = stamps.main;
            main.startLifetime = Mathf.Max(0.05f, profile.stampLife);
            main.startSize = profile.stampSize;

            BulletTrailShading.Resolve(profile.colorStart, profile.intensity, profile.alpha,
                out Color color, out float alpha);

            float shaderScale = ShaderColorScale(profile.additive);
            main.startColor = new Color(color.r * shaderScale, color.g * shaderScale,
                color.b * shaderScale, alpha * shaderScale);

            var emission = stamps.emission;
            emission.rateOverDistance = Mathf.Clamp(profile.stampRate, 0f, MaxStampRate);

            // 머리 쪽 색에서 꼬리 쪽 색으로 넘어가며 사라지게 합니다.
            var overLifetime = stamps.colorOverLifetime;
            overLifetime.color = GetStampGradient(profile);
        }

        private static BulletTrailProfile? _stampGradientProfile;
        private static Gradient? _stampGradientCache;
        private static int _stampGradientHash;

        private static Gradient GetStampGradient(BulletTrailProfile profile)
        {
            int hash = profile.colorStart.GetHashCode();
            hash = (hash * 397) ^ profile.colorEnd.GetHashCode();

            if (_stampGradientCache != null && ReferenceEquals(_stampGradientProfile, profile)
                && _stampGradientHash == hash)
                return _stampGradientCache;

            // 자국은 startColor가 이미 밝기·투명도를 담고 있습니다. 여기서는 색조 변화와
            // 사라짐만 다룹니다 — 양쪽에서 밝기를 곱하면 두 번 곱해집니다.
            float startPeak = Mathf.Max(profile.colorStart.r,
                Mathf.Max(profile.colorStart.g, profile.colorStart.b));
            float endPeak = Mathf.Max(profile.colorEnd.r,
                Mathf.Max(profile.colorEnd.g, profile.colorEnd.b));

            Color start = startPeak > 0.0001f
                ? new Color(profile.colorStart.r / startPeak, profile.colorStart.g / startPeak,
                    profile.colorStart.b / startPeak, 1f)
                : Color.white;

            Color end = endPeak > 0.0001f
                ? new Color(profile.colorEnd.r / endPeak, profile.colorEnd.g / endPeak,
                    profile.colorEnd.b / endPeak, 1f)
                : Color.white;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0f, 1f),
                });

            _stampGradientCache = gradient;
            _stampGradientProfile = profile;
            _stampGradientHash = hash;
            return gradient;
        }

        /// <summary>
        /// 총알 머리 설정.
        ///
        /// 원본 궤적을 숨기지 않는 동안에는 그리지 않습니다. 원본 대시와 우리 머리가
        /// 같은 자리에 겹쳐서 두 겹으로 보이기 때문입니다.
        /// </summary>
        private static void ConfigureHead(TrailHandle handle, BulletTrailProfile profile)
        {
            var head = handle.HeadRenderer;
            if (head == null)
                return;

            bool draw = BulletTrailSettings.HideVanillaTrail && profile.headWidth > 0.0001f;

            head.enabled = draw;
            if (!draw)
                return;

            head.sharedMaterial = GetHeadMaterial(profile, profile.additive);

            handle.HeadWidth = profile.headWidth;
            handle.HeadLength = profile.headWidth * Mathf.Max(0.2f, profile.headAspect);

            ApplyHeadEmission(head, profile);
        }

        /// <summary>
        /// 원본 총알(<c>Lazer</c> 셰이더)의 <c>_EmissionColor</c> 최대 채널값.
        /// 실측 (4.579, 1.525, 0) — 채널이 1을 한참 넘는 HDR 값입니다.
        /// </summary>
        private const float VanillaHeadEmission = 4.579f;

        /// <summary>이 밝기에서 원본과 같은 세기가 나오도록 맞춥니다 (= 기본 headIntensity).</summary>
        private const float HeadIntensityReference = 1.35f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static MaterialPropertyBlock? _headBlock;

        /// <summary>
        /// 머리 밝기를 HDR 발광으로 넣습니다.
        ///
        /// 정점 색만으로는 원본을 따라갈 수 없습니다. 정점 색은 1에서 천장에 막히는데,
        /// 원본은 발광값 4.58로 블룸을 태워서 그만큼 또렷합니다(실측). 그래서 같은 수단을 씁니다.
        ///
        /// 머티리얼이 아니라 <see cref="MaterialPropertyBlock"/>에 넣습니다 — 머티리얼은
        /// 도형이 같은 등급끼리 공유하므로, 거기에 색을 쓰면 한 등급을 만질 때 다른 등급의
        /// 총알까지 같이 바뀝니다.
        ///
        /// 꼬리는 건드리지 않습니다. 이미 쓰고 있는 사람의 잔상 밝기가 업데이트만으로
        /// 달라지면 안 됩니다.
        /// </summary>
        /// <summary>
        /// 머리 밝기를 실제 발광값으로 환산합니다.
        ///
        /// 설정 창 미리보기도 이 함수를 씁니다 — 미리보기만 따로 계산하면 화면에서
        /// 하얗게 뜬 머리가 미리보기에서는 멀쩡한 색으로 보입니다.
        /// </summary>
        public static float HeadEmissionGain(float headIntensity)
            => Mathf.Max(0f, headIntensity) / HeadIntensityReference * VanillaHeadEmission;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        private static void ApplyHeadEmission(MeshRenderer head, BulletTrailProfile profile)
        {
            _headBlock ??= new MaterialPropertyBlock();

            head.GetPropertyBlock(_headBlock);

            var color = profile.ResolveHeadColor();

            // 색조를 지키려면 세 채널에 같은 배율을 곱해야 합니다. 가장 밝은 채널을
            // 1로 올려 놓고, 실제 세기는 아래 gain이 담당합니다.
            float peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (peak > 0.0001f)
                color = new Color(color.r / peak, color.g / peak, color.b / peak, 1f);

            // 판에는 그라디언트가 없습니다(궤적이 아니니까요). 기본 색을 직접 넣어 줘야
            // 도형 텍스처의 알파가 그대로 실루엣으로 나옵니다.
            _headBlock.SetColor(BaseColorId, color);
            _headBlock.SetColor(ColorId, color);
            _headBlock.SetColor(TintColorId, color);

            float gain = HeadEmissionGain(profile.headIntensity);

            _headBlock.SetColor(EmissionColorId,
                new Color(color.r * gain, color.g * gain, color.b * gain, 1f));

            head.SetPropertyBlock(_headBlock);
        }

        private static BulletTrailProfile? _gradientProfile;
        private static Gradient? _gradientCache;
        private static int _gradientHash;

        /// <summary>
        /// 그라디언트는 색 네 값과 합성 방식에만 의존합니다. 연사 중에는 같은 프로필이 계속
        /// 들어오므로 마지막 결과 하나만 들고 있어도 매 발 새로 만드는 일이 없어집니다.
        /// </summary>
        private static Gradient GetGradient(BulletTrailProfile profile)
        {
            int hash = profile.colorStart.GetHashCode();
            hash = (hash * 397) ^ profile.colorEnd.GetHashCode();
            hash = (hash * 397) ^ profile.alpha.GetHashCode();
            hash = (hash * 397) ^ profile.intensity.GetHashCode();
            hash = (hash * 397) ^ profile.additive.GetHashCode();

            if (_gradientCache != null && ReferenceEquals(_gradientProfile, profile) && _gradientHash == hash)
                return _gradientCache;

            _gradientCache = BuildGradient(profile);
            _gradientProfile = profile;
            _gradientHash = hash;
            return _gradientCache;
        }

        /// <summary>
        /// 머리에서 꼬리로 가면서 색이 바뀌고 투명해지는 그라디언트.
        ///
        /// 색·알파는 <see cref="BulletTrailShading"/>이 계산합니다 (미리보기와 같은 식).
        /// 여기에 셰이더 보정만 얹습니다.
        /// </summary>
        private static Gradient BuildGradient(BulletTrailProfile profile)
        {
            BulletTrailShading.Resolve(profile.colorStart, profile.intensity, profile.alpha,
                out Color start, out float startAlpha);
            BulletTrailShading.Resolve(profile.colorEnd, profile.intensity, profile.alpha,
                out Color end, out float endAlpha);

            // 두 끝의 알파 보정치가 다를 이유는 없지만(같은 밝기·투명도), 색의 최대 채널이
            // 다르면 값이 갈립니다. 잔상 전체의 세기는 하나여야 하므로 낮은 쪽에 맞춥니다.
            float alpha = Mathf.Min(startAlpha, endAlpha);

            float shaderScale = ShaderColorScale(profile.additive);
            start *= shaderScale;
            end *= shaderScale;
            alpha *= shaderScale;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(alpha, 0f),
                    new GradientAlphaKey(alpha * 0.55f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                });

            return gradient;
        }

        private static float? _additiveColorScale;
        private static float? _blendColorScale;

        /// <summary>
        /// 셰이더에 따른 색 보정.
        ///
        /// 레거시 <c>Particles/Additive</c>는 최종 색을 <c>2.0 × 정점색 × 틴트 × 텍스처</c>로 냅니다.
        /// 그대로 넣으면 채널값이 0.5만 넘어도 화면에서 1에 붙어 흰색이 됩니다 —
        /// 색을 아무리 골라도 잔상이 하얗게 나오던 이유입니다. 미리 절반으로 눌러 두면
        /// 셰이더를 거친 결과가 고른 색과 같아집니다.
        /// </summary>
        private static float ShaderColorScale(bool additive)
        {
            if (additive)
            {
                _additiveColorScale ??= ComputeShaderColorScale(GetAdditiveMaterial());
                return _additiveColorScale.Value;
            }

            _blendColorScale ??= ComputeShaderColorScale(GetBlendMaterial());
            return _blendColorScale.Value;
        }

        private static float ComputeShaderColorScale(Material? material)
        {
            string name = material != null && material.shader != null ? material.shader.name : "";
            return name.IndexOf("Particles/Additive", StringComparison.OrdinalIgnoreCase) >= 0
                ? 0.5f
                : 1f;
        }

        /// <summary>총알에서 떼어내고, 남은 꼬리가 사라질 때까지만 살려 둡니다.</summary>
        private static void BeginFade(TrailHandle handle)
        {
            if (!ReferenceEquals(handle.Follow, null))
            {
                _followed.Remove(handle.Follow);
                handle.Follow = null;
            }

            handle.Trail.emitting = false;

            // 총알이 사라지는 순간 머리도 같이 사라져야 합니다. 꼬리처럼 남겨 두면
            // 명중 지점에 총알이 잠깐 멈춰 선 것처럼 보입니다.
            if (handle.HeadRenderer != null)
                handle.HeadRenderer.enabled = false;

            float linger = handle.Trail.time;

            if (handle.Stamps != null)
            {
                // 뿌리기만 멈춥니다. 이미 남은 자국은 제자리에서 수명대로 사라져야
                // "지나간 자리"라는 말이 성립합니다.
                if (handle.Stamps.isPlaying)
                    handle.Stamps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                if (handle.StampRenderer != null && handle.StampRenderer.enabled)
                    linger = Mathf.Max(linger, handle.Stamps.main.startLifetime.constant);
            }

            // 남은 것이 다 사라질 때까지 두면 마지막까지 자연스럽게 없어집니다.
            handle.RecycleAt = Time.time + linger + 0.05f;
        }

        private static void Recycle(TrailHandle handle)
        {
            if (!ReferenceEquals(handle.Follow, null))
            {
                _followed.Remove(handle.Follow);
                handle.Follow = null;
            }

            if (handle.Go == null || handle.Trail == null)
                return;

            handle.Trail.emitting = false;
            handle.Trail.Clear();

            if (handle.HeadRenderer != null)
                handle.HeadRenderer.enabled = false;

            if (handle.Stamps != null)
            {
                handle.Stamps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                handle.Stamps.Clear();
            }

            handle.Go.SetActive(false);

            if (_holder != null)
                handle.Go.transform.SetParent(_holder.transform, false);

            _idle.Push(handle);
        }

        private static void EnsureHolder()
        {
            if (_holder != null)
                return;

            _holder = new GameObject(HolderName);
            UnityEngine.Object.DontDestroyOnLoad(_holder);
        }

        // ── 머티리얼 ─────────────────────────────────────────────

        /// <summary>
        /// 머리는 도형마다 텍스처가 다르므로 (도형 × 합성방식)별로 머티리얼을 따로 둡니다.
        ///
        /// 꼬리처럼 하나를 돌려 쓸 수 없습니다 — 같은 머티리얼의 텍스처를 매 발 바꾸면
        /// 화면에 이미 떠 있는 다른 총알들의 모양까지 함께 바뀝니다.
        /// </summary>
        private static readonly Dictionary<(Texture2D, bool), Material> _headMaterials =
            new Dictionary<(Texture2D, bool), Material>();

        private static Material GetHeadMaterial(BulletTrailProfile profile, bool additive)
            => GetHeadMaterial(BulletHeadShapes.Resolve(profile.headShape, profile.headTextureName), additive);

        private static Material GetHeadMaterial(Texture2D texture, bool additive)
        {
            // 텍스처로 키를 잡습니다. 내장 도형과 사용자 도형·PNG가 섞이므로 열거형만으로는
            // 구분이 안 되고, 같은 그림을 고른 등급끼리는 머티리얼을 나눠 쓸 수 있습니다.
            var key = (texture, additive);

            if (_headMaterials.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var material = CreateMaterial(additive);
            material.name = $"WeaponAura_BulletHead_{texture.name}_{(additive ? "Add" : "Blend")}";
            material.mainTexture = texture;

            // 발광을 켜 둡니다. 실제 색은 렌더러마다 MaterialPropertyBlock으로 들어갑니다
            // (<see cref="ApplyHeadEmission"/>). 여기서 켜 두지 않으면 셰이더가 발광 항을
            // 아예 계산하지 않아서 블록에 넣은 값이 무시됩니다.
            if (material.HasProperty("_EmissionEnabled"))
                material.SetFloat("_EmissionEnabled", 1f);

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            _headMaterials[key] = material;
            return material;
        }

        private static Material GetAdditiveMaterial()
        {
            if (_additiveMaterial == null)
                _additiveMaterial = CreateMaterial(additive: true);
            return _additiveMaterial;
        }

        private static Material GetBlendMaterial()
        {
            if (_blendMaterial == null)
                _blendMaterial = CreateMaterial(additive: false);
            return _blendMaterial;
        }

        /// <summary>
        /// 잔상용 머티리얼.
        ///
        /// TrailRenderer는 그라디언트를 정점 색으로 넣기 때문에, 정점 색을 무시하는
        /// 셰이더(URP/Unlit 등)를 쓰면 색이 통째로 단색이 되고 꼬리도 안 사라집니다.
        /// 그래서 파티클/스프라이트 계열만 후보로 둡니다.
        /// </summary>
        private static Material CreateMaterial(bool additive)
        {
            Shader? shader = FindShader(
                "Particles/Additive",
                "Legacy Shaders/Particles/Additive",
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Sprites/Default");

            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                name = additive ? "WeaponAura_BulletTrail_Add" : "WeaponAura_BulletTrail_Blend",
                mainTexture = StreakTexture,
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
                material.SetInt("_DstBlend", additive
                    ? (int)UnityEngine.Rendering.BlendMode.One
                    : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);    // Transparent
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", additive ? 1f : 0f);

            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

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

        /// <summary>
        /// 가운데가 밝고 위아래로 부드럽게 사라지는 띠 텍스처.
        /// 흰색 1×1을 쓰면 꼬리 가장자리가 칼같이 잘려서 종잇조각처럼 보입니다.
        /// </summary>
        private static Texture2D StreakTexture
        {
            get
            {
                if (_streakTexture != null)
                    return _streakTexture;

                const int height = 32;

                var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
                {
                    name = "WeaponAura_BulletTrail_Streak",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color[height];
                for (int y = 0; y < height; y++)
                {
                    // 가운데(0.5)에서 1, 위아래 끝에서 0으로 떨어지는 부드러운 감쇠
                    float d = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y] = new Color(1f, 1f, 1f, a * a);
                }

                texture.SetPixels(pixels);
                texture.Apply();

                _streakTexture = texture;
                return _streakTexture;
            }
        }
    }
}
