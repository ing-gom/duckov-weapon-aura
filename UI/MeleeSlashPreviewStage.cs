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
            TickPendingLayers();

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

            // 레이어는 모드와 무관하게 나갑니다. 다만 호 위에 놓아야 하는데 호는 이
            // 프레임에 아직 안 나와 있습니다 — 런타임과 같은 이유입니다. OnFrame에서
            // 자리를 찾는 대로 뿜습니다.
            if (_gameSlash != null)
            {
                _layersPending = true;
                _layerWaitFrames = ArcWaitFrames;
            }
            else
            {
                // 참격을 지우는 모드. 기다릴 호가 없으니 무대 원점에서 뿜습니다.
                PlayLayerBurst(Profile.layers, Profile.sparkSize, SlowMotion);
            }
        }

        /// <summary>호가 나오기를 기다리는 중인지.</summary>
        private bool _layersPending;

        private int _layerWaitFrames;

        /// <summary>
        /// 호를 몇 프레임까지 기다릴지.
        ///
        /// 런타임(6프레임)보다 넉넉합니다 — 무대의 참격은 형태를 읽으라고 <see cref="SlowMotion"/>
        /// 배로 늦춰 돌리기 때문에, 알갱이가 나오기까지도 그만큼 더 걸립니다. 여섯 프레임에서
        /// 끊으면 대부분 못 읽고 한가운데로 밀려났습니다.
        /// </summary>
        private const int ArcWaitFrames = 30;

        /// <summary>
        /// 마지막으로 읽어 둔 호. 참격이 사라진 사이에도 들고 있습니다.
        ///
        /// 호는 참격 알갱이가 <b>살아 있는 동안에만</b> 읽힙니다. 카메라 방향과 화면 크기가
        /// 이미 같은 이유로 마지막 값을 붙들고 있는데(<see cref="_measuredRadius"/>),
        /// 레이어만 "지금 못 읽으면 포기"였습니다. 참격을 세우자마자 뿜으려 드니 그 순간에는
        /// 아직 알갱이가 없어서, <b>미리보기에서만</b> 자리를 못 잡고 한가운데로 밀려났습니다.
        /// 실제 게임에서는 참격이 열두 프레임쯤 살아 있어서 기다리면 잡혔습니다.
        /// </summary>
        private (Vector3 position, Quaternion rotation)? _arcPose;

        private EffectLayerBurst.BurstArc _arc;

        private void TickPendingLayers()
        {
            bool live = MeleeSlashSystem.TryGetArcAnchor(_gameSlash, Profile, out var position,
                out var rotation, out var arc);

            if (live)
            {
                _arcPose = (position, rotation);
                _arc = arc;
            }

            if (!_layersPending)
                return;

            // 이번 참격에서 호를 읽었으면 그 위에서 뿜습니다.
            if (live)
            {
                PlayLayerBurst(Profile.layers, Profile.sparkSize, SlowMotion, _arcPose, _arc);
                LogLayerBurst("호 읽음");
                _layersPending = false;
                return;
            }

            // 아직입니다. 참격이 살아 있는 동안 계속 봅니다 — 다음 참격이 시작되면
            // Fire가 다시 잡아 줍니다.
            if (--_layerWaitFrames > 0)
                return;

            // 이 참격에서는 끝내 못 읽었습니다. 예전에 읽어 둔 호가 있으면 그것을 씁니다.
            // 자리는 같은 무기라면 매번 같으므로 눈에 띄는 차이가 없습니다.
            PlayLayerBurst(Profile.layers, Profile.sparkSize, SlowMotion, _arcPose,
                _arcPose != null ? _arc : (EffectLayerBurst.BurstArc?)null);

            LogLayerBurst(_arcPose != null ? "직전 호" : "호 없음");
            _layersPending = false;
        }

        /// <summary>
        /// 레이어가 무대에서 어떻게 뿜혔는지 한 줄.
        ///
        /// "미리보기에만 안 나온다"를 눈으로 가리려면 <b>호를 읽었는지</b>와 <b>어디에
        /// 놓였는지</b>를 알아야 합니다. 그게 없으면 안 만들어진 것인지, 만들었는데 화면
        /// 밖인지 구분이 안 됩니다. 상태가 바뀔 때만 남겨서 로그를 채우지 않습니다.
        /// </summary>
        private void LogLayerBurst(string how)
        {
            int on = 0;
            int arcs = 0;

            foreach (var layer in Profile.layers)
            {
                if (layer == null || !layer.enabled)
                    continue;

                on++;
                if (layer.arcSpread)
                    arcs++;
            }

            string line = $"{how} 레이어={on}개(반월 {arcs}) " +
                          $"반지름={_arc.Radius:0.00}m 각도={_arc.Degrees:0}도 " +
                          $"참격={(_gameSlash != null ? "있음" : "없음")}";

            if (line == _lastLayerLog)
                return;

            _lastLayerLog = line;
            UnityEngine.Debug.Log($"[WeaponAura] 참격 미리보기 레이어: {line}");
        }

        private string _lastLayerLog = "";

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

            // 레이어는 모드와 무관하게 나갑니다. 참격이 남는 모드에서는 호 위에서
            // 뿜으므로, 호 밖으로 뻗는 만큼을 호 크기에 <b>더해</b> 잡아야 합니다.
            float layers = LayerViewHeight(Profile.layers);

            if (mode != MeleeSlashMode.Replace && _measuredRadius > 0.01f)
                layers += _measuredRadius * 2f;

            needed = Mathf.Max(needed, layers);

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
