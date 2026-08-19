using UnityEngine;
using WeaponAura.Patches;
using WeaponAura.Settings;
using WeaponAura.Systems;

namespace WeaponAura.UI
{
    /// <summary>
    /// 총구 화염 미리보기 무대.
    ///
    /// 무대·카메라·레이어 격리는 <see cref="EffectPreviewStage{TProfile}"/>가 맡습니다.
    /// 여기 남는 것은 "한 발 쏘면 무슨 일이 일어나는가" 뿐입니다 — 게임 화염 프리팹을
    /// 세워 런타임과 같은 함수로 물들이고, 그 위에 우리 화염을 터뜨립니다.
    ///
    /// (직접 그리던 시절에는 게임 화염을 "둥근 글로우"로 흉내 냈는데, 실제 프리팹은
    ///  파티클 셋에 연기까지 딸린 물건이라 전혀 다르게 보였습니다.)
    /// </summary>
    public class MuzzleFlashPreviewStage : EffectPreviewStage<MuzzleFlashProfile>
    {
        /// <summary>게임 기본 화염의 대략적인 크기(m). 실측 로그에서 나온 값입니다.</summary>
        private const float GameFlashMetres = 7f;

        private MuzzleFlashSystem.PreviewEmitter? _emitter;
        private GameObject? _gameFlash;

        protected override string StageName => "Muzzle";

        /// <summary>발사 간격(초). 실제 연사보다 느리게 돌려야 형태가 눈에 들어옵니다.</summary>
        protected override float Interval => 0.75f;

        protected override void OnStageCreated(Transform anchor)
        {
            _emitter = MuzzleFlashSystem.CreatePreviewEmitter(anchor);
            PrepareForStage(_emitter.Root);
        }

        protected override void OnDispose()
        {
            DestroyGameFlash();
            _emitter = null;
        }

        protected override void Fire()
        {
            DestroyGameFlash();

            var mode = MuzzleFlashSettings.Mode;

            // 게임 화염이 남는 모드에서는 진짜 프리팹을 세우고 런타임과 같은 함수로 물들입니다.
            if (mode != MuzzleFlashMode.Replace)
            {
                var prefab = MuzzleFlashPatch.LastMuzzlePrefab;
                if (prefab != null)
                {
                    _gameFlash = UnityEngine.Object.Instantiate(prefab, Anchor!.position, Anchor.rotation);
                    _gameFlash.transform.SetParent(Anchor, worldPositionStays: true);

                    PrepareForStage(_gameFlash);
                    MuzzleFlashSystem.TintExisting(_gameFlash, Profile);

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
                    Status = Localized("muzzle_preview_need_shot");
                }
            }

            if (mode != MuzzleFlashMode.TintDefault && _emitter != null)
                MuzzleFlashSystem.PreviewEmit(_emitter, Profile);

            // 레이어는 모드와 무관하게 나갑니다 — 런타임(MuzzleFlashPatch)과 같습니다.
            // 게임 화염을 그대로 두고 불티만 얹는 조합이 이 미리보기에서도 보여야 합니다.
            PlayLayerBurst(Profile.layers, Profile.size);
        }

        protected override float NeededViewHeight()
        {
            var mode = MuzzleFlashSettings.Mode;
            float needed = 0f;

            if (mode != MuzzleFlashMode.Replace && MuzzleFlashPatch.LastMuzzlePrefab != null)
                needed = Mathf.Max(needed, GameFlashMetres * Mathf.Max(0.05f, Profile.sizeScale));

            if (mode != MuzzleFlashMode.TintDefault)
            {
                needed = Mathf.Max(needed, Profile.size * 1.8f);
                needed = Mathf.Max(needed, (Profile.sparkDistance + Profile.sparkSize) * 1.15f);
                needed = Mathf.Max(needed, Mathf.Abs(Profile.sparkRise) * 2.6f + Profile.sparkSize);
            }

            // 레이어는 모드와 무관하게 나가므로 분기 밖에서 잽니다.
            needed = Mathf.Max(needed, LayerViewHeight(Profile.layers));

            return needed;
        }

        private void DestroyGameFlash()
        {
            if (_gameFlash == null)
                return;

            UnityEngine.Object.Destroy(_gameFlash);
            _gameFlash = null;
        }
    }
}
