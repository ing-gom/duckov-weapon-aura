using System;
using UnityEngine;
using WeaponAura.Systems;

namespace WeaponAura.UI
{
    /// <summary>
    /// 튜닝 패널 안에 실제 무기 3D 모델을 보여줍니다.
    ///
    /// 무기를 복제하는 대신 <b>전용 카메라로 진짜 무기를 찍어</b> RenderTexture에 담습니다.
    /// - 복제본과 실물이 어긋날 일이 없습니다 (지금 손에 든 그 무기, 그 오라 그대로)
    /// - 오라 시스템을 건드리지 않으므로 미리보기 때문에 본편이 바뀌지 않습니다
    /// </summary>
    public class WeaponAuraPreview
    {
        private const int TextureSize = 320;

        private Camera? _camera;
        private RenderTexture? _texture;
        private float _yaw = 35f;
        private float _pitch = 18f;
        private float _zoom = 1f;

        /// <summary>사용자가 드래그로 돌린 각도</summary>
        public void Rotate(float deltaYaw, float deltaPitch)
        {
            _yaw += deltaYaw;
            _pitch = Mathf.Clamp(_pitch + deltaPitch, -70f, 70f);
        }

        public void Zoom(float delta)
        {
            _zoom = Mathf.Clamp(_zoom * (1f + delta), 0.5f, 2.5f);
        }

        public bool AutoRotate { get; set; } = true;

        /// <summary>
        /// 현재 무기를 찍어 텍스처를 갱신합니다. 무기가 없으면 null을 돌려줍니다.
        /// OnGUI의 Repaint 단계에서만 호출해야 합니다 (카메라 렌더는 레이아웃 단계에서 하면 안 됩니다).
        /// </summary>
        public Texture? Render()
        {
            try
            {
                var target = FindWeaponTransform(out Bounds bounds);
                if (target == null)
                    return null;

                EnsureResources();
                if (_camera == null || _texture == null)
                    return null;

                // 일시정지 중에는 Time.deltaTime이 0이라 회전이 멈춥니다.
                if (AutoRotate)
                    _yaw += Time.unscaledDeltaTime * 25f;

                // 무기 바운즈에 맞춰 카메라를 배치합니다.
                float radius = Mathf.Max(0.15f, bounds.extents.magnitude);
                float distance = radius * 3.2f / _zoom;

                var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                _camera.transform.position = bounds.center + rotation * new Vector3(0f, 0f, -distance);
                _camera.transform.rotation = rotation;
                _camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 3f);
                _camera.farClipPlane = distance + radius * 6f;

                _camera.Render();
                return _texture;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 미리보기 렌더 실패: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
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

        private void EnsureResources()
        {
            if (_texture == null)
            {
                _texture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "WeaponAura_Preview",
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
                _camera.enabled = false;                 // 수동 Render()만 사용
                _camera.targetTexture = _texture;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.03f, 0.035f, 0.05f, 1f);
                _camera.fieldOfView = 35f;
                _camera.orthographic = false;

                // 무기와 오라가 올라가는 레이어만 찍습니다 (지형·UI 제외)
                int characterLayer = LayerMask.NameToLayer("Character");
                _camera.cullingMask = characterLayer >= 0 ? 1 << characterLayer : ~0;
            }

            // 게임 화면의 후처리(블룸 등)를 그대로 받게 맞춥니다.
            // 맨 카메라로 두면 같은 오라가 게임보다 어둡고 작게 보입니다.
            PreviewCameraSetup.Match(_camera, "진단", _camera.gameObject.layer);
        }

        /// <summary>현재 들고 있는 무기의 위치·크기를 구합니다.</summary>
        private static Transform? FindWeaponTransform(out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one * 0.3f);

            var player = CharacterMainControl.Main;
            var holder = player != null ? player.agentHolder : null;
            var agent = holder != null ? holder.CurrentHoldItemAgent : null;
            if (agent == null)
                return null;

            bool initialized = false;

            foreach (var renderer in agent.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer == null || renderer is LineRenderer || renderer is TrailRenderer)
                    continue;

                var b = renderer.bounds;
                if (b.size.sqrMagnitude <= 1e-8f)
                    continue;

                // 레이저처럼 멀리 뻗는 렌더러는 제외 (미리보기 프레이밍이 망가집니다)
                if (b.size.x > 2f || b.size.y > 2f || b.size.z > 2f)
                    continue;

                if (!initialized)
                {
                    bounds = b;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return initialized ? agent.transform : null;
        }
    }
}
