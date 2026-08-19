using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WeaponAura.Helpers
{
    /// <summary>
    /// <see cref="WeaponModelSource"/>를 카탈로그 <b>전체</b>에 돌려 보는 검증.
    ///
    /// 표본 몇 개가 되는 것과 142정이 되는 것은 다른 이야기입니다. 라이브러리 UI는
    /// "아무 무기나 눌러도 미리보기가 뜬다"를 전제로 만들 것이므로, 그 전제가 실제로
    /// 몇 %인지 먼저 숫자로 확인합니다.
    ///
    /// 한 정씩 만들고 재고 즉시 지웁니다 — 142정을 동시에 띄우면 프레임이 튑니다.
    /// </summary>
    public static class WeaponGraphicProbe
    {
        /// <summary>실패·자리표시자를 이름까지 남길 최대 개수 (로그가 넘치지 않게)</summary>
        private const int MaxListed = 15;

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WeaponAura 무기 모델 생성 검증 (카탈로그 전체) ===");

            var all = WeaponCatalog.All;
            if (all.Count == 0)
            {
                sb.AppendLine("카탈로그가 비어 있습니다.");
                string empty = sb.ToString();
                UnityEngine.Debug.Log(empty);
                return empty;
            }

            var host = new GameObject("WeaponAura_ModelProbe");
            host.transform.position = new Vector3(0f, -2000f, 0f);

            int okGun = 0, okMelee = 0, failGun = 0, failMelee = 0, placeholder = 0;
            var bySource = new Dictionary<string, int>();
            var failures = new List<string>();
            var placeholders = new List<string>();

            float startedAt = Time.realtimeSinceStartup;

            try
            {
                foreach (var entry in all)
                {
                    var handle = WeaponModelSource.Create(entry.TypeId, host.transform);

                    if (handle == null)
                    {
                        if (entry.Kind == WeaponKind.Gun)
                            failGun++;
                        else
                            failMelee++;

                        if (failures.Count < MaxListed)
                            failures.Add($"{entry.Name}(#{entry.TypeId}, {entry.GunClass ?? "근접"})");

                        continue;
                    }

                    try
                    {
                        if (entry.Kind == WeaponKind.Gun)
                            okGun++;
                        else
                            okMelee++;

                        bySource.TryGetValue(handle.Source, out int count);
                        bySource[handle.Source] = count + 1;

                        if (handle.IsPlaceholder)
                        {
                            placeholder++;
                            if (placeholders.Count < MaxListed)
                                placeholders.Add($"{entry.Name}(#{entry.TypeId}, 등급 {entry.Quality})");
                        }
                    }
                    finally
                    {
                        // 다음 무기로 넘어가기 전에 반드시 지웁니다.
                        handle.Dispose();
                    }
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }

            float elapsed = Time.realtimeSinceStartup - startedAt;

            sb.AppendLine($"대상: {all.Count}정 (총기 {okGun + failGun} · 근접 {okMelee + failMelee})");
            sb.AppendLine($"성공: 총기 {okGun} · 근접 {okMelee}");
            sb.AppendLine($"실패: 총기 {failGun} · 근접 {failMelee}");
            sb.AppendLine($"자리표시자(TestGunItemGraphic): {placeholder}정");
            sb.AppendLine($"소요: {elapsed:F2}초 (한 정당 평균 {elapsed / Mathf.Max(1, all.Count) * 1000f:F1}ms)");

            sb.AppendLine("경로별:");
            foreach (var pair in bySource)
                sb.AppendLine($"    {pair.Key,-24} {pair.Value,4}정");

            if (failures.Count > 0)
            {
                sb.AppendLine($"실패 목록(최대 {MaxListed}):");
                foreach (string name in failures)
                    sb.AppendLine($"    {name}");
            }

            if (placeholders.Count > 0)
            {
                sb.AppendLine($"자리표시자 목록(최대 {MaxListed}):");
                foreach (string name in placeholders)
                    sb.AppendLine($"    {name}");
            }

            string text = sb.ToString();
            UnityEngine.Debug.Log(text);
            return text;
        }
    }
}
