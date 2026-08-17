using UnityEngine;
using WeaponAura.Patches;
using WeaponAura.Settings;
using WeaponAura.Systems;

namespace WeaponAura.UI
{
    /// <summary>
    /// 근접 참격 미리보기 무대.
    ///
    /// 게임 참격 프리팹을 그대로 세워 런타임과 같은 함수로 물들이고, 그 위에 우리 흩뿌림을
    /// 터뜨립니다. 참격은 원래 캐릭터 앞쪽으로 나가므로 무대에서도 앞쪽(화면 오른쪽)을 봅니다.
    /// </summary>
    public class MeleeSlashPreviewStage : EffectPreviewStage<MeleeSlashProfile>
    {
        /// <summary>참격을 아직 한 번도 못 재 봤을 때 쓰는 화면 크기(m).</summary>
        private const float FallbackSlashMetres = 3.5f;

        private MeleeSlashSystem.PreviewEmitter? _emitter;
        private GameObject? _gameSlash;

        /// <summary>마지막으로 잰 호의 반지름(m). 참격이 사라진 사이에도 화면이 흔들리지 않게 들고 있습니다.</summary>
        private float _measuredRadius;

        protected override string StageName => "Melee";

        /// <summary>
        /// 휘두르는 간격(초).
        ///
        /// 게임 참격은 0.2초짜리입니다. 1.1초마다 한 번씩 보여 주면 다섯 번 중 네 번은
        /// 빈 화면이라 "안 보인다"가 됩니다. 간격을 좁히고 재생을 늦춰서 대부분의 시간
        /// 동안 호가 화면에 남아 있게 합니다.
        /// </summary>
        protected override float Interval => 0.7f;

        /// <summary>참격을 얼마나 늦춰 볼지. 0.2초짜리를 그대로 돌리면 형태가 안 읽힙니다.</summary>
        private const float SlowMotion = 0.45f;

        /// <summary>
        /// 참격 판을 정면으로 봅니다.
        ///
        /// 참격은 평평한 판 한 장이라, 옆에서 찍으면 선 하나로 보입니다(그래서 미리보기에
        /// 아무것도 없는 것처럼 보였습니다). 판이 실제로 어느 쪽을 향하는지는 프리팹마다
        /// 다르므로, 살아 있는 알갱이에서 법선을 읽어 그쪽으로 카메라를 돌립니다.
        /// </summary>
        protected override Quaternion ViewRotation => _view;

        /// <summary>참격은 중심을 두고 사방으로 퍼지는 호라 한가운데가 맞습니다.</summary>
        protected override float FocusShift => 0f;

        private Quaternion _view = Quaternion.identity;

        protected override void OnStageCreated(Transform anchor)
        {
            _emitter = MeleeSlashSystem.CreatePreviewEmitter(anchor);
            PrepareForStage(_emitter.Root);
        }

        /// <summary>
        /// 무대의 참격도 호가 커집니다. 실제와 똑같이 프레임마다 얹어 줘야
        /// 미리보기에서 붙어 보이는 것이 게임에서도 붙습니다.
        /// </summary>
        protected override void OnFrame()
        {
            MeleeSlashSystem.PreviewTick(_emitter);

            // 판이 향하는 쪽으로 카메라를 돌리고, 호의 실제 크기에 화면을 맞춥니다.
            // 알갱이가 살아 있는 동안에만 읽을 수 있으므로, 읽힐 때마다 갱신하고
            // 사라진 사이에는 마지막 값을 유지합니다.
            if (MeleeSlashSystem.TryGetSlashFrame(_gameSlash, out _, out var frame, out float radius))
            {
                if (radius > 0.01f)
                    _measuredRadius = radius;

                // 판의 법선을 향해 보되, 판의 위쪽이 화면 오른쪽으로 가게 굴립니다 —
                // 다른 탭과 같이 "왼쪽에서 시작해 오른쪽으로 뻗는" 그림이 됩니다.
                var normal = frame * Vector3.forward;
                var up = frame * Vector3.up;

                _view = Quaternion.LookRotation(-normal, Vector3.Cross(-normal, up));
            }
        }

        protected override void OnDispose()
        {
            DestroyGameSlash();
            _emitter = null;
        }

