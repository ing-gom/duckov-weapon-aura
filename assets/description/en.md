# Weapon Aura

Four grade-driven effects: an **aura spreading off your weapon's surface**, a **tracer trail behind every bullet**, a **muzzle flash at the barrel**, and a **coloured slash when you swing a melee weapon**. All four are edited in an in-game window, and anything you don't want can be switched off.

## Weapon Aura — by weapon grade

An aura **spreads outward from your weapon's surface.** It's not a cloud of particles — it's a shell that follows the weapon's shape, so guns and melee weapons alike get wrapped in their own silhouette.

- **Seven grades** — one per item grade 1–7. Special and crafted grades such as 9 or 999 can be added yourself and styled separately.
- **Live 3D preview** — shows the weapon you are actually holding. Pick a grade and its color appears instantly; drag to spin it, or zoom in.
- **12 elemental templates** — Aurora / Fire / Frost / Toxic / Void / Shock / Holy / Blood / Arcane / Plasma / Nature / Shadow. One click swaps the whole look and motion.
- **Shell, waves, particles** — layer count, spread distance, concentric waves, surface particle rate/size/life, and trailing tails.
- **Leave a trail behind** — particles stay where they were born instead of following the weapon, so moving lays a pattern along your path. Turn it off to keep the aura hugging the weapon.
- **Ring** — glowing dots orbiting the weapon. Set count, radius, size, spin and tilt, and give them a <b>different shape</b> from the aura (hearts, stars, …).
- **Flipbook** — plays a tiled sheet frame by frame. Drop a 4×4 sheet into `assets/vfx_textures` and set the tile counts.
- **Global strength** — subtle / normal / intense.

## Bullet Trails — by ammo grade

Every shot leaves a **coloured tail matched to the grade of the ammo you have loaded.** Weapon grade and ammo grade are separate axes, so your gun and your bullets can run different colours.

- **Colour and shape per grade** — head colour, tail colour, trail length, front/back width, opacity, brightness, glow.
- **Trail style** — a continuous `line`, or `stamps` that drop a shape at a fixed spacing along the path. Stamps have per-metre count, size and lifetime.
- **Bullet head** — hide the game's own trail and let the mod draw the bullet itself. Pick its shape (capsule, dot, diamond, arrow, ring, spark, or one you drew), width, length ratio, brightness and colour.
- **Bullet glow** — resize, brighten, recolour or hide the light that follows the bullet. Everything defaults to `vanilla`, so turning it on changes nothing until you move a slider.
- **Live preview** — watch a single bullet fly past. Head shape, glow and stamps are all drawn with the same maths the game uses.
- **Applies to** — my shots only / everyone.

## Muzzle Flash — by ammo grade

Firing bursts a **grade-coloured flash at the barrel.** It reads off the same ammo grade as the trails, so the colour that bursts at the muzzle is the colour that flies downrange.

- **White-hot core, grade colour outside** — looks like a real flash while still reading as a grade.
- **Size, duration, sparks** — including the count, speed, and size of the sparks thrown forward.
- **Hide the game's own flash** — stacking on top of it doubles the brightness. Turn this on when you want the colour clean.
- **Preview loops at a realistic rate of fire** — a flash lasts about 0.05s, so a single burst is too quick to judge. You see it the way you'll see it in a firefight.

## Melee Slash — by weapon grade

The slash that arcs out when you swing a melee weapon is **plain white in the base game.** This recolours it to the weapon's grade and throws particles along the swing. It reads the same grade as the aura, so the colour wrapping the weapon and the colour it leaves behind are one set.

- **Slash colour** — paints the arc that sweeps out when you swing, with its own opacity and brightness. **Size is never touched** — the slash's size shows the weapon's attack range, so scaling it would make the visual lie about where you actually reach.
- **Slash shape** — swaps the picture drawn on the arc: built-in shapes (glow, heart, star, diamond, ring, sparkle), shapes you drew yourself, or any PNG in `assets/vfx_textures`.
- **Scatter** — particles fly out in a flat fan along the swing direction. Count, size, distance, fan width, rise height, spin and lifetime are all separate.
- **Scatter shape** — the same list as the muzzle flash: built-in shapes (glow, heart, star, diamond, ring, sparkle), shapes you drew yourself, and any PNG in `assets/vfx_textures`.
- **Three presets** — blade shards / embers / petals. Each sets half a dozen values at once.
- **Mode** — `Recolour` · `Colour + scatter` (default) · `Scatter only` (removes the game's slash).
- **Applies to** — my weapon only, or everyone.

## The settings window

Opens from the `Aura Settings` button in the pause menu, built with the game's own font and colors. Tabs at the top switch between the four effects.

- **Color picker** — grab a color from the saturation/value square and hue bar, or type an exact value as HEX (`#FF8800`) or R/G/B.
- **Per-grade on/off** — silence a whole grade when you don't want the effect on low-grade gear.
- **Draw your own shape** — a grid pad sits in the Weapon Aura, Muzzle Flash and Melee Slash tabs. Paint one, save it, and it becomes selectable in all three. (Bullet Trails don't use shapes.)
- **Basic / Advanced** — every tab splits the values you reach for often from the ones that fine-tune character. Start with Basic.
- **Share settings** — copies the grade you are editing to the clipboard as one line. Paste it back to apply.
- **Random generator** — rolls colour *and* shape in one click. It's seeded, so note the seed down when you get a combination you like.

## How to use

1. Press `ESC` in game to open the pause menu.
2. Click `Aura Settings`.
3. Pick the `Weapon Aura`, `Bullet Trails`, `Muzzle Flash`, or `Melee Slash` tab at the top.
4. Pick a grade, adjust the color and shape, then press `Save changes`. Saving writes all four tabs at once.

Saved settings load automatically next time. `Restore defaults` only resets the tab you are looking at. Aiming and firing are blocked while the window is open, and `ESC` closes just the window.

## Languages

English · 한국어 · 简体中文 · 繁體中文
