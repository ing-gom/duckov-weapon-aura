using Ducky.Sdk.Attributes;
using Ducky.Sdk.Localizations;

namespace WeaponAura;

/// <summary>
/// 로컬라이제이션 키 모음.
///
/// Ducky.Sdk가 이 클래스를 보고 <c>L</c> 클래스를 생성합니다. 코드에서는 항상
/// <c>L.Window.Title</c>처럼 <c>L</c>을 통해 읽고, 문자열 리터럴을 직접 쓰지 않습니다.
/// 키를 추가하면 assets/Locales의 ko/en/zh/zh-hant CSV 네 곳에 모두 넣어야 합니다.
/// </summary>
[LanguageSupport("zh", "en", "ko", "zh-hant")]
public static class LK
{
    public static class Menu
    {
        /// <summary>일시정지 메뉴에 추가되는 버튼</summary>
        public const string OpenButton = "menu_open_button";
    }

    public static class Window
    {
        public const string Title = "window_title";
        public const string TitleWithTier = "window_title_with_tier";
        public const string Close = "window_close";
    }

    public static class Preview
    {
        public const string Zoom = "preview_zoom";
        public const string ResetView = "preview_reset_view";
        public const string NoWeapon = "preview_no_weapon";
        public const string NoModel = "preview_no_model";
        public const string NoParts = "preview_no_parts";
        public const string NoSilhouette = "preview_no_silhouette";
        public const string Error = "preview_error";
    }

    public static class Tier
    {
        public const string SectionLabel = "tier_section_label";
        public const string FollowWeapon = "tier_follow_weapon";
        public const string FollowState = "tier_follow_state";
        public const string ManualState = "tier_manual_state";
        public const string AppliesToHeld = "tier_applies_to_held";
        public const string PreviewOnly = "tier_preview_only";

        // ── 티어 추가 / 삭제 ──
        public const string GradeField = "tier_grade_field";
        public const string Add = "tier_add";
        public const string Remove = "tier_remove";
        public const string Added = "tier_added";
        public const string Removed = "tier_removed";
        public const string Duplicate = "tier_duplicate";
        public const string InvalidGrade = "tier_invalid_grade";
        public const string OutOfRange = "tier_out_of_range";
        public const string BuiltinLocked = "tier_builtin_locked";
    }

    public static class Status
    {
        public const string Weapon = "status_weapon";
        public const string Grade = "status_grade";
        public const string Editing = "status_editing";
    }

    public static class Section
    {
        public const string ColorInner = "section_color_inner";
        public const string ColorOuter = "section_color_outer";
        public const string Shape = "section_shape";
        public const string Waves = "section_waves";
        public const string Display = "section_display";
        public const string Particles = "section_particles";
        public const string Presets = "section_presets";
    }

    /// <summary>속성 템플릿 12종의 이름</summary>
    public static class Preset
    {
        public const string Aurora = "preset_aurora";
        public const string Fire = "preset_fire";
        public const string Frost = "preset_frost";
        public const string Toxic = "preset_toxic";
        public const string Void = "preset_void";
        public const string Shock = "preset_shock";
        public const string Holy = "preset_holy";
        public const string Blood = "preset_blood";
        public const string Arcane = "preset_arcane";
        public const string Plasma = "preset_plasma";
        public const string Nature = "preset_nature";
        public const string Shadow = "preset_shadow";

        public const string Applied = "preset_applied";
    }

    public static class Field
    {
        public const string Alpha = "field_alpha";
        public const string Intensity = "field_intensity";
        public const string Layers = "field_layers";
        public const string Spread = "field_spread";
        public const string WaveCount = "field_wave_count";
        public const string WaveSpeed = "field_wave_speed";
        public const string Period = "field_period";
        public const string Wobble = "field_wobble";

        // ── 파티클 · 잔상 ──
        public const string EmissionRate = "field_emission_rate";
        public const string ParticleSize = "field_particle_size";
        public const string ParticleLifetime = "field_particle_lifetime";
        public const string TrailOn = "field_trail_on";
        public const string TrailOff = "field_trail_off";
        public const string TrailLength = "field_trail_length";
        public const string TrailWidth = "field_trail_width";
    }

    public static class Display
    {
        /// <summary>지금 편집 중인 티어에만 적용되는 켜기/끄기</summary>
        public const string TierOn = "display_tier_on";

        public const string TierOff = "display_tier_off";
        public const string TierDisabledNotice = "display_tier_disabled_notice";

        public const string Strength = "display_strength";
        public const string Subtle = "display_subtle";
        public const string Normal = "display_normal";
        public const string Intense = "display_intense";
    }

    public static class Action
    {
        public const string Save = "action_save";
        public const string Random = "action_random";
        public const string ResetDefaults = "action_reset_defaults";
        public const string Saved = "action_saved";
        public const string SaveFailed = "action_save_failed";
        public const string RandomApplied = "action_random_applied";
        public const string ResetDone = "action_reset_done";
    }
}
