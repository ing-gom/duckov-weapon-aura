using System;
using System.Text;
using UnityEngine;

namespace WeaponAura.Systems
{
    /// <summary>설정 코드가 무엇을 담고 있는지</summary>
    public enum ShareKind
    {
        Aura = 0,
        Trail = 1,
        Muzzle = 2,
        Melee = 3,

        /// <summary>네 이펙트 전부 (오라의 겹까지 포함)</summary>
        All = 4,
    }

    /// <summary>코드를 풀어낸 결과</summary>
    public sealed class ShareContent
    {
        public ShareKind Kind;
        public WeaponAuraProfile? Aura;
        public BulletTrailProfile? Trail;
        public MuzzleFlashProfile? Muzzle;
        public MeleeSlashProfile? Melee;

        /// <summary>오라에 얹힌 겹 수 (미리 보여 주기용)</summary>
        public int LayerCount => Aura?.layers?.Length ?? 0;
    }

    /// <summary>
    /// 설정을 한 줄짜리 코드로 주고받습니다.
    ///
    /// 예전에는 <b>오라 프로필 하나</b>만 담았습니다. 무기 전용 설정을 만들어 놓고 남에게
    /// 주려면 네 이펙트가 다 가야 하는데 오라만 갔습니다. 그래서 코드에 <b>무엇이 들었는지</b>를
    /// 함께 적어, 하나만 담은 것과 통째로 담은 것을 같은 방식으로 다룹니다.
    ///
    /// 옛 코드(<c>WAURA1:</c>)도 그대로 읽습니다 — 이미 나눠 가진 것들이 있습니다.
    ///
    /// 프로필을 <b>문자열로</b> 담는 이유는 저장 파일과 같습니다. Unity는 중첩된 사용자
    /// 정의 클래스를 담지 못해서, 한 겹만 들어가도 조용히 사라집니다.
    /// </summary>
    public static class ShareCodec
    {
        /// <summary>지금 형식. 무엇이 들었는지를 함께 적습니다.</summary>
        public const string Prefix = "WAURA2:";

        /// <summary>예전 형식 — 오라 프로필 하나. 읽기만 합니다.</summary>
        public const string LegacyPrefix = "WAURA1:";

        [Serializable]
        private class Payload
        {
            public int kind;
            public string aura = "";
            public string trail = "";
            public string muzzle = "";
            public string melee = "";
        }

        public static string Encode(ShareKind kind,
            WeaponAuraProfile? aura, BulletTrailProfile? trail,
            MuzzleFlashProfile? muzzle, MeleeSlashProfile? melee)
        {
            var payload = new Payload
            {
                kind = (int)kind,
                aura = ProfileJson.One(aura),
                trail = ProfileJson.One(trail),
                muzzle = ProfileJson.One(muzzle),
                melee = ProfileJson.One(melee),
            };

            string json = JsonUtility.ToJson(payload);
            return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        /// <summary>코드를 풀어냅니다. 형식이 아니거나 깨졌으면 null.</summary>
        public static ShareContent? Decode(string? raw)
        {
            string code = (raw ?? "").Trim();

            try
            {
                if (code.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    string json = Encoding.UTF8.GetString(
                        Convert.FromBase64String(code.Substring(Prefix.Length)));

                    var payload = JsonUtility.FromJson<Payload>(json);
                    if (payload == null)
                        return null;

                    return new ShareContent
                    {
                        Kind = (ShareKind)payload.kind,
                        Aura = ProfileJson.OneFrom<WeaponAuraProfile>(payload.aura),
                        Trail = ProfileJson.OneFrom<BulletTrailProfile>(payload.trail),
                        Muzzle = ProfileJson.OneFrom<MuzzleFlashProfile>(payload.muzzle),
                        Melee = ProfileJson.OneFrom<MeleeSlashProfile>(payload.melee),
                    };
                }

                if (code.StartsWith(LegacyPrefix, StringComparison.Ordinal))
                {
                    string json = Encoding.UTF8.GetString(
                        Convert.FromBase64String(code.Substring(LegacyPrefix.Length)));

                    var aura = ProfileJson.OneFrom<WeaponAuraProfile>(json);
                    if (aura == null)
                        return null;

                    return new ShareContent { Kind = ShareKind.Aura, Aura = aura };
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WeaponAura] 설정 코드 해석 실패: {ex.Message}");
            }

            return null;
        }

        /// <summary>코드 앞부분만 보고 우리 것인지 판단합니다 (입력칸 안내용).</summary>
        public static bool LooksLikeCode(string? raw)
        {
            string code = (raw ?? "").Trim();

            return code.StartsWith(Prefix, StringComparison.Ordinal)
                   || code.StartsWith(LegacyPrefix, StringComparison.Ordinal);
        }
    }
}
