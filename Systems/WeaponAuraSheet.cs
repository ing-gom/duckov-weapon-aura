using System;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 무기 표면에서 바깥으로 뻗어나가는 "면"(셸) 한 겹.
    ///
    /// 등배 확대가 아니라 <b>법선 방향 오프셋</b>이라, 총열처럼 얇은 부분도 같은 두께로 밀려나가
    /// 무기 형태를 유지한 채 번집니다. 파티클 알갱이가 아닌 연속된 면이라 오로라 커튼·화염 막이 됩니다.
    ///
    /// 셸의 원본은 두 가지입니다.
    ///  - 무기 메시를 읽을 수 있으면 그 메시 자체 (진짜 총 표면)
    ///  - 읽을 수 없으면 바운딩 박스 비율의 초타원체 (sheetBoxiness로 각을 조절)
    /// </summary>
    public class WeaponAuraSheet : MonoBehaviour
    {
        /// <summary>이 이상 무거운 메시는 매 프레임 갱신이 부담이라 폴백 셸을 씁니다.</summary>
        public const int MaxSourceVertices = 3000;

        // 림과 동심원 밴드를 정점 색으로 계산하므로, 너무 성기면 각져 보입니다.
        // 벤치는 프래그먼트 셰이더라 매끈하지만 여기선 해상도로 메웁니다. (20×32 = 693정점)
        private const int Rings = 20;
        private const int Segments = 32;

        private Mesh? _mesh;
        private MeshRenderer? _renderer;

        private Vector3[] _baseVertices = Array.Empty<Vector3>();
        private Vector3[] _baseNormals = Array.Empty<Vector3>();
        private Vector3[] _vertices = Array.Empty<Vector3>();
        private Color[] _colors = Array.Empty<Color>();

        private float _phase;
        /// <summary>중심에서 가장 먼 정점까지의 거리 — 동심원 위상 정규화에 씁니다.</summary>
        private float _maxRadius = 1f;

        /// <summary>무기 메시를 원본으로 쓰고 있는지</summary>
        public bool UsesWeaponMesh { get; private set; }

        /// <summary>겹마다 어긋나게 퍼지도록 위상을 지정합니다.</summary>
        public void SetPhase(float phase) => _phase = phase;

        // ── 실루엣 모드 ────────────────────────────────────────
        // 메시가 읽기 불가(isReadable=false)여도 "그리는" 것은 됩니다.
        // 그래서 무기 메시를 그대로 한 벌 더 렌더링하고 크기만 키워서
        // 총 모양 그대로 부풀어 오르는 껍질을 만듭니다.
        private readonly List<MeshRenderer> _silhouetteRenderers = new List<MeshRenderer>();
        private readonly List<Transform> _silhouettePivots = new List<Transform>();
        /// <summary>부품별 축 반지름 — 축마다 같은 두께로 부풀리기 위해 씁니다.</summary>
        private readonly List<Vector3> _silhouetteExtents = new List<Vector3>();
        private MaterialPropertyBlock? _propertyBlock;
        private bool _isSilhouette;

        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// 무기 메시를 그대로 복제해 총 모양 껍질을 만듭니다.
        /// 정점을 못 읽어도 되므로 읽기 불가 메시에서도 실루엣이 유지됩니다.
        /// </summary>
        public bool BuildSilhouette(IList<Renderer> weaponRenderers, Transform auraRoot, Material material)
        {
            try
            {
                transform.SetParent(auraRoot, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;

                float maxExtent = 0.01f;

                foreach (var weaponRenderer in weaponRenderers)
                {
                    if (weaponRenderer == null)
                        continue;

                    // SkinnedMeshRenderer는 정점이 본(bone)으로 변형되므로 MeshRenderer로 복제하면
                    // 바인드 포즈(엉뚱한 위치·자세)로 그려집니다. 그런 무기는 실루엣을 포기하고
                    // 호출부에서 박스 셸로 폴백하게 둡니다.
                    if (weaponRenderer is SkinnedMeshRenderer)
                        continue;

                    Mesh? mesh = GetSharedMesh(weaponRenderer);
                    if (mesh == null || mesh.vertexCount <= 0)
                        continue;

                    Vector3 center = mesh.bounds.center;
                    Vector3 rendererScale = weaponRenderer.transform.lossyScale;

                    // 부품마다 피벗을 그 메시 중심에 둡니다.
                    // 그래야 스케일을 키울 때 한쪽으로 쏠리지 않고 제자리에서 부풀어 오릅니다.
                    var pivot = new GameObject($"Pivot_{weaponRenderer.gameObject.name}");
                    pivot.transform.SetParent(transform, false);
                    pivot.transform.position = weaponRenderer.transform.TransformPoint(center);
                    pivot.transform.rotation = weaponRenderer.transform.rotation;
                    pivot.transform.localScale = Vector3.one;

                    var meshObject = new GameObject("Silhouette");
                    meshObject.transform.SetParent(pivot.transform, false);
                    meshObject.transform.localPosition = -Vector3.Scale(center, rendererScale);
                    meshObject.transform.localRotation = Quaternion.identity;
                    meshObject.transform.localScale = rendererScale;

                    var filter = meshObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;

                    var mr = meshObject.AddComponent<MeshRenderer>();

                    // 서브메시가 여러 개면 머티리얼도 그 수만큼 있어야 전부 그려집니다.
                    // 하나만 넣으면 첫 서브메시만 나오고 나머지는 통째로 빠집니다.
                    if (mesh.subMeshCount > 1)
                    {
                        var materials = new Material[mesh.subMeshCount];
                        for (int i = 0; i < materials.Length; i++)
                            materials[i] = material;
                        mr.sharedMaterials = materials;
                    }
                    else
                    {
                        mr.sharedMaterial = material;
                    }
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                    // hurtVisual 간섭을 피하려고 CharacterSubVisuals에 등록하지 않으므로,
                    // 무기와 같은 레이어를 직접 맞춰 줍니다(같은 카메라에 그려지도록).
                    pivot.layer = weaponRenderer.gameObject.layer;
                    meshObject.layer = weaponRenderer.gameObject.layer;

                    _silhouettePivots.Add(pivot.transform);
                    _silhouetteRenderers.Add(mr);

                    // 확대 기준 반지름.
                    // 메시 바운즈가 비정상적으로 큰 무기가 있어서(MCX Spear는 10m가 넘게 잡힘)
                    // 그 값을 그대로 쓰면 배율이 1에 가까워져 셸이 총 안쪽에 파묻힙니다.
                    // 실제로 화면에 차지하는 크기인 월드 바운즈를 우선 쓰고, 상한을 둡니다.
                    float worldExtent = weaponRenderer.bounds.extents.magnitude;
                    float meshExtent = Vector3.Scale(mesh.bounds.extents, rendererScale).magnitude;
                    float extent = worldExtent > 0.001f ? Mathf.Min(worldExtent, meshExtent) : meshExtent;

                    maxExtent = Mathf.Max(maxExtent, Mathf.Clamp(extent, 0.05f, 1f));

                    // 축별 반지름(로컬 기준). 축마다 같은 "두께"로 부풀리려면 이 값이 필요합니다.
                    // 균등 확대만 쓰면 활처럼 긴 무기가 길이 방향으로만 늘어나 형태가 깨집니다.
                    Vector3 axisExtents = Vector3.Scale(mesh.bounds.extents, rendererScale);
                    _silhouetteExtents.Add(new Vector3(
                        Mathf.Clamp(Mathf.Abs(axisExtents.x), 0.02f, 1f),
                        Mathf.Clamp(Mathf.Abs(axisExtents.y), 0.02f, 1f),
                        Mathf.Clamp(Mathf.Abs(axisExtents.z), 0.02f, 1f)));
                }

                if (_silhouetteRenderers.Count == 0)
                    return false;

                _propertyBlock = new MaterialPropertyBlock();
                _maxRadius = maxExtent;
                _isSilhouette = true;
                UsesWeaponMesh = true;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 실루엣 껍질 생성 실패: {ex.Message}");
                return false;
            }
        }

        private static Mesh? GetSharedMesh(Renderer renderer)
        {
            try
            {
                if (renderer is SkinnedMeshRenderer skinned)
                    return skinned.sharedMesh;

                var filter = renderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>실루엣 모드 갱신 — 정점을 못 만지므로 스케일과 색만 움직입니다.</summary>
        private void TickSilhouette(float time, WeaponAuraProfile profile, float intensity)
        {
            if (_silhouetteRenderers.Count == 0 || _propertyBlock == null)
                return;

            float period = Mathf.Max(0.1f, profile.sheetPeriod);
            float u = Mathf.Repeat(time / period + _phase, 1f);

            // 축마다 "같은 두께"로 부풀립니다.
            // 균등 확대(scale = 1 + r)를 쓰면 긴 축이 절대량으로 훨씬 많이 늘어나서
            // 활·석궁처럼 길쭉한 무기는 길이 방향으로만 쭉 빠져 형태가 깨집니다.
            // 축별로 1 + 두께/반지름 을 쓰면 어느 축이든 대략 같은 거리만큼 밀려납니다.
            float thickness = Mathf.Max(0f, profile.sheetSpread) * u;

            for (int i = 0; i < _silhouettePivots.Count; i++)
            {
                var pivot = _silhouettePivots[i];
                if (pivot == null)
                    continue;

                Vector3 extents = i < _silhouetteExtents.Count
                    ? _silhouetteExtents[i]
                    : Vector3.one * _maxRadius;

                pivot.localScale = new Vector3(
                    AxisGrow(thickness, extents.x),
                    AxisGrow(thickness, extents.y),
                    AxisGrow(thickness, extents.z));
            }

            float alpha = FadeAt(profile, u) * profile.alpha * Mathf.Max(0f, intensity) * LayerDamping(profile);

            // 동심원 파동은 밝기 맥동으로 대체합니다 (정점 단위 밴딩 불가)
            if (profile.sheetRings > 0.001f)
            {
                float wave = Mathf.Sin((u * profile.sheetRings - time * profile.sheetRingSpeed + _phase) * 6.2831853f);
                alpha *= 0.45f + 0.55f * (0.5f + 0.5f * wave);
            }

            // 색은 시간(u)이 아니라 겹 위상(_phase)으로 섞습니다.
            // u로 섞으면 주기마다 색A↔색B를 훑어서 오라 색이 계속 변하고,
            // 그러면 색으로 등급을 구분할 수 없습니다. 겹별로 고정하면
            // 각 티어가 일정한 색 정체성을 유지하면서 겹 사이 그라디언트만 남습니다.
            Color color = Color.Lerp(profile.colorA, profile.colorB, _phase) * Mathf.Max(0f, profile.colorIntensity);

            // 알파를 색에 미리 곱해 둡니다(premultiplied).
            // 머티리얼이 One+One 가산이라 셰이더가 알파를 무시해도 밝기가 정확히 나옵니다.
            color = new Color(color.r * alpha, color.g * alpha, color.b * alpha, alpha);

            _propertyBlock.SetColor(TintColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(BaseColorId, color);

            for (int i = 0; i < _silhouetteRenderers.Count; i++)
            {
                if (_silhouetteRenderers[i] != null)
                    _silhouetteRenderers[i].SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>
        /// 셸 한 겹을 만듭니다.
        /// </summary>
        /// <param name="source">무기 메시 (null이거나 읽기 불가면 폴백 셸)</param>
        /// <param name="sourceToLocal">무기 메시 로컬 → 이 오브젝트 로컬 변환</param>
        /// <param name="halfSize">폴백 셸에 쓸 바운딩 박스 절반 크기</param>
        /// <param name="phase">0~1 위상 — 겹마다 어긋나게 퍼지도록</param>
        public bool Build(Mesh? source, Matrix4x4 sourceToLocal, Vector3 halfSize, float boxiness,
            float phase, Material material)
        {
            _phase = phase;

            if (!TryBuildFromWeapon(source, sourceToLocal))
                BuildFallbackShell(halfSize, boxiness);

            if (_mesh == null)
                return false;

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return true;
        }

        /// <summary>매 프레임 갱신 — 컨트롤러가 호출합니다.</summary>
        public void Tick(float time, WeaponAuraProfile profile, float intensity)
        {
            if (profile == null)
                return;

            if (_isSilhouette)
            {
                TickSilhouette(time, profile, intensity);
                return;
            }

            if (_mesh == null)
                return;

            float period = Mathf.Max(0.1f, profile.sheetPeriod);
            float u = Mathf.Repeat(time / period + _phase, 1f);

            // 표면이 법선 방향으로 0 → sheetSpread 만큼 밀려나감
            float offset = Mathf.Max(0f, profile.sheetSpread) * u;
            float alpha = FadeAt(profile, u) * profile.alpha * Mathf.Max(0f, intensity) * LayerDamping(profile);

            float wobble = profile.sheetWobble * Mathf.Max(0f, profile.sheetSpread);
            float wobbleSpeed = profile.sheetWobbleSpeed;
            float noiseScale = Mathf.Max(0.05f, profile.noiseFrequency) * 12f;
            float strength = Mathf.Max(0f, profile.noiseStrength);

            float roundness = Mathf.Clamp01(profile.sheetRoundness);
            float rings = Mathf.Max(0f, profile.sheetRings);
            float ringSpeed = profile.sheetRingSpeed;
            float invRadius = _maxRadius > 0.0001f ? 1f / _maxRadius : 0f;

            // 카메라를 정면으로 보는 면은 옅게, 스치는 가장자리는 진하게 —
            // 이 림 가중이 없으면 가산 합성이라 빛나는 덩어리로 보입니다.
            // Camera.main은 "MainCamera" 태그가 없으면 null이라, 현재 카메라로 폴백합니다.
            // 여기가 null로 남으면 림이 고정 방향으로 계산돼 커튼처럼 안 보입니다.
            Vector3 viewLocal = Vector3.forward;
            var camera = Camera.main != null ? Camera.main : Camera.current;
            if (camera == null && Camera.allCamerasCount > 0)
                camera = Camera.allCameras[0];

            if (camera != null)
                viewLocal = transform.InverseTransformDirection(camera.transform.forward).normalized;

            for (int i = 0; i < _baseVertices.Length; i++)
            {
                Vector3 basePos = _baseVertices[i];
                Vector3 normal = _baseNormals[i];

                // 중심에서 본 반경 방향. roundness가 클수록 표면 법선 대신 이쪽으로 뻗어서
                // 퍼질수록 무기 형태가 풀리고 둥글둥글해집니다.
                Vector3 radial = basePos.sqrMagnitude > 1e-8f ? basePos.normalized : normal;
                Vector3 direction = Vector3.Slerp(normal, radial, roundness);

                float push = offset;
                float bandAlpha = 1f;

                if (rings > 0.001f)
                {
                    // 중심에서의 거리로 위상을 만들어 동심원 파동이 바깥으로 흘러가게
                    float d = basePos.magnitude * invRadius;
                    float wavePhase = d * rings - time * ringSpeed + _phase;
                    float wave = Mathf.Sin(wavePhase * 6.2831853f);

                    push += wave * wobble;
                    bandAlpha = 0.35f + 0.65f * (0.5f + 0.5f * wave);
                }
                else if (wobble > 0.0001f)
                {
                    float n = Mathf.Sin((basePos.x + basePos.z) * noiseScale + time * wobbleSpeed + _phase * 6.283f)
                            * Mathf.Cos(basePos.y * noiseScale * 1.7f - time * wobbleSpeed * 0.8f);
                    push += n * wobble * (0.4f + strength);
                }

                _vertices[i] = basePos + direction * push;

                // 위로 갈수록 색 B — 오로라/화염 그라디언트
                float t = Mathf.Clamp01(basePos.y * 4f + 0.5f);
                Color c = Color.Lerp(profile.colorA, profile.colorB, t) * Mathf.Max(0f, profile.colorIntensity);

                float facing = Mathf.Abs(Vector3.Dot(direction, viewLocal));
                float rim = Mathf.Pow(1f - facing, 1.5f);
                c.a = alpha * bandAlpha * (0.2f + 0.8f * rim);

                _colors[i] = c;
            }

            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }

        /// <summary>
        /// 한 축의 확대 배율. 얇은 축이 과하게 부풀지 않도록 상한을 둡니다.
        /// (칼날처럼 두께 1cm인 축에 20cm를 그대로 적용하면 슬래브가 됩니다)
        /// </summary>
        private static float AxisGrow(float thickness, float extent)
        {
            return Mathf.Clamp(1f + thickness / Mathf.Max(0.02f, extent), 1f, 2.2f);
        }

        /// <summary>
        /// 겹 수에 따른 밝기 보정.
        /// 가산 합성이라 겹이 겹칠수록 밝기가 그대로 누적돼 하얗게 타버립니다.
        /// 겹을 늘려도 전체 밝기가 비슷하게 유지되도록 나눠 줍니다.
        /// </summary>
        private static float LayerDamping(WeaponAuraProfile p)
        {
            int layers = Mathf.Clamp(p.sheetLayers, 1, 8);
            return 1f / Mathf.Sqrt(layers);
        }

        private static float FadeAt(WeaponAuraProfile p, float u)
        {
            float inEnd = Mathf.Clamp(p.fadeIn, 0.001f, 0.98f);
            float outStart = Mathf.Clamp(1f - p.fadeOut, inEnd + 0.001f, 0.999f);
            if (u < inEnd) return u / inEnd;
            if (u > outStart) return 1f - (u - outStart) / (1f - outStart);
            return 1f;
        }

        /// <summary>무기 메시 자체를 셸 원본으로 복사합니다.</summary>
        private bool TryBuildFromWeapon(Mesh? source, Matrix4x4 sourceToLocal)
        {
            try
            {
                if (source == null || !source.isReadable)
                    return false;
                if (source.vertexCount <= 0 || source.vertexCount > MaxSourceVertices)
                    return false;

                var vertices = source.vertices;
                var normals = source.normals;
                var triangles = source.triangles;
                if (triangles.Length < 3)
                    return false;

                bool hasNormals = normals != null && normals.Length == vertices.Length;

                var localVertices = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                    localVertices[i] = sourceToLocal.MultiplyPoint3x4(vertices[i]);

                _mesh = new Mesh { name = "WeaponAura_AuraSheet_Weapon" };
                _mesh.MarkDynamic();
                _mesh.vertices = localVertices;
                _mesh.triangles = triangles;

                if (hasNormals)
                {
                    var localNormals = new Vector3[normals!.Length];
                    for (int i = 0; i < normals.Length; i++)
                        localNormals[i] = sourceToLocal.MultiplyVector(normals[i]).normalized;
                    _mesh.normals = localNormals;
                }
                else
                {
                    _mesh.RecalculateNormals();
                }

                _baseVertices = localVertices;
                _baseNormals = _mesh.normals;
                Finish(localVertices.Length);

                UsesWeaponMesh = true;
                return true;
            }
            catch
            {
                _mesh = null;
                return false;
            }
        }

        /// <summary>
        /// 바운딩 박스 비율의 초타원체. boxiness가 1에 가까울수록 모서리가 서서 총 형태에 가까워집니다.
        /// </summary>
        private void BuildFallbackShell(Vector3 halfSize, float boxiness)
        {
            var half = new Vector3(
                Mathf.Max(0.02f, halfSize.x),
                Mathf.Max(0.02f, halfSize.y),
                Mathf.Max(0.02f, halfSize.z));

            // e가 작을수록 박스에 가까움
            float e = Mathf.Lerp(1f, 0.28f, Mathf.Clamp01(boxiness));

            int vertexCount = (Rings + 1) * (Segments + 1);
            var vertices = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];

            int v = 0;
            for (int r = 0; r <= Rings; r++)
            {
                float phi = Mathf.PI * r / Rings;
                float y = Mathf.Cos(phi);
                float ring = Mathf.Sin(phi);

                for (int s = 0; s <= Segments; s++)
                {
                    float theta = 2f * Mathf.PI * s / Segments;
                    var unit = new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta));

                    vertices[v] = new Vector3(
                        SignPow(unit.x, e) * half.x,
                        SignPow(unit.y, e) * half.y,
                        SignPow(unit.z, e) * half.z);

                    uv[v] = new Vector2((float)s / Segments, 1f - (float)r / Rings);
                    v++;
                }
            }

            var triangles = new int[Rings * Segments * 6];
            int t = 0;
            for (int r = 0; r < Rings; r++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int a = r * (Segments + 1) + s;
                    int b = a + Segments + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                    triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
                }
            }

            _mesh = new Mesh { name = "WeaponAura_AuraSheet_Box" };
            _mesh.MarkDynamic();
            _mesh.vertices = vertices;
            _mesh.uv = uv;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();   // 비등방 스케일 후라 여기서 계산해야 정확합니다

            _baseVertices = vertices;
            _baseNormals = _mesh.normals;
            Finish(vertexCount);

            UsesWeaponMesh = false;
        }

        private void Finish(int vertexCount)
        {
            _vertices = new Vector3[vertexCount];
            _colors = new Color[vertexCount];
            Array.Copy(_baseVertices, _vertices, vertexCount);
            _mesh!.colors = _colors;

            float maxSqr = 0f;
            for (int i = 0; i < _baseVertices.Length; i++)
                maxSqr = Mathf.Max(maxSqr, _baseVertices[i].sqrMagnitude);
            _maxRadius = Mathf.Max(0.01f, Mathf.Sqrt(maxSqr));
        }

        private static float SignPow(float value, float exponent)
        {
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
        }
    }
}
