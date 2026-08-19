using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>
    /// 게임 원본 총알 궤적(<c>Projectile</c>의 자식 TrailRenderer)을 숨깁니다.
    ///
    /// 원본 궤적은 짧고 얇은 흰 선(셰이더 Lazer)입니다. 모드 잔상을 굵거나 밝게 켜면
    /// 그 안쪽에 원본 선이 겹쳐 보여서 지저분합니다. 그래서 "모드 잔상으로 대체"
    /// 옵션을 켜면 원본 쪽을 꺼 둡니다.
    ///
    /// <b>필드가 아니라 계층을 훑습니다.</b> 코드상으로는 <c>Projectile.trail</c> 하나와
    /// 비어 있는 <c>otherTrails</c>뿐이지만, 그건 실제로 확인한 두 프리팹
    /// (BulletNormal·BulletSMG) 얘기입니다. 총마다 프리팹이 다르므로 아직 못 본 총알에
    /// 필드로 참조되지 않은 궤적이 붙어 있을 수 있습니다. 자식 TrailRenderer를 전부
    /// 끄면 그런 것까지 덮습니다.
    ///
    /// 모드 잔상은 총알의 자식이 아니라 별도 홀더(<c>WeaponAura_BulletTrails</c>)에
    /// 달려 있어서 여기에 같이 걸릴 일은 없습니다.
    ///
    /// <b>한 번 끄고 마는 게 아니라 발사할 때마다 다시 정합니다.</b>
    /// <c>BulletPool</c>은 프리팹별로 풀 하나를 두고 플레이어와 적이 <b>같은 인스턴스를
    /// 나눠 씁니다</b>. 적용 대상이 "내 총알"(기본값)일 때 내가 쏴서 꺼 놓은 총알이 그대로
    /// 적에게 넘어가면, 적 총알은 모드 잔상도 없고 원본 궤적도 없어서 거의 안 보이게
    /// 됩니다. 그래서 매 발 <see cref="SetSuppressed"/>로 상태를 다시 씁니다.
    ///
    /// 계층 탐색만 인스턴스당 한 번 하고 결과를 캐시합니다 — 연사 무기는 초당 수십 발을
    /// 쏘기 때문에 매번 <c>GetComponentsInChildren</c>을 부르면 그대로 GC 부담이 됩니다.
    ///
    /// 점광원(<c>SodaPointLight</c>)은 건드리지 않습니다. 그건 선이 아니라 총알을
    /// 따라다니는 빛이라, 끄면 잔상이 없는 등급에서 총알이 아예 안 보입니다.
    /// </summary>
    public static class VanillaTrailSuppressor
    {
        /// <summary>
        /// 총알 인스턴스별로 우리가 관리하는 궤적들.
        ///
        /// 처음 봤을 때 <b>켜져 있던</b> 것만 담습니다. 프리팹이 의도적으로 꺼 둔 궤적을
        /// 나중에 켜 버리면 안 됩니다.
        /// </summary>
        private static readonly Dictionary<Projectile, TrailRenderer[]> _trails =
            new Dictionary<Projectile, TrailRenderer[]>();

        /// <summary>
        /// 총알 인스턴스별 <b>몸체 그림</b>. 궤적과 따로 관리합니다.
        ///
        /// 궤적을 숨기는 것과 몸체를 숨기는 것은 조건이 다릅니다. 모드가 자기 총알 머리를
        /// 그릴 때만 몸체를 숨겨야 하고, 머리를 안 그리는데 몸체까지 숨기면 총알이 아무것도
        /// 없이 날아갑니다.
        ///
        /// TrailRenderer는 뺍니다 — 그건 위쪽이 담당합니다. 빛(SodaPointLight)은 Renderer가
        /// 아니라 여기 안 걸립니다. 그건 의도한 것입니다.
        /// </summary>
        private static readonly Dictionary<Projectile, Renderer[]> _bodies =
            new Dictionary<Projectile, Renderer[]>();

        /// <summary>
        /// 이 총알의 원본 그림을 숨길지 정합니다. 발사될 때마다 부릅니다.
        /// </summary>
        /// <param name="suppress">원본 궤적(선)을 숨길지</param>
        /// <param name="suppressBody">
        /// 원본 총알 몸체까지 숨길지.
        ///
        /// 모드가 자기 총알 머리를 그릴 때만 true입니다. 안 그러면 원본 몸체와 모드 머리가
        /// 겹쳐서 두 개로 보입니다 — 특히 속성이 붙은 총(프로스트 등)은 몸체 쪽에 속성
        /// 색·장식이 들어가 있어서 겹친 티가 확 납니다.
        /// </param>
        public static void SetSuppressed(Projectile? projectile, bool suppress, bool suppressBody = false)
        {
            if (projectile == null)
                return;

            try
            {
                if (!_trails.TryGetValue(projectile, out var trails))
                {
                    trails = Collect(projectile);
                    _trails[projectile] = trails;
                }

                foreach (var trail in trails)
                {
                    if (trail == null)
                        continue;

                    trail.enabled = !suppress;
                }

                if (!_bodies.TryGetValue(projectile, out var bodies))
                {
                    bodies = CollectBodies(projectile);
                    _bodies[projectile] = bodies;
                }

                foreach (var body in bodies)
                {
                    if (body == null)
                        continue;

                    body.enabled = !suppressBody;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 원본 궤적 상태 변경 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>이 총알의 원본 궤적을 되돌립니다.</summary>
        public static void Restore(Projectile? projectile) => SetSuppressed(projectile, false, false);

        /// <summary>
        /// 꺼 둔 원본 궤적을 모두 되돌리고 캐시를 비웁니다.
        ///
        /// 옵션을 끌 때·씬이 바뀔 때·모드를 내릴 때 반드시 불러야 합니다. 총알은 판이
        /// 끝날 때까지 풀에서 재사용되기 때문에, 여기서 안 켜면 옵션을 도로 꺼도 원본
        /// 궤적이 그 판 내내 돌아오지 않습니다.
        /// </summary>
        public static void RestoreAll()
        {
            foreach (var pair in _trails)
            {
                foreach (var trail in pair.Value)
                {
                    // 씬이 바뀌면서 파괴된 것들이 섞여 있습니다 (Unity의 가짜 null).
                    if (trail == null)
                        continue;

                    try
                    {
                        trail.enabled = true;
                    }
                    catch
                    {
                        // 파괴 중인 오브젝트. 되돌릴 대상이 아닙니다.
                    }
                }
            }

            _trails.Clear();

            foreach (var pair in _bodies)
            {
                foreach (var body in pair.Value)
                {
                    if (body == null)
                        continue;

                    try
                    {
                        body.enabled = true;
                    }
                    catch
                    {
                        // 파괴 중인 오브젝트. 되돌릴 대상이 아닙니다.
                    }
                }
            }

            _bodies.Clear();
        }

        /// <summary>
        /// 처음 봤을 때 켜져 있던 <b>몸체 그림</b>을 모읍니다.
        ///
        /// 필드(<c>Projectile.mesh</c>) 하나만 보지 않고 계층을 훑는 이유는 궤적 쪽과
        /// 같습니다 — 속성이 붙은 총알은 몸체 말고도 장식용 그림이 자식으로 더 달려 있고,
        /// 그것이 필드로 참조된다는 보장이 없습니다. 실제로 그 장식이 모드 머리와 겹쳐
        /// 보이는 것이 이 함수가 생긴 이유입니다.
        ///
        /// TrailRenderer는 뺍니다(위쪽 담당). 빛은 Renderer가 아니라 애초에 안 걸립니다.
        /// </summary>
        private static Renderer[] CollectBodies(Projectile projectile)
        {
            var found = projectile.GetComponentsInChildren<Renderer>(true);

            var kept = new List<Renderer>(found.Length);
            foreach (var renderer in found)
            {
                if (renderer == null || renderer is TrailRenderer)
                    continue;

                if (renderer.enabled)
                    kept.Add(renderer);
            }

            return kept.ToArray();
        }

        /// <summary>처음 봤을 때 켜져 있던 자식 TrailRenderer만 모읍니다.</summary>
        private static TrailRenderer[] Collect(Projectile projectile)
        {
            var found = projectile.GetComponentsInChildren<TrailRenderer>(true);

            var kept = new List<TrailRenderer>(found.Length);
            foreach (var trail in found)
            {
                if (trail != null && trail.enabled)
                    kept.Add(trail);
            }

            return kept.ToArray();
        }
    }
}
