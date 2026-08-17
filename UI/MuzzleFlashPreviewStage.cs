using System;
using UnityEngine;
using WeaponAura.Patches;
using WeaponAura.Settings;
using WeaponAura.Systems;

namespace WeaponAura.UI
{
    /// <summary>
    /// 총구 화염 미리보기 무대.
    ///
    /// 손으로 그린 그림 대신 <b>진짜를 찍습니다</b> — 게임이 쓰는 총구 화염 프리팹을 그대로
    /// 세워 놓고, 런타임과 같은 함수로 색을 입히고 같은 함수로 우리 이펙트를 터뜨린 뒤,
    /// 전용 카메라로 렌더 텍스처에 담습니다. 미리보기와 실제가 갈릴 여지가 없습니다.
    ///
    /// (직접 그리던 시절에는 게임 화염을 "둥근 글로우"로 흉내 냈는데, 실제 프리팹은
    ///  파티클 셋에 연기까지 딸린 물건이라 전혀 다르게 보였습니다.)
    ///
    /// 무기 미리보기(<see cref="WeaponAuraPreviewStage"/>)와 같은 방식입니다 —
    /// 빈 레이어를 하나 잡아 무대를 격리하고, 그 레이어만 찍는 카메라를 수동으로 돌립니다.
    /// </summary>
    public class MuzzleFlashPreviewStage
    {
        private const int TextureWidth = 512;
        private const int TextureHeight = 340;

        /// <summary>게임 기본 화염의 대략적인 크기(m). 실측 로그에서 나온 값입니다.</summary>
        private const float GameFlashMetres = 7f;

        /// <summary>아무리 작아도 이보다는 넓게 잡습니다 (너무 당기면 뭐가 뭔지 안 보입니다).</summary>
        private const float MinViewHeight = 1.2f;

        /// <summary>발사 간격(초). 실제 연사보다 느리게 돌려야 형태가 눈에 들어옵니다.</summary>
        private const float Interval = 0.75f;

        private Camera? _camera;
        private RenderTexture? _texture;
        private GameObject? _stage;
        private Transform? _anchor;
        private MuzzleFlashSystem.PreviewEmitter? _emitter;
        private GameObject? _gameFlash;
        private int _layer = -1;

        private float _timer = Interval;

        /// <summary>지금 보이는 세로 범위(m). 내용에 맞춰 부드럽게 따라갑니다.</summary>
        private float _viewHeight = 4f;

        public string Status { get; private set; } = "";

        /// <summary>한 프레임 그리고 결과 텍스처를 돌려줍니다.</summary>
        public Texture? Render(MuzzleFlashProfile? profile)
        {
            if (profile == null)
                return null;

            try
            {
                EnsureResources();
                if (_camera == null || _texture == null || _anchor == null)
                    return null;

                FrameContent(profile);

                // 창이 떠 있는 동안 게임은 멈춰 있습니다.
                _timer += Time.unscaledDeltaTime;
                if (_timer >= Interval)
                {
                    _timer = 0f;
                    Fire(profile);
                }

                _camera.Render();
                return _texture;
            }
            catch (Exception ex)
            {
                Status = L2("preview_error");
                UnityEngine.Debug.LogWarning($"[WeaponAura] 총구 화염 미리보기 오류: {ex.Message}");
                return _texture;
            }
        }

        public void Dispose()
        {
            DestroyGameFlash();

            if (_stage != null)
            {
                UnityEngine.Object.Destroy(_stage);
                _stage = null;
            }

            _anchor = null;
            _emitter = null;

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

        // ── 한 발 ────────────────────────────────────────────────────

        private void Fire(MuzzleFlashProfile profile)
        {
            DestroyGameFlash();

            var mode = MuzzleFlashSettings.Mode;

            // 게임 화염이 남는 모드에서는 진짜 프리팹을 세우고 런타임과 같은 함수로 물들입니다.
            if (mode != MuzzleFlashMode.Replace)
            {
                var prefab = MuzzleFlashPatch.LastMuzzlePrefab;
                if (prefab != null)
                {
                    _gameFlash = UnityEngine.Object.Instantiate(prefab, _anchor!.position, _anchor.rotation);
                    _gameFlash.transform.SetParent(_anchor, worldPositionStays: true);

                    PrepareForStage(_gameFlash);
                    MuzzleFlashSystem.TintExisting(_gameFlash, profile);

                    // 물들이기가 파티클을 다시 재생시키지는 않으므로 여기서 처음부터 돌립니다.
                    foreach (var system in _gameFlash.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (system == null)
                            continue;

                        system.Clear();
                        system.Play();
                    }

                    Status = "";
                }
                else
                {
                    // 한 발도 쏘기 전에는 어떤 총의 화염인지 알 수가 없습니다.
                    Status = L2("muzzle_preview_need_shot");
                }
            }

            if (mode != MuzzleFlashMode.TintDefault)
                MuzzleFlashSystem.PreviewEmit(_emitter!, profile);
        }

        private void DestroyGameFlash()
        {
            if (_gameFlash == null)
                return;

            UnityEngine.Object.Destroy(_gameFlash);
            _gameFlash = null;
        }

        /// <summary>
        /// 무대에 올리기 전 손봐야 하는 것들.
        /// 레이어를 옮기지 않으면 카메라가 못 찍고, 시간 기준을 안 바꾸면 멈춰 있는 동안
        /// 파티클이 얼어붙고, 라이트를 가두지 않으면 실제 게임 화면까지 밝힙니다.
        /// </summary>
        private void PrepareForStage(GameObject root)
        {
            SetLayerRecursive(root.transform, _layer);

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
                    light.cullingMask = 1 << _layer;
            }
        }

