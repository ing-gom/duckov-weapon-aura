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
        public const string TitleTrail = "window_title_trail";
        public const string TitleMuzzle = "window_title_muzzle";
        public const string Close = "window_close";
    }

    /// <summary>창 상단 탭</summary>
    public static class Tab
    {
        public const string Aura = "tab_aura";
        public const string Trail = "tab_trail";
        public const string Muzzle = "tab_muzzle";
    }

    /// <summary>총구 화염 탭</summary>
    public static class Muzzle
    {
        public const string SectionGrade = "muzzle_section_grade";
        public const string FollowAmmo = "muzzle_follow_ammo";
        public const string FollowState = "muzzle_follow_state";
        public const string ManualState = "muzzle_manual_state";
        public const string AppliesToLoaded = "muzzle_applies_to_loaded";
        public const string PreviewOnly = "muzzle_preview_only";

        public const string StatusEditing = "muzzle_status_editing";

        public const string On = "muzzle_on";
        public const string Off = "muzzle_off";
        public const string OffNotice = "muzzle_off_notice";

        public const string Scope = "muzzle_scope";
        public const string ScopePlayer = "muzzle_scope_player";
        public const string ScopeEveryone = "muzzle_scope_everyone";

        public const string Mode = "muzzle_mode";
        public const string ModeTint = "muzzle_mode_tint";
        public const string ModeReplace = "muzzle_mode_replace";
        public const string ModeOverlay = "muzzle_mode_overlay";

        public const string GradeOn = "muzzle_grade_on";
        public const string GradeOff = "muzzle_grade_off";
        public const string GradeDisabledNotice = "muzzle_grade_disabled_notice";

        public const string SectionColorInner = "muzzle_section_color_inner";
        public const string SectionColorOuter = "muzzle_section_color_outer";
        public const string SectionShape = "muzzle_section_shape";
        public const string SectionSparks = "muzzle_section_sparks";

        public const string FieldSize = "muzzle_field_size";
        public const string FieldSizeScale = "muzzle_field_size_scale";
        public const string FieldDuration = "muzzle_field_duration";
        public const string FieldAlpha = "muzzle_field_alpha";
        public const string FieldIntensity = "muzzle_field_intensity";
        public const string FieldSparkCount = "muzzle_field_spark_count";
        public const string FieldSparkDistance = "muzzle_field_spark_distance";
        public const string FieldSparkRise = "muzzle_field_spark_rise";
        public const string FieldSparkSize = "muzzle_field_spark_size";

        public const string PreviewNote = "muzzle_preview_note";

        // ── 모양 · 프리셋 ──
        public const string SectionLook = "muzzle_section_look";
        public const string ShapePrev = "muzzle_shape_prev";
        public const string ShapeNext = "muzzle_shape_next";
        public const string ShapeRescan = "muzzle_shape_rescan";
        public const string ShapeGlow = "muzzle_shape_glow";
        public const string ShapeHeart = "muzzle_shape_heart";
        public const string ShapeStar = "muzzle_shape_star";
        public const string ShapeDiamond = "muzzle_shape_diamond";
        public const string ShapeRing = "muzzle_shape_ring";
        public const string ShapeSparkle = "muzzle_shape_sparkle";

        public const string PresetFlash = "muzzle_preset_flash";
        public const string PresetHearts = "muzzle_preset_hearts";
        public const string PresetStardust = "muzzle_preset_stardust";
        public const string PresetApplied = "muzzle_preset_applied";

        // ── 도형 그리기 ──
        public const string SectionDraw = "muzzle_section_draw";
        public const string ShapeName = "muzzle_shape_name";
        public const string ShapeSave = "muzzle_shape_save";
        public const string ShapeRandom = "muzzle_shape_random";
        public const string ShapeClear = "muzzle_shape_clear";
        public const string ShapeLoad = "muzzle_shape_load";
        public const string ShapeDelete = "muzzle_shape_delete";
        public const string ShapeHint = "muzzle_shape_hint";
        public const string ShapeSaved = "muzzle_shape_saved";
        public const string ShapeLoaded = "muzzle_shape_loaded";
        public const string ShapeDeleted = "muzzle_shape_deleted";
        public const string ShapeRandomised = "muzzle_shape_randomised";
        public const string ShapeNeedName = "muzzle_shape_need_name";
        public const string ShapeEmpty = "muzzle_shape_empty";
        public const string ShapeFull = "muzzle_shape_full";
        public const string ShapeSaveFailed = "muzzle_shape_save_failed";
        public const string ShapeNotDrawn = "muzzle_shape_not_drawn";

        public const string FieldSparkSpread = "muzzle_field_spark_spread";
        public const string FieldSparkSpin = "muzzle_field_spark_spin";
        public const string StretchOn = "muzzle_stretch_on";
        public const string StretchOff = "muzzle_stretch_off";

        public const string RandomApplied = "muzzle_random_applied";
        public const string ResetDone = "muzzle_reset_done";
    }

    /// <summary>탄환 잔상 탭</summary>
    public static class Trail
    {
        public const string SectionGrade = "trail_section_grade";
        public const string FollowAmmo = "trail_follow_ammo";
        public const string FollowState = "trail_follow_state";
        public const string ManualState = "trail_manual_state";
        public const string AppliesToLoaded = "trail_applies_to_loaded";
        public const string PreviewOnly = "trail_preview_only";

        public const string StatusAmmo = "trail_status_ammo";
        public const string StatusGrade = "trail_status_grade";
        public const string StatusNoAmmo = "trail_status_no_ammo";
        public const string StatusEditing = "trail_status_editing";

        public const string On = "trail_on";
        public const string Off = "trail_off";
        public const string OffNotice = "trail_off_notice";

        public const string Scope = "trail_scope";
        public const string ScopePlayer = "trail_scope_player";
        public const string ScopeEveryone = "trail_scope_everyone";

        public const string GradeOn = "trail_grade_on";
        public const string GradeOff = "trail_grade_off";
        public const string GradeDisabledNotice = "trail_grade_disabled_notice";

        public const string SectionColorHead = "trail_section_color_head";
        public const string SectionColorTail = "trail_section_color_tail";
        public const string SectionShape = "trail_section_shape";

        public const string FieldLength = "trail_field_length";
        public const string FieldStartWidth = "trail_field_start_width";
        public const string FieldEndWidth = "trail_field_end_width";
        public const string FieldAlpha = "trail_field_alpha";
        public const string FieldIntensity = "trail_field_intensity";

        public const string GlowOn = "trail_glow_on";
        public const string GlowOff = "trail_glow_off";

        public const string RandomApplied = "trail_random_applied";
        public const string ResetDone = "trail_reset_done";
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