        protected override void Fire()
        {
            DestroyGameSlash();

            var mode = MeleeSlashSettings.Mode;

            // 게임 참격이 남는 모드에서는 진짜 프리팹을 세우고 런타임과 같은 함수로 물들입니다.
            if (mode != MeleeSlashMode.Replace)
            {
                var prefab = SlashPrefab();
                if (prefab != null)
                {
                    _gameSlash = UnityEngine.Object.Instantiate(prefab, Anchor!.position, Anchor.rotation);
                    _gameSlash.transform.SetParent(Anchor, worldPositionStays: true);

                    PrepareForStage(_gameSlash);
                    MeleeSlashSystem.ApplyShape(_gameSlash, Profile);
                    MeleeSlashSystem.TintExisting(_gameSlash, Profile);

                    // 물들이기가 파티클을 다시 재생시키지는 않으므로 여기서 처음부터 돌립니다.
                    foreach (var system in _gameSlash.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (system == null)
                            continue;

                        // 0.2초짜리를 실시간으로 돌리면 형태를 읽을 수 없습니다.
                        var main = system.main;
                        main.simulationSpeed = SlowMotion;

                        system.Clear();
                        system.Play();
                    }

                    Status = "";
                }
                else
                {
                    // 한 번도 휘두르기 전에는 어떤 무기의 참격인지 알 수가 없습니다.
                    Status = Localized("melee_preview_need_swing");
                }
            }

            // 무대에 세워 둔 참격을 그대로 넘깁니다 — 런타임과 같은 함수가 같은 궤적 위에서
            // 뿌리므로, 미리보기에서 호를 따라 붙어 있으면 게임에서도 붙어 있습니다.
            if (mode != MeleeSlashMode.TintDefault && _emitter != null)
                MeleeSlashSystem.PreviewEmit(_emitter, Profile, _gameSlash);
        }

        protected override float NeededViewHeight()
        {
            var mode = MeleeSlashSettings.Mode;
            float needed = 0f;

            if (mode != MeleeSlashMode.Replace && SlashPrefab() != null)
            {
                // 상수로 짐작하지 않고 실제로 잰 크기를 씁니다 — 참격이 그보다 크면
                // 화면 밖으로 나가고, 작으면 점처럼 보입니다.
                needed = Mathf.Max(needed, _measuredRadius > 0.01f
                    ? _measuredRadius * 2.4f
                    : FallbackSlashMetres);
            }

            if (mode != MeleeSlashMode.TintDefault)
            {
                // 부채꼴은 가로로 퍼지지만 세로 화면에도 sin(반각)만큼 걸립니다.
                float half = Mathf.Clamp(Profile.sparkArc * 0.5f, 0f, 90f) * Mathf.Deg2Rad;
                float spread = Profile.sparkDistance * Mathf.Sin(half);

                needed = Mathf.Max(needed, (spread + Profile.sparkSize) * 2.4f);
                needed = Mathf.Max(needed, Mathf.Abs(Profile.sparkRise) * 2.6f + Profile.sparkSize);
                needed = Mathf.Max(needed, Profile.sparkSize * 4f);
            }

            return needed;
        }

        /// <summary>
        /// 무대에 세울 참격 프리팹.
        ///
        /// 지금 든 근접무기에서 바로 가져옵니다 — 예전에는 한 번 휘두르기 전에는 알 수 없어서
        /// 설정 창을 열어도 빈 화면이었습니다. 무기를 들고만 있으면 바로 보이는 편이 맞습니다.
        /// 무기를 안 들었을 때만 마지막으로 휘두른 것으로 돌아갑니다.
        /// </summary>
        private static GameObject? SlashPrefab()
        {
            try
            {
                var player = CharacterMainControl.Main;
                var melee = player != null ? player.GetMeleeWeapon() : null;

                if (melee != null && melee.slashFx != null)
                    return melee.slashFx;
            }
            catch
            {
                // 캐릭터가 없는 화면(메인 메뉴 등)일 수 있습니다.
            }

            return MeleeSlashPatch.LastSlashPrefab;
        }

        private void DestroyGameSlash()
        {
            if (_gameSlash == null)
                return;

            // 참격보다 먼저 그것을 가리키는 셰이프를 끊어야 합니다.
            MeleeSlashSystem.PreviewDetach(_emitter);

            UnityEngine.Object.Destroy(_gameSlash);
            _gameSlash = null;
        }
    }
}
