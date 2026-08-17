using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WeaponAura.UI
{
    /// <summary>
    /// 미리보기 카메라를 게임 카메라와 <b>같은 조건</b>으로 맞춥니다.
    ///
    /// 미리보기 무대는 카메라를 직접 만들어 씁니다. 그런데 새로 만든 카메라는 URP 후처리가
    /// 꺼진 맨 카메라라, 게임 화면에 걸려 있는 블룸이 미리보기에는 걸리지 않습니다.
    /// 오라는 가산 합성이라 블룸이 있으면 <b>밝아지면서 바깥으로 번져 커 보이기까지</b> 합니다.
    /// "인게임이 더 밝고 크게 보인다"가 정확히 이 차이였습니다.
    ///
    /// 값을 우리가 정하지 않고 게임 카메라에서 그대로 읽어 옵니다 — 게임 설정이나 다른 모드가
    /// 후처리를 끄면 미리보기도 같이 꺼져야 맞습니다.
    /// </summary>
    internal static class PreviewCameraSetup
    {
        private static bool _logged;

        /// <summary>미리보기 전용 볼륨. 레이어마다 하나면 충분합니다.</summary>
        private static readonly System.Collections.Generic.Dictionary<int, Volume> _volumes =
            new System.Collections.Generic.Dictionary<int, Volume>();

        /// <summary>
        /// 미리보기 전용 볼륨을 세웁니다.
        ///
        /// 게임 볼륨에서 <b>밝기에 관여하는 것만</b> 베껴 옵니다 — 블룸(빛 번짐),
        /// 톤매핑(HDR을 화면 밝기로 접는 방식), 색 보정. 심도·모션블러는 카메라가 어디서
        /// 무엇을 보는지에 달린 효과라 미리보기에 그대로 쓰면 오히려 실제와 달라집니다.
        /// </summary>
        private static void EnsureVolume(int layer)
        {
            if (_volumes.TryGetValue(layer, out var existing) && existing != null)
            {
                CopyOverrides(existing);
                return;
            }

            var go = new GameObject("WeaponAura_PreviewVolume")
            {
                layer = layer,
                hideFlags = HideFlags.HideAndDontSave,
            };
            UnityEngine.Object.DontDestroyOnLoad(go);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            _volumes[layer] = volume;
            CopyOverrides(volume);
        }

        /// <summary>게임에 지금 걸려 있는 값을 미리보기 볼륨에 옮깁니다.</summary>
        private static void CopyOverrides(Volume volume)
        {
            try
            {
                var stack = VolumeManager.instance?.stack;
                if (stack == null || volume.profile == null)
                    return;

                var sourceBloom = stack.GetComponent<Bloom>();
                if (sourceBloom != null && sourceBloom.active)
                {
                    var bloom = volume.profile.TryGet(out Bloom existing)
                        ? existing
                        : volume.profile.Add<Bloom>(true);

                    bloom.active = true;
                    bloom.threshold.Override(sourceBloom.threshold.value);
                    bloom.intensity.Override(sourceBloom.intensity.value);
                    bloom.scatter.Override(sourceBloom.scatter.value);
                    bloom.tint.Override(sourceBloom.tint.value);
                    bloom.clamp.Override(sourceBloom.clamp.value);
                    bloom.highQualityFiltering.Override(sourceBloom.highQualityFiltering.value);
                }

                var sourceTone = stack.GetComponent<Tonemapping>();
                if (sourceTone != null && sourceTone.active)
                {
                    var tone = volume.profile.TryGet(out Tonemapping existing)
                        ? existing
                        : volume.profile.Add<Tonemapping>(true);

                    tone.active = true;
                    tone.mode.Override(sourceTone.mode.value);
                }

                var sourceColor = stack.GetComponent<ColorAdjustments>();
                if (sourceColor != null && sourceColor.active)
                {
                    var color = volume.profile.TryGet(out ColorAdjustments existing)
                        ? existing
                        : volume.profile.Add<ColorAdjustments>(true);

                    color.active = true;
                    color.postExposure.Override(sourceColor.postExposure.value);
                    color.contrast.Override(sourceColor.contrast.value);
                    color.colorFilter.Override(sourceColor.colorFilter.value);
                    color.saturation.Override(sourceColor.saturation.value);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 미리보기 볼륨 복사 실패: {ex.Message}");
            }
        }

#if DEBUG
        /// <summary>
        /// 지금 걸려 있는 후처리 목록. 심도(DoF)가 켜져 있으면 미리보기가 흐려지는 원인이 됩니다 —
        /// 초점이 게임 카메라 거리에 맞춰져 있는데 미리보기 카메라는 훨씬 가까이 붙어 있습니다.
        /// </summary>
        private static string DescribeVolumes()
        {
            try
            {
                var stack = VolumeManager.instance?.stack;
                if (stack == null)
                    return "볼륨스택 없음";

                var dof = stack.GetComponent<DepthOfField>();
                var bloom = stack.GetComponent<Bloom>();

                string dofText = dof != null && dof.active
                    ? $"심도={dof.mode.value}/초점={dof.focusDistance.value:0.##}"
                    : "심도=없음";

                string bloomText = bloom != null && bloom.active
                    ? $"블룸=강도{bloom.intensity.value:0.##}/임계{bloom.threshold.value:0.##}"
                    : "블룸=없음";

                return dofText + " " + bloomText;
            }
            catch (Exception ex)
            {
                return "볼륨조회실패:" + ex.Message;
            }
        }
#endif

        /// <summary>지금 게임 화면을 그리는 카메라. 미리보기 카메라는 제외합니다.</summary>
        internal static Camera? FindGameCamera()
        {
            try
            {
                var main = Camera.main;
                if (main != null && main.enabled)
                    return main;

                // MainCamera 태그가 없는 경우 — 켜져 있는 카메라 중 가장 나중에 그리는 것.
                Camera? best = null;

                foreach (var camera in Camera.allCameras)
                {
                    if (camera == null || !camera.enabled)
                        continue;

                    // 우리 미리보기 카메라는 enabled=false로 두고 수동 Render만 하므로
                    // 여기 걸리지 않지만, 이름으로 한 번 더 걸러 둡니다.
                    if (camera.name.StartsWith("WeaponAura_", StringComparison.Ordinal))
                        continue;

                    if (best == null || camera.depth > best.depth)
                        best = camera;
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>게임 카메라가 HDR로 그리는지 — 렌더 텍스처 포맷을 여기에 맞춰야 합니다.</summary>
        internal static bool GameUsesHdr()
        {
            var game = FindGameCamera();
            return game != null && game.allowHDR;
        }

        /// <summary>
        /// 게임 카메라의 렌더 조건을 복사합니다.
        ///
        /// 위치·회전·화각·컬링 마스크는 무대가 정하므로 건드리지 않습니다.
        /// 여기서 옮기는 것은 "같은 그림으로 보이게 하는" 값뿐입니다.
        /// </summary>
        /// <param name="volumeLayer">미리보기 무대를 가둬 둔 레이어. 전용 볼륨도 여기 세웁니다.</param>
        internal static void Match(Camera? preview, string stageName, int volumeLayer)
        {
            if (preview == null)
                return;

            var game = FindGameCamera();
            if (game == null)
                return;

            try
            {
                preview.allowHDR = game.allowHDR;
                preview.allowMSAA = game.allowMSAA;

                var source = game.GetUniversalAdditionalCameraData();
                var target = preview.GetUniversalAdditionalCameraData();

                if (source == null || target == null)
                    return;

                target.renderPostProcessing = source.renderPostProcessing;

                // 시간 기반 AA(TAA)만은 가져오면 안 됩니다.
                //
                // TAA는 이전 프레임 결과와 모션 벡터를 섞어서 가장자리를 다듬습니다. 그런데
                // 미리보기 카메라는 enabled=false로 두고 우리가 한 장씩 수동으로 찍는 데다,
                // 무대는 계속 돌고 있습니다. 이력이 매 프레임 어긋나서 다듬는 게 아니라
                // 잔상으로 번집니다 — 캐릭터가 흐릿하게 보이던 원인입니다.
                // 공간 기반(SMAA)은 한 장만 보고 판단하므로 그대로 씁니다.
                target.antialiasing = source.antialiasing == AntialiasingMode.TemporalAntiAliasing
                    ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                    : source.antialiasing;

                target.antialiasingQuality = source.antialiasingQuality;

                // 게임 볼륨을 그대로 물려받으면 <b>심도(DoF)</b>까지 딸려옵니다.
                //
                // 게임은 탑다운이라 초점이 45m 밖에 잡혀 있는데 미리보기 카메라는 피사체에
                // 1~2m까지 붙습니다. 그러면 미리보기만 완전히 초점 밖이 되어 뿌옇게 뭉개지고,
                // 보케가 알갱이의 빛을 넓게 퍼뜨려 블룸 임계값도 못 넘깁니다 —
                // "게임에서는 빛나는데 미리보기에서는 안 보인다"가 이것이었습니다.
                //
                // 그래서 게임 볼륨을 쓰지 않고, 밝기에 관여하는 것만 복사한 전용 볼륨을
                // 미리보기 레이어에 세워 그것만 보게 합니다.
                target.volumeLayerMask = 1 << volumeLayer;
                target.volumeTrigger = preview.transform;

                EnsureVolume(volumeLayer);

#if DEBUG
                if (_logged)
                    return;

                _logged = true;
                UnityEngine.Debug.Log(
                    $"[WeaponAura] 미리보기 카메라 맞춤({stageName}): 게임카메라='{game.name}' " +
                    $"HDR={game.allowHDR} 후처리={source.renderPostProcessing} " +
                    $"AA={source.antialiasing}→{target.antialiasing} " +
                    $"볼륨레이어={source.volumeLayerMask.value} | {DescribeVolumes()}");
#endif
            }
            catch (Exception ex)
            {
                // 렌더 파이프라인이 URP가 아니거나 API가 다르면 그냥 맨 카메라로 둡니다.
                UnityEngine.Debug.LogWarning(
                    $"[WeaponAura] 미리보기 카메라를 게임과 맞추지 못했습니다({stageName}): {ex.Message}");
            }
        }
    }
}
