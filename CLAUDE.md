# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A tModLoader mod for Terraria that enhances vanilla weapons/minions, adds new accessories and armor sets, and implements advanced systems like time stop, on-hit effects, and custom keybinds. Depends on the **Luminance** library (bundled as a DLL) for shader/visual effects.

## Build System

tModLoader mods are built via the tModLoader IDE Helper or directly through Visual Studio/Rider. There are no standalone CLI build commands — the game's mod loader compiles and loads the mod at runtime.

- **Build & run:** Launch tModLoader, go to Workshop → Develop Mods → Build & Reload
- **Build only:** Workshop → Develop Mods → Build
- The `.csproj` imports `tModLoader.targets` which handles all Terraria-specific compilation

`build.txt` defines mod metadata:
```
displayName = test
author = test
version = 0.2
modReferences = Luminance
```

## Architecture

### Entry Point

[TestMod.cs](TestMod.cs) — Main mod class. Currently only calls `OnHitEffectsPlayer.LoadRegistry()` / `UnloadRegistry()` to initialize the on-hit effects plugin system.

### On-Hit Effects (Plugin System)

The most architecturally notable system. Located in [Items/Accessories/Effects/](Items/Accessories/Effects/).

- [OnHitEffect.cs](Items/Accessories/Effects/OnHitEffect.cs) — Abstract base class. Subclasses implement `Trigger()`. Supports `ExcludedProjectileTypes` and global cooldowns.
- [OnHitEffectsPlayer.cs](Items/Accessories/Effects/OnHitEffectsPlayer.cs) — Registry + dispatcher. Uses **reflection at load time** to auto-discover all `OnHitEffect` subclasses. Accessories activate effects each frame by calling `Activate<T>(player)`. Per-NPC and per-effect cooldowns prevent spam.
- 16 concrete effect implementations (e.g., `BuffApplyEffect`, `TimeStopEffect`, `TripleDodgeEffect`) — each corresponds to an accessory behavior.

To add a new accessory effect: create a subclass of `OnHitEffect`, implement `Trigger()`, and call `OnHitEffectsPlayer.Activate<YourEffect>(player)` from the accessory's `UpdateAccessory`.

### Global Projectile Enhancements (Partial Class)

[Common/GlobalProjectiles/TestGlobalProjectile.cs](Common/GlobalProjectiles/TestGlobalProjectile.cs) is split across **10 files** by concern:

| File suffix | Responsibility |
|---|---|
| *(main)* | Time stop interception, rarity/damage modifiers, base hooks |
| `.Daybreak` | Daybreak lance tracking and sun burst |
| `.Dragon` | Stardust Dragon lock-on AI |
| `.Spider` | Spider minion range + immunity frames |
| `.TimeStop` | Freeze mechanics |
| `.Phantasm` | Phantasm bow enhancements |
| `.NebulaBlaze` | Nebula Blaze ring spawning |
| `.CorvidRaven` | Raven AI replacement (PreAI returns false) |
| `.MinionClassify` | Minion type classification helpers |
| `.Helpers` | Shared utility methods |

### Player Systems

[Common/Players/CorePlayer.cs](Common/Players/CorePlayer.cs) — Central player modifier handling:
- God mode toggles (two tiers with stat scaling)
- Rarity backup/swap dictionary (preserves original rarities when god mode changes them)
- Damage isolation multipliers for incoming projectile/NPC damage
- Crit damage bonuses

Seven other specialized `ModPlayer` classes in [Common/Players/](Common/Players/) each own a narrow concern (time stop, supercrit stats, flight control, Antares minion management, etc.).

### Key Subsystems

- **Time Stop** — Keybind in [Common/Systems/TimeStopKeybinds.cs](Common/Systems/TimeStopKeybinds.cs), player state in [Common/Players/TimeStopPlayer.cs](Common/Players/TimeStopPlayer.cs), projectile behavior in `TestGlobalProjectile.TimeStop.cs`, buff in [Buffs/TimeStoppedBuff.cs](Buffs/TimeStoppedBuff.cs).
- **Antares Armor** — Armor set in [Items/Armor/](Items/Armor/), minion in [Projectiles/Minions/AntaresMinion.cs](Projectiles/Minions/AntaresMinion.cs), beam in [Projectiles/Minions/AntaresBeam.cs](Projectiles/Minions/AntaresBeam.cs), player logic split between [AntaresArmorPlayer.cs](Common/Players/AntaresArmorPlayer.cs) and [AntaresMinionPlayer.cs](Common/Players/AntaresMinionPlayer.cs).
- **Dash System** — [Items/Accessories/Dashes/PlayerDashManager.cs](Items/Accessories/Dashes/PlayerDashManager.cs) coordinates `LongDash` and `ShortDash` items; `DashPlayer.cs` holds per-player state.
- **Refinement Prefixes** — [Prefixes/](Prefixes/) has per-damage-class prefix variants (Melee/Ranged/Summon/Magic, with and without knockback).
- **Calamity Compat** — [Common/Systems/CalamityCompatSystem.cs](Common/Systems/CalamityCompatSystem.cs) handles optional Calamity mod integration.

### Assets

Shader `.fx` files live in [Assets/AutoloadedEffects/](Assets/AutoloadedEffects/) and are processed by Luminance. The `.csproj` explicitly preserves `TimeStopFilter.fx` from tModLoader's default removal behavior.

## Conventions

- **Partial classes for large global hooks** — `TestGlobalProjectile` and potentially `TestGlobalNPC`/`TestGlobalItem` use partial classes to split logic by weapon or feature domain.
- **Namespace matches folder** — `TestMod.Common.Players`, `TestMod.Items.Accessories.Effects`, etc.
- **Localization** — All display strings go in [Localization/en-US_Mods.TestMod.hjson](Localization/en-US_Mods.TestMod.hjson).
