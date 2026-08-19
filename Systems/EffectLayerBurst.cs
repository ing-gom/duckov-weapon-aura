using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 한 번 뿜고 사라지는 레이어 — 총구 화염과 근접 참격이 씁니다.
    ///
    /// 오라 레이어는 무기에 붙어 <b>계속</b> 뿜지만, 이 둘은 쏘거나 휘두르는 <b>순간</b>에만
    /// 나옵니다. 그래서 같은 <see cref="WeaponEffectLayer"/> 값을 쓰면서도 해석이 하나
    /// 달라집니다 — <c>rate</c>가 "초당 개수"가 아니라 <b>한 번에 뿜는 개수</b>입니다.
    /// 0.1초 사는 연출에 초당 개수를 적용하면 한두 알만 나오고 끝납니다.
    ///
    /// 만든 것은 수명이 다하면 스스로 정리합니다. 발사할 때마다 새로 만들고 지우는 것이
    /// 아까워 보이지만, 총구·참격은 이미 게임이 그렇게 하고 있고(매 발 새 오브젝트)
    /// 여기서만 풀을 두면 수명 관리가 두 벌이 됩니다.
    /// </summary>
    public static class EffectLayerBurst
    {
        /// <summary>동시에 살아 있을 수 있는 뿜음 수. 연사에서 무한정 쌓이지 않게 합니다.</summary>
        private const int MaxLive = 48;

        /// <summary>
        /// 호를 따라 퍼뜨릴 때 필요한 것 — 어느 원의 어느 구간인지.
        ///
        /// 근접 참격이 넘겨줍니다. 총구에는 호가 없으므로 넘기지 않습니다.
        /// </summary>
        public readonly struct BurstArc
        {
            /// <summary>호의 중심 (참격의 회전 중심)</summary>
            public readonly Vector3 Center;

            /// <summary>호가 시작하는 쪽. 여기서부터 <see cref="Degrees"/>만큼 돕니다.</summary>
            public readonly Vector3 Start;

            /// <summary>호가 도는 축 — 참격 판의 법선입니다.</summary>
            public readonly Vector3 Axis;

            /// <summary>호의 반지름(m)</summary>
            public readonly float Radius;

            /// <summary>호가 걸치는 각도(도)</summary>
            public readonly float Degrees;

            public BurstArc(Vector3 center, Vector3 start, Vector3 axis, float radius, float degrees)
            {
                Center = center;
                Start = start;
                Axis = axis;
                Radius = radius;
                Degrees = degrees;
            }
        }

        private sealed class Live
        {
            public GameObject Go = null!;
            public float DieAt;
        }

        private static readonly List<Live> _live = new List<Live>();

        /// <summary>
        /// 레이어들을 한 번 뿜습니다.
        /// </summary>
        /// <param name="layers">뿜을 레이어 목록 (꺼진 것은 건너뜁니다)</param>
        /// <param name="position">뿜을 자리 (월드)</param>
        /// <param name="rotation">뿜는 방향 기준 (월드)</param>
        /// <param name="size">무기·이펙트 크기 — "전체" 자리를 쓰는 레이어의 상자 크기</param>
        /// <param name="arc">
        /// 호를 따라 퍼뜨릴 수 있는 자리(근접 참격)라면 그 호. 넘기면 <see cref="WeaponEffectLayer.arcSpread"/>가
        /// 켜진 레이어가 이 호 전체에 퍼집니다. 총구처럼 호가 없는 자리는 넘기지 않습니다.
        /// </param>
        public static void Play(WeaponEffectLayer[]? layers, Vector3 position, Quaternion rotation,
            Vector3 size, BurstArc? arc = null)
        {
            if (layers == null || layers.Length == 0)
                return;

            Cleanup();

            foreach (var layer in layers)
            {
                if (layer == null || !layer.enabled)
                    continue;

                if (_live.Count >= MaxLive)
                    return;

                try
                {
                    Spawn(layer, position, rotation, size, arc);
                }
                catch (Exception ex)
                {
#if DEBUG
                    UnityEngine.Debug.LogWarning($"[WeaponAura] 레이어 뿜기 실패: {ex.Message}");
#endif
                }
            }
        }

        private static void Spawn(WeaponEffectLayer layer, Vector3 position, Quaternion rotation,
            Vector3 size, BurstArc? arc)
        {
            var host = new GameObject("WeaponAura_LayerBurst");
            host.transform.SetPositionAndRotation(position, rotation);

            AddEmitter(host.transform, layer, size, 1f, arc);

            // 마지막 알갱이가 사라질 때까지 두었다가 정리합니다.
            float life = Mathf.Max(0.05f, layer.lifetime) + 0.2f;

            _live.Add(new Live { Go = host, DieAt = Time.unscaledTime + life });
        }

        /// <summary>
        /// 미리보기 무대용 한 벌.
        ///
        /// 실제와 갈리면 안 되므로 <see cref="Play"/>와 <b>같은 이미터</b>를 만듭니다. 다른
        /// 것은 셋뿐입니다 — 무대 밑에 매달고(카메라가 찍을 수 있게), 수명 목록에 넣지 않고
        /// (무대가 다음 발사 때 직접 지웁니다), 느리게 돌립니다(0.2초짜리는 형태가 안 읽힙니다).
        ///
        /// 돌려준 오브젝트의 수명은 부르는 쪽이 책임집니다.
        /// </summary>
        /// <param name="speed">재생 배속. 1이면 실제와 같은 속도입니다.</param>
        /// <returns>뿜을 레이어가 하나도 없으면 null</returns>
        /// <param name="worldPose">
        /// 무대 원점이 아닌 자리에 놓아야 할 때 (참격은 호 위에 놓입니다).
        /// 호를 따라 퍼뜨리는 레이어는 <paramref name="arc"/>가 자리를 정하므로 무시됩니다.
        /// </param>
        public static GameObject? CreatePreview(WeaponEffectLayer[]? layers, Transform parent,
            Vector3 size, float speed = 1f, BurstArc? arc = null,
            (Vector3 position, Quaternion rotation)? worldPose = null)
        {
            if (layers == null || layers.Length == 0 || parent == null)
                return null;

            GameObject? host = null;

            foreach (var layer in layers)
            {
                if (layer == null || !layer.enabled)
                    continue;

                if (host == null)
                {
                    host = new GameObject("WeaponAura_LayerBurstPreview");
                    host.transform.SetParent(parent, false);
                    host.transform.localPosition = Vector3.zero;
                    host.transform.localRotation = Quaternion.identity;
                }

                try
                {
                    AddEmitter(host.transform, layer, size, speed, arc, worldPose);
                }
                catch (Exception ex)
                {
#if DEBUG
                    UnityEngine.Debug.LogWarning($"[WeaponAura] 레이어 미리보기 실패: {ex.Message}");
#endif
                }
            }

            return host;
        }

        /// <summary>
        /// 레이어 하나를 <b>한 번 뿜는</b> 이미터로 만들어 붙입니다.
        ///
        /// 오라 레이어와 갈리는 지점이 여기 한 곳에 모여 있습니다 — 반복을 끄고, 초당
        /// 개수를 0으로 만든 다음, <c>rate</c>를 <b>한 번에 뿜는 개수</b>로 다시 읽습니다.
        /// </summary>
        private static void AddEmitter(Transform parent, WeaponEffectLayer layer, Vector3 size,
            float speed, BurstArc? arc,
            (Vector3 position, Quaternion rotation)? worldPose = null)
        {
            var go = WeaponAuraLayerSystem.CreateEmitter(
                layer, parent, Vector3.zero, size, out var particles);

            // 한 번만 뿜습니다. 계속 도는 이미터가 아니라 그 순간의 연출입니다.
            var main = particles.main;
            main.loop = false;
            main.simulationSpeed = Mathf.Max(0.01f, speed);

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, Mathf.Max(1f, layer.rate)),
            });

            // 자리는 이미터마다 갈립니다 — 같은 참격에서 한 겹은 반월을 따라 흐르고
            // 다른 겹은 한 점에서 터지는 조합이 되므로, 부모를 옮겨서는 안 됩니다.
            if (arc != null && layer.arcSpread)
                LayOnArc(go.transform, particles, layer, arc.Value);
            else if (worldPose != null)
                LayAtPoint(go.transform, layer, worldPose.Value.position, worldPose.Value.rotation);

            particles.Clear();
            particles.Play();
        }

        /// <summary>
        /// 이미터를 지정한 자리에 놓습니다. <see cref="WeaponAuraLayerSystem.CreateEmitter"/>가
        /// 넣어 둔 자리 옮기기는 월드 자리를 덮어쓰면서 사라지므로 여기서 다시 먹입니다.
        /// </summary>
        private static void LayAtPoint(Transform emitter, WeaponEffectLayer layer,
            Vector3 position, Quaternion rotation)
        {
            emitter.SetPositionAndRotation(position, rotation);
            emitter.position += rotation * layer.offset;
        }

        /// <summary>
        /// 이미터를 호 위에 눕힙니다.
        ///
        /// Unity의 원 셰이프는 <b>로컬 XY 평면</b>에서 +X부터 반시계로 돕니다(축은 +Z).
        /// 그래서 이미터를 통째로 돌려서 +Z를 참격 판의 법선에, +X를 호가 시작하는 쪽에
        /// 맞춥니다 — 셰이프의 회전값을 오일러로 비트는 것보다 어긋날 여지가 적습니다.
        /// </summary>
        private static void LayOnArc(Transform emitter, ParticleSystem particles,
            WeaponEffectLayer layer, BurstArc arc)
        {
            var axis = arc.Axis.sqrMagnitude > 0.000001f ? arc.Axis.normalized : Vector3.up;

            var start = Vector3.ProjectOnPlane(arc.Start, axis);
            start = start.sqrMagnitude > 0.000001f ? start.normalized : Vector3.right;

            // LookRotation(axis, axis×start)를 쓰면 +X가 정확히 start로 떨어집니다.
            var up = Vector3.Cross(axis, start);

            emitter.SetPositionAndRotation(arc.Center, Quaternion.LookRotation(axis, up));

            // 자리 옮기기는 호의 좌표계에서 먹입니다 (월드 축으로 밀면 참격이 어느 쪽을
            // 보느냐에 따라 매번 다른 데로 갑니다).
            emitter.position += emitter.rotation * layer.offset;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.01f, arc.Radius);

            // 0이면 테두리에만 붙습니다. 안쪽까지 채우면 반월이 원반이 됩니다.
            // 퍼짐 값이 그 띠의 두께가 됩니다 — 한 점에서 뿜을 때의 반경과 같은 자리입니다.
            shape.radiusThickness = Mathf.Clamp01(layer.spread);

            shape.arc = Mathf.Clamp(arc.Degrees, 1f, 360f);
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;
            shape.randomDirectionAmount = 0f;
        }

        private static void Cleanup()
        {
            float now = Time.unscaledTime;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];

                if (live.Go != null && now < live.DieAt)
                    continue;

                if (live.Go != null)
                    UnityEngine.Object.Destroy(live.Go);

                _live.RemoveAt(i);
            }
        }

        /// <summary>모드를 내리거나 설정을 끌 때 — 지금 떠 있는 것을 전부 걷습니다.</summary>
        public static void Clear()
        {
            foreach (var live in _live)
            {
                if (live.Go != null)
                    UnityEngine.Object.Destroy(live.Go);
            }

            _live.Clear();
        }

        /// <summary>ModBehaviour.LateUpdate에서 — 수명이 다한 것을 걷습니다.</summary>
        public static void Tick() => Cleanup();
    }
}