        // ── 자원 ─────────────────────────────────────────────────────

        private void EnsureResources()
        {
            EnsureLayer();

            if (_texture == null)
            {
                _texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "WeaponAura_MuzzlePreviewRT",
                    antiAliasing = 2,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _texture.Create();
            }

            if (_stage == null)
            {
                _stage = new GameObject("WeaponAura_MuzzlePreviewStage")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                UnityEngine.Object.DontDestroyOnLoad(_stage);

                // 실제 게임에서 화염은 총구가 향하는 쪽으로 터집니다. 무대에서는 그 방향을
                // 화면 오른쪽(+X)으로 돌려서 옆에서 보는 그림을 만듭니다.
                _stage.transform.position = new Vector3(0f, 0f, 500f);
                _stage.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                var anchor = new GameObject("Muzzle").transform;
                anchor.SetParent(_stage.transform, false);
                _anchor = anchor;

                _emitter = MuzzleFlashSystem.CreatePreviewEmitter(anchor);
                PrepareForStage(_emitter.Root);
            }

            if (_camera == null)
            {
                var go = new GameObject("WeaponAura_MuzzlePreviewCamera")
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

            _camera.cullingMask = 1 << _layer;
        }

        /// <summary>
        /// 내용에 맞춰 시야를 좁혔다 넓힙니다.
        ///
        /// 고정 시야로 두면 게임 화염(7m)에 맞춘 화면에서 1m짜리 하트가 점처럼 보입니다.
        /// 무엇을 보여 주는 모드인지에 따라 필요한 범위가 다섯 배씩 차이 납니다.
        /// </summary>
        private void FrameContent(MuzzleFlashProfile profile)
        {
            if (_camera == null || _stage == null)
                return;

            var mode = MuzzleFlashSettings.Mode;
            float needed = MinViewHeight;

            if (mode != MuzzleFlashMode.Replace && MuzzleFlashPatch.LastMuzzlePrefab != null)
                needed = Mathf.Max(needed, GameFlashMetres * Mathf.Max(0.05f, profile.sizeScale));

            if (mode != MuzzleFlashMode.TintDefault)
            {
                needed = Mathf.Max(needed, profile.size * 1.8f);
                needed = Mathf.Max(needed, (profile.sparkDistance + profile.sparkSize) * 1.15f);
                needed = Mathf.Max(needed, Mathf.Abs(profile.sparkRise) * 2.6f + profile.sparkSize);
            }

            // 슬라이더를 끌 때 화면이 튀지 않게 천천히 따라갑니다.
            _viewHeight = Mathf.Lerp(_viewHeight, needed, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));

            _camera.orthographicSize = _viewHeight * 0.5f;

            // 총구를 왼쪽에 두고 오른쪽으로 뻗어 나가게 잡습니다.
            float halfWidth = _viewHeight * 0.5f * TextureWidth / TextureHeight;
            var focus = _stage.transform.position + new Vector3(halfWidth * 0.45f, 0f, 0f);

            _camera.transform.position = focus + new Vector3(0f, 0f, -20f);
            _camera.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 무대를 가둘 빈 레이어를 찾습니다. 못 찾으면 게임 오브젝트와 같은 레이어를 쓰게 되어
        /// 미리보기 카메라가 실제 씬을 찍습니다.
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
            UnityEngine.Debug.LogWarning(
                "[WeaponAura] 비어 있는 레이어가 없어 총구 화염 미리보기를 격리하지 못했습니다.");
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
        /// 이 무대는 상태 문구가 두 개뿐이라 키를 노출할 만큼은 아닙니다.
        /// </summary>
        private static string L2(string key)
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
