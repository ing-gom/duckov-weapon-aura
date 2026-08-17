using System;
using UnityEngine;

namespace WeaponAura.UI
{
    /// <summary>
    /// 이펙트 미리보기 무대의 공통 뼈대.
    ///
    /// 손으로 그린 그림 대신 <b>진짜를 찍습니다</b> — 게임이 쓰는 프리팹을 그대로 세워 놓고,
    /// 런타임과 같은 함수로 색을 입히고 같은 함수로 우리 이펙트를 터뜨린 뒤, 전용 카메라로
    /// 렌더 텍스처에 담습니다. 미리보기와 실제가 갈릴 여지가 없습니다.
    ///
    /// 무대는 빈 레이어 하나에 가둡니다. 그러지 않으면 미리보기 카메라가 실제 씬을 찍고,
    /// 무대의 라이트가 게임 화면까지 밝힙니다.
    ///
    /// 총구 화염과 근접 참격이 이 뼈대를 나눠 씁니다. 다른 것은 셋뿐입니다 —
    /// 무대에 무엇을 올리는지(<see cref="OnStageCreated"/>), 한 번에 무엇을 터뜨리는지
    /// (<see cref="Fire"/>), 그리고 그걸 담으려면 세로로 몇 미터가 필요한지
    /// (<see cref="NeededViewHeight"/>).
    /// </summary>
    public abstract class EffectPreviewStage<TProfile> where TProfile : class
    {
        protected const int TextureWidth = 512;
        protected const int TextureHeight = 340;

        /// <summary>아무리 작아도 이보다는 넓게 잡습니다 (너무 당기면 뭐가 뭔지 안 보입니다).</summary>
        private const float MinViewHeight = 1.2f;

        private Camera? _camera;
        private RenderTexture? _texture;
        private GameObject? _stage;
        private float _timer;

        /// <summary>지금 보이는 세로 범위(m). 내용에 맞춰 부드럽게 따라갑니다.</summary>
        private float _viewHeight = 4f;

        /// <summary>이펙트가 놓이는 자리. 앞쪽(+Z)이 화면 오른쪽을 향합니다.</summary>
        protected Transform? Anchor { get; private set; }

        /// <summary>무대를 가둬 둔 레이어</summary>
        protected int Layer { get; private set; } = -1;

        /// <summary>지금 그리고 있는 프로필. <see cref="Fire"/> 안에서 읽습니다.</summary>
        protected TProfile Profile { get; private set; } = null!;

        public string Status { get; protected set; } = "";

        /// <summary>무대 오브젝트 이름 (진단용)</summary>
        protected abstract string StageName { get; }

        /// <summary>반복 재생 간격(초). 실제보다 느리게 돌려야 형태가 눈에 들어옵니다.</summary>
        protected abstract float Interval { get; }

        /// <summary>무대를 처음 세울 때 한 번. 여기서 미리보기용 이미터를 붙입니다.</summary>
        protected abstract void OnStageCreated(Transform anchor);

        /// <summary>한 번 터뜨립니다.</summary>
        protected abstract void Fire();

        /// <summary>지금 프로필을 다 담으려면 세로로 몇 미터가 필요한지.</summary>
        protected abstract float NeededViewHeight();

        /// <summary>
        /// 카메라가 어느 쪽에서 보는지. 기본은 옆에서 보는 그림(+Z 방향)입니다.
        ///
        /// 이펙트가 평평한 판이라면 옆에서 찍을 때 선 하나로 보입니다 — 그래서
        /// 판을 쓰는 쪽은 이걸 덮어써서 정면으로 봅니다.
        /// </summary>
        protected virtual Quaternion ViewRotation => Quaternion.identity;

        /// <summary>
        /// 이펙트를 화면 어디에 둘지 (화면 반폭 기준). 0이면 한가운데입니다.
        ///
        /// 총구에서 앞으로 뻗는 이펙트는 시작점을 왼쪽에 두는 편이 잘 읽히지만,
        /// 참격처럼 중심을 두고 사방으로 퍼지는 것은 가운데가 맞습니다.
        /// </summary>
        protected virtual float FocusShift => 0.45f;

        /// <summary>
        /// 매 프레임. 터뜨린 뒤에도 프레임마다 손봐야 하는 이펙트가 여기서 갱신합니다
        /// (근접 흩뿌림은 참격 호가 커지는 것을 따라가며 알갱이를 얹습니다).
        /// </summary>
        protected virtual void OnFrame() { }

        /// <summary>무대를 접을 때 추가로 정리할 것이 있으면.</summary>
        protected virtual void OnDispose() { }

        /// <summary>한 프레임 그리고 결과 텍스처를 돌려줍니다.</summary>
        public Texture? Render(TProfile? profile)
        {
            if (profile == null)
                return null;

            Profile = profile;

            try
            {
                EnsureResources();
                if (_camera == null || _texture == null || Anchor == null)
                    return null;

                FrameContent();

                // 창이 떠 있는 동안 게임은 멈춰 있습니다.
                _timer += Time.unscaledDeltaTime;
                if (_timer >= Interval)
                {
                    _timer = 0f;
                    Fire();
                }

                OnFrame();

                _camera.Render();
                return _texture;
            }
            catch (Exception ex)
            {
                Status = Localized("preview_error");
                UnityEngine.Debug.LogWarning($"[WeaponAura] {StageName} 미리보기 오류: {ex.Message}");
                return _texture;
            }
        }

