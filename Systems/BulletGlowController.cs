using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>총알 발광체 색을 어디서 가져올지.</summary>
    public enum BulletGlowColorMode
    {
        /// <summary>게임 원본 색 그대로 (기본)</summary>
        Vanilla = 0,
        /// <summary>잔상 앞쪽 색을 따라감</summary>
        FollowTrail = 1,
        /// <summary>따로 고른 색</summary>
        Custom = 2,
    }

    /// <summary>
    /// 총알을 따라다니는 발광체(<c>SodaPointLight</c>)를 등급별로 조절합니다.
    ///
    /// 원본 총알에는 본체 모델이 없고 <b>궤적 + 이 발광체</b>가 전부입니다. 그래서 궤적만
    /// 바꾸면 총알 주변 빛은 여전히 게임 기본색으로 남아 등급 색과 따로 놉니다.
    ///
    /// <b>값을 직접 쓰지 않고 원본 대비 배율로 조절합니다.</b> 발광체의 기본 색·크기는
    /// 총알 프리팹마다 다르고, 우리가 아는 값이 아닙니다. 절대값을 넣으면 어떤 총에서는
    /// 알맞고 어떤 총에서는 터무니없이 밝아집니다. 처음 본 순간의 값을 기억해 두고
    /// 거기에 배율을 곱하면, 배율 1이 곧 "원본 그대로"가 됩니다.
    ///
    /// <c>SodaPointLight</c>는 색·경도·감쇠가 모두 public 프로퍼티이고 세터가
    /// <c>SyncToLight()</c>로 MaterialPropertyBlock을 갱신합니다. 그래서 패치 없이
    /// 값만 대입하면 됩니다.
    ///
    /// <b>발사할 때마다 다시 씁니다.</b> 총알은 <c>BulletPool</c>에서 아군·적이 같은
    /// 인스턴스를 나눠 씁니다. 한 번 칠하고 두면 내가 쏜 색이 적 총알에 그대로 넘어갑니다.
    /// </summary>
    public static class BulletGlowController
    {
        /// <summary>처음 봤을 때의 값. 되돌릴 기준이자 배율의 기준입니다.</summary>
        private sealed class Original
        {
            public Color Color;
            public Vector3 LocalScale;
            public bool Enabled;
        }

        private static readonly Dictionary<SodaPointLight, Original> _originals =
            new Dictionary<SodaPointLight, Original>();

        /// <summary>총알 인스턴스별 발광체 목록. 계층 탐색은 한 번만 합니다.</summary>
        private static readonly Dictionary<Projectile, SodaPointLight[]> _lights =
            new Dictionary<Projectile, SodaPointLight[]>();

        /// <summary>이 총알의 발광체를 프로필대로 칠합니다.</summary>
        public static void Apply(Projectile? projectile, BulletTrailProfile? profile)
        {
            if (projectile == null || profile == null)
                return;

            try
            {
                foreach (var light in Collect(projectile))
                {
                    if (light == null || !_originals.TryGetValue(light, out var original))
                        continue;

                    light.enabled = original.Enabled && profile.glowVisible;
                    if (!light.enabled)
                        continue;

                    light.LightColor = BuildColor(original, profile);
                    light.transform.localScale = original.LocalScale * Mathf.Max(0f, profile.glowScale);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                UnityEngine.Debug.LogWarning($"[WeaponAura] 발광체 조절 실패: {ex.Message}");
#endif
            }
        }

        /// <summary>이 총알의 발광체를 원래대로 되돌립니다.</summary>
        public static void Restore(Projectile? projectile)
        {
            if (projectile == null)
                return;

            // 아직 한 번도 손대지 않은 총알까지 계층을 훑을 이유는 없습니다.
            if (!_lights.TryGetValue(projectile, out var lights))
                return;

            foreach (var light in lights)
                RestoreOne(light);
        }

        /// <summary>
        /// 손댄 발광체를 모두 되돌리고 기억을 비웁니다.
        /// 옵션을 끌 때·씬이 바뀔 때·모드를 내릴 때 반드시 불러야 합니다 —
        /// 총알은 판이 끝날 때까지 풀에서 재사용됩니다.
        /// </summary>
        public static void RestoreAll()
        {
            foreach (var pair in _originals)
                RestoreOne(pair.Key);

            _originals.Clear();
            _lights.Clear();
        }

        private static void RestoreOne(SodaPointLight? light)
        {
            // 씬이 바뀌면서 파괴된 것들이 섞여 있습니다 (Unity의 가짜 null).
            if (light == null || !_originals.TryGetValue(light, out var original))
                return;

            try
            {
                light.enabled = original.Enabled;
                light.LightColor = original.Color;
                light.transform.localScale = original.LocalScale;
            }
            catch
            {
                // 파괴 중인 오브젝트. 되돌릴 대상이 아닙니다.
            }
        }

        private static SodaPointLight[] Collect(Projectile projectile)
        {
            if (_lights.TryGetValue(projectile, out var cached))
                return cached;

            var found = projectile.GetComponentsInChildren<SodaPointLight>(true);
            _lights[projectile] = found;

            foreach (var light in found)
            {
                if (light == null || _originals.ContainsKey(light))
                    continue;

                _originals[light] = new Original
                {
                    Color = light.LightColor,
                    LocalScale = light.transform.localScale,
                    Enabled = light.enabled,
                };
            }

            return found;
        }

        private static Color BuildColor(Original original, BulletTrailProfile profile)
            => BuildColor(original.Color, profile);

        /// <summary>
        /// 밝기는 원본 세기를 기준으로 곱합니다.
        ///
        /// 발광체 색은 HDR이라 채널이 1을 넘습니다. 고른 색을 그대로 넣으면 채널이 1에서
        /// 막혀 원본보다 어두워집니다. 그래서 <b>색조는 고른 색에서, 세기는 원본에서</b>
        /// 가져와 합칩니다 — 배율 1에서 원본과 같은 밝기의 다른 색이 됩니다.
        ///
        /// 설정 창 미리보기도 이 함수를 씁니다. 여기서 갈라지면 미리보기에서 고른 색이
        /// 게임에서 다르게 나옵니다.
        /// </summary>
        public static Color BuildColor(Color vanilla, BulletTrailProfile profile)
        {
            float intensity = Mathf.Max(0f, profile.glowIntensity);

            if (profile.glowColorMode == BulletGlowColorMode.Vanilla)
                return new Color(vanilla.r * intensity, vanilla.g * intensity, vanilla.b * intensity, vanilla.a);

            var tint = profile.glowColorMode == BulletGlowColorMode.FollowTrail
                ? profile.colorStart
                : profile.glowColor;

            float tintPeak = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
            if (tintPeak <= 0.0001f)
                return new Color(0f, 0f, 0f, vanilla.a);

            float basePeak = Peak(vanilla);

            // 원본이 검은색이면 기준이 없습니다. 그때는 1을 기준으로 씁니다.
            if (basePeak <= 0.0001f)
                basePeak = 1f;

            float scale = basePeak / tintPeak * intensity;

            return new Color(tint.r * scale, tint.g * scale, tint.b * scale, vanilla.a);
        }

        public static float Peak(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

        // ── 미리보기용 원본 색 ────────────────────────────────────

        /// <summary>
        /// 프리팹을 못 읽었을 때 쓸 색.
        ///
        /// 게임 총알 빛은 따뜻한 계열입니다(궤적 머티리얼의 발광색도 주황이었습니다).
        /// 흰색으로 두면 미리보기만 차갑게 떠서 실제와 인상이 달라집니다.
        /// </summary>
        private static readonly Color FallbackVanillaColor = new Color(2.4f, 1.5f, 0.7f, 1f);

        private static Projectile? _sampledFrom;
        private static Color _sampledColor = FallbackVanillaColor;

        /// <summary>
        /// 설정 창 미리보기용 — 지금 든 총 총알의 원본 발광체 색.
        ///
        /// 런타임 캐시(<see cref="_originals"/>)는 한 발이라도 쏴야 채워집니다. 설정 창은
        /// 로비에서도 열리므로 프리팹에서 직접 읽습니다. 프리팹은 바뀌지 않으니
        /// 같은 프리팹이면 다시 훑지 않습니다.
        /// </summary>
        public static Color SampleVanillaColor()
        {
            try
            {
                var prefab = HeldBulletPrefab();
                if (prefab == null)
                    return _sampledColor;

                if (ReferenceEquals(prefab, _sampledFrom))
                    return _sampledColor;

                _sampledFrom = prefab;

                var light = prefab.GetComponentInChildren<SodaPointLight>(true);
                _sampledColor = light != null ? light.LightColor : FallbackVanillaColor;
            }
            catch
            {
                // 로비 등 플레이어가 없는 상태. 마지막에 읽은 값을 그대로 씁니다.
            }

            return _sampledColor;
        }

        private static Projectile? HeldBulletPrefab()
        {
            var player = CharacterMainControl.Main;
            var holder = player != null ? player.agentHolder : null;
            var agent = holder != null ? holder.CurrentHoldItemAgent : null;

            if (agent is ItemAgent_Gun gun && gun.GunItemSetting != null && gun.GunItemSetting.bulletPfb != null)
                return gun.GunItemSetting.bulletPfb;

            var prefabs = Duckov.Utilities.GameplayDataSettings.Prefabs;
            return prefabs != null ? prefabs.DefaultBullet : null;
        }
    }
}
