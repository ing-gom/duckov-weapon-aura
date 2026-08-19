# Weapon Aura

Four grade-driven effects: an **aura off the weapon's surface**, **bullet trails**, a **muzzle flash**, and a **melee slash**. All four are editable in-game, and anything you don't want can be switched off.

## Weapon Aura — by weapon grade

Not loose particles but a shell that follows the weapon's shape, so it wraps the silhouette of guns and melee weapons alike.

- **Seven grades** — mapped to item grades 1–7. Special grades like 9 or 999 can be added by hand.
- **Live 3D preview** — shows the weapon you are holding, or the one you picked from the list. Drag to rotate, pull the `Zoom` slider, `Front` to reset.
- **12 elemental templates** — aurora / flame / frost / poison / void / shock / holy / blood / arcane / plasma / nature / shadow.
- **Shell, pulses, particles** — layer count, reach, concentric pulses, surface particle amount, size and lifetime, plus trails.
- **Leave it behind** — particles stick where they spawn, laying a pattern along your path.
- **Ring** — orbiting points of light. Count, radius, size, spin and tilt, and it can use a <b>different shape</b> from the aura.
- **Layers** — stack up to four extra particle layers. Each picks its own emission point (muzzle / body / barrel / whole weapon) plus direction, colour, shape and lifetime.
- **Overall strength** — light / normal / strong.

## Bullet Trails — by ammo grade

Driven by the ammo you have loaded. That's a different axis from weapon grade, so gun and bullet can run different colours.

- **Colour and form per grade** — head colour, tail colour, length, taper, alpha, brightness, glow.
- **Trail style** — a continuous `line`, or `stamps` dropped at a fixed spacing (count per metre, size, lifetime).
- **Bullet head** — hides the game's own tracer and draws the bullet itself. Shape, width, aspect, brightness, colour.
- **Bullet glow** — resize, brighten, recolour or disable the light that follows the bullet.
- **Moving preview** — a single round crossing the frame. Colours and shapes come from the same source as the real thing.
- **Applies to** — my bullets only / all bullets.

## Muzzle Flash — by ammo grade

Shares the ammo grade with trails, so the colour that bursts at the barrel carries on down the shot.

- **White core, grade-coloured edge** — reads as fire and as a grade at the same time.
- **Size, duration, sparks** — including the count, speed and size of the sparks thrown forward.
- **Layers** — fire extra particles on top of the flash as you shoot.
- **Mode** — `tint only` · `overlay` · `replace`. Stacking on the game's flash doubles the brightness, so use replace when you want the colour clean.
- **Looping preview** — a flash lasts 0.05s, too short to read once. It replays on a slower-than-real interval so the shape registers.
- **Applies to** — my gun only / every gun.

## Melee Slash — by weapon grade

The slash is a single white arc by default. This recolours it by weapon grade and scatters particles along the swing. Same grade axis as the aura, so the two read as one set.

- **Slash colour** — paint the arc, with its own alpha and brightness. Size is left alone: it tells you the weapon's reach.
- **Slash and scatter shapes** — pick the image on the arc and the particle shape separately.
- **Scatter** — particles fly out in a flat fan along the swing. Count, size, distance, width, rise, spin, lifetime.
- **Layers** — throw extra particles as you swing. They <b>spread along the arc</b> by default, sitting right on the slash the game draws.
- **Three presets** — blade shards / embers / petals.
- **Mode** — `tint only` · `tint + scatter` (default) · `scatter only` (removes the game's slash).
- **Applies to** — my weapon only / every weapon.

## The settings window

Opens from the `Aura Settings` button in the pause menu. Tabs at the top switch between the four effects.

- **Colour picker** — saturation/value square and hue bar, or exact HEX (`#FF8800`) and R/G/B entry.
- **Per-grade on/off** — silence a grade you don't want the effect on.
- **Per-weapon settings** — style <b>one specific gun</b> instead of a grade. Every weapon in the game is browsable, so you can set one up before you ever find it.
- **Shape picker** — browse built-in shapes, shapes you drew, and PNGs from `vfx_textures` as pictures. 52 PNGs ship with the mod.
- **Draw your own shape** — paint on a grid pad, save it, and it becomes selectable in every tab.
- **Basic / Advanced** — the values you reach for often, split from the ones that fine-tune character.
- **Share settings** — copy one tab, or all four at once, as a single line. Pasting goes through a confirmation box.
- **Random generator** — rolls colour *and* shape. It's seeded, so you can get a combination back.

## How to use

1. Press `ESC` for the pause menu and hit `Aura Settings`.
2. Pick the `Weapon Aura` · `Bullet Trails` · `Muzzle Flash` · `Melee Slash` tab.
3. Choose a grade, tune colour and form, then `Save`. All four tabs save together.

Saved settings load on the next run. `Reset defaults` only reverts the tab you are looking at. Aiming and firing are blocked while the window is open, and `ESC` closes just the window.

## Languages

한국어 · English · 简体中文 · 繁體中文