        public void Dispose()
        {
            OnDispose();

            if (_stage != null)
            {
                UnityEngine.Object.Destroy(_stage);
                _stage = null;
            }

            Anchor = null;

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

        /// <summary>
        /// 무대에 올리기 전 손봐야 하는 것들.
        /// 레이어를 옮기지 않으면 카메라가 못 찍고, 시간 기준을 안 바꾸면 멈춰 있는 동안
        /// 파티클이 얼어붙고, 라이트를 가두지 않으면 실제 게임 화면까지 밝힙니다.
        /// </summary>
        protected void PrepareForStage(GameObject root)
        {
            if (root == null)
                return;

            SetLayerRecursive(root.transform, Layer);

            foreach (var system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system == null)
                    continue;

                var main = system.main;
                main.useUnscaledTime = true;
            }

            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light != null)
                    light.cullingMask = 1 << Layer;
            }
        }

        // ── 자원 ─────────────────────────────────────────────────────

        private void EnsureResources()
        {
            EnsureLayer();

            if (_texture == null)
            {
                // 게임이 HDR로 그리면 미리보기도 HDR이어야 블룸이 같게 걸립니다.
                var format = PreviewCameraSetup.GameUsesHdr()
                    ? RenderTextureFormat.DefaultHDR
                    : RenderTextureFormat.ARGB32;

                _texture = new RenderTexture(TextureWidth, TextureHeight, 24, format)
                {
                    name = $"WeaponAura_{StageName}PreviewRT",
                    antiAliasing = 2,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _texture.Create();
            }

            if (_stage == null)
            {
                _stage = new GameObject($"WeaponAura_{StageName}PreviewStage")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                UnityEngine.Object.DontDestroyOnLoad(_stage);

                // 실제 게임에서 이펙트는 무기가 향하는 쪽으로 나갑니다. 무대에서는 그 방향을
                // 화면 오른쪽(+X)으로 돌려서 옆에서 보는 그림을 만듭니다.
                _stage.transform.position = new Vector3(0f, 0f, 500f);
                _stage.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                var anchor = new GameObject("Origin").transform;
                anchor.SetParent(_stage.transform, false);
                Anchor = anchor;

                OnStageCreated(anchor);
            }

            if (_camera == null)
            {
                var go = new GameObject($"WeaponAura_{StageName}PreviewCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                UnityEngine.Object.DontDestroyOnLoad(go);

                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;                 // 수동 Render()만
                _camera.targetTexture = _texture;
                _camera.clearFlags = CameraClearFlags.SolidColor;

                // 가산 합성이라 완전한 검정 배경에서는 옅은 부분이 안 보입니다.
                // 오라 미리보기와 같은 이유로 중간 밝기를 씁니다.
                _camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f);
                _camera.orthographic = true;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 60f;
            }

            _camera.cullingMask = 1 << Layer;

            // 게임 화면의 후처리를 그대로 받게 맞춥니다(오라 미리보기와 같은 이유).
            PreviewCameraSetup.Match(_camera, StageName, Layer);
        }

        /// <summary>
        /// 내용에 맞춰 시야를 좁혔다 넓힙니다.
        ///
        /// 고정 시야로 두면 7m짜리 게임 이펙트에 맞춘 화면에서 1m짜리 하트가 점처럼 보입니다.
        /// 무엇을 보여 주는 모드인지에 따라 필요한 범위가 다섯 배씩 차이 납니다.
        /// </summary>
        private void FrameContent()
        {
            if (_camera == null || _stage == null)
                return;

            float needed = Mathf.Max(MinViewHeight, NeededViewHeight());

            // 슬라이더를 끌 때 화면이 튀지 않게 천천히 따라갑니다.
            _viewHeight = Mathf.Lerp(_viewHeight, needed, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));

            _camera.orthographicSize = _viewHeight * 0.5f;

            var view = ViewRotation;

            // 시작점을 왼쪽에 두고 오른쪽으로 뻗어 나가게 잡습니다.
            //
            // 밀어내는 방향은 반드시 <b>카메라의</b> 오른쪽이어야 합니다. 월드 X로 밀면
            // 카메라를 돌린 순간 그 방향이 화면 오른쪽이 아니게 되어, 이펙트가 화면 밖으로
            // (심하면 카메라 뒤로) 밀려납니다. 근접 참격에서 아무것도 안 보이던 원인입니다.
            float halfWidth = _viewHeight * 0.5f * TextureWidth / TextureHeight;
            var focus = _stage.transform.position + view * Vector3.right * (halfWidth * FocusShift);

            _camera.transform.position = focus - view * Vector3.forward * 20f;
            _camera.transform.rotation = view;
        }

        /// <summary>
        /// 무대를 가둘 빈 레이어를 찾습니다. 못 찾으면 게임 오브젝트와 같은 레이어를 쓰게 되어
        /// 미리보기 카메라가 실제 씬을 찍습니다.
        /// </summary>
        private void EnsureLayer()
        {
            if (Layer >= 0)
                return;

            for (int i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    Layer = i;
                    return;
                }
            }

            Layer = Mathf.Max(0, LayerMask.NameToLayer("Character"));
            UnityEngine.Debug.LogWarning(
                $"[WeaponAura] 비어 있는 레이어가 없어 {StageName} 미리보기를 격리하지 못했습니다.");
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;

            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        /// <summary>
        /// 생성된 <c>L</c> 접근자를 쓰지 않고 키로 직접 읽습니다.
        /// 무대가 쓰는 상태 문구는 두어 개뿐이라 키를 노출할 만큼은 아닙니다.
        /// </summary>
        protected static string Localized(string key)
        {
            try
            {
                return Ducky.Sdk.Localizations.L.Get(key);
            }
            catch
            {
                return "";
            }
        }
    }
}
