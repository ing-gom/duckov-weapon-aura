namespace WeaponAura.Settings
{
    /// <summary>
    /// 발사 관련 효과를 누구에게 적용할지. 탄환 잔상과 총구 화염이 함께 씁니다.
    /// </summary>
    public enum EffectScope
    {
        /// <summary>플레이어가 쏜 것만</summary>
        PlayerOnly = 0,
        /// <summary>적까지 전부</summary>
        Everyone = 1,
    }
}
