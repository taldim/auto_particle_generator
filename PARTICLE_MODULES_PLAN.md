# Plan: Add All Missing Particle System Modules

## Overview

Add all missing Unity particle system modules to the auto-particle-system framework.
Each task is a vertical slice: schema class + applier logic + prompt update for its modules.

**Files modified by every task:**
- `auto-particle-system/ParticleConfigSchema.cs` — add config class(es), add field(s) to `ParticleConfig`
- `auto-particle-system/ParticleSystemApplier.cs` — add apply logic after existing modules
- `auto-particle-system/VFXPrompts.cs` — add schema docs + design guidelines to `PARTICLE_SYSTEM` prompt

**Execution:** Tasks 1-5 are sequential (each edits the same 3 files). Task 6 runs after all others.

---

## Task 1: Simple Force & Velocity Modules

**Modules:** Force over Lifetime, Limit Velocity over Lifetime, Inherit Velocity, Lifetime by Emitter Speed

**Complexity:** Low — all follow the same pattern of curves + floats + space enum.

### Schema (`ParticleConfigSchema.cs`)

Add to `ParticleConfig` class:
```csharp
public ForceOverLifetimeConfig forceOverLifetime;
public LimitVelocityOverLifetimeConfig limitVelocityOverLifetime;
public InheritVelocityConfig inheritVelocity;
public LifetimeByEmitterSpeedConfig lifetimeByEmitterSpeed;
```

New classes:

```csharp
// Force over Lifetime — constant forces applied to particles
public class ForceOverLifetimeConfig
{
    public CurvePoint[] x;               // force on X axis
    public CurvePoint[] y;               // force on Y axis
    public CurvePoint[] z;               // force on Z axis
    public string space = "Local";       // "Local" or "World"
    public bool randomized;              // randomize force direction per frame
}

// Limit Velocity over Lifetime — cap/dampen particle speed
public class LimitVelocityOverLifetimeConfig
{
    public float speed = 1f;             // max speed
    public CurvePoint[] speedCurve;      // OR varying speed limit over lifetime
    public float dampen = 0.1f;          // how aggressively speed is reduced (0-1)
    public float drag;                   // drag coefficient
    public bool multiplyDragBySize;
    public bool multiplyDragByVelocity;
    public string space = "Local";
}

// Inherit Velocity — particles inherit emitter's velocity
public class InheritVelocityConfig
{
    public string mode = "Initial";      // "Initial" or "Current"
    public float multiplier = 1f;        // velocity inheritance multiplier
}

// Lifetime by Emitter Speed — particle lifetime scales with emitter speed
public class LifetimeByEmitterSpeedConfig
{
    public CurvePoint[] curve;           // speed-to-lifetime multiplier curve
    public float speedRangeMin;          // min emitter speed
    public float speedRangeMax = 1f;     // max emitter speed
}
```

### Applier (`ParticleSystemApplier.cs`)

Add after the existing Velocity over Lifetime block. Reference Unity API:
- `ps.forceOverLifetime` — `.x`, `.y`, `.z` (MinMaxCurve), `.space`, `.randomized`
- `ps.limitVelocityOverLifetime` — `.limit` (MinMaxCurve), `.dampen`, `.drag`, `.multiplyDragBySize`, `.multiplyDragByParticleVelocity`, `.space`
- `ps.inheritVelocity` — `.mode` (Initial/Current), `.curve` (MinMaxCurve)
- `ps.lifetimeByEmitterSpeed` — `.curve` (MinMaxCurve), `.range` (Vector2)

### Prompt (`VFXPrompts.cs`)

Add to schema section (after velocityOverLifetime):
```
"forceOverLifetime": { ... }
"limitVelocityOverLifetime": { ... }
"inheritVelocity": { ... }
"lifetimeByEmitterSpeed": { ... }
```

Add to design guidelines:
```
FORCE OVER LIFETIME:
- Wind: constant X force in World space (1-3)
- Updraft: Y force curve increasing over lifetime
- Gravity wells: combine X+Y forces pointing inward
- Randomized mode for chaotic effects like fire embers

LIMIT VELOCITY:
- Explosions: high initial speed + dampen 0.5 to slow down naturally
- Rain: limit speed to terminal velocity
- Drag: smoke/dust with drag 0.5-2.0 for air resistance

INHERIT VELOCITY:
- Projectile trails: "Initial" mode, multiplier 0.5-1.0 so particles follow projectile
- Moving character auras: "Current" mode, multiplier 0.3-0.5
```

---

## Task 2: Speed-Reactive Modules

**Modules:** Color by Speed, Size by Speed, Rotation by Speed

**Complexity:** Low — all share the same pattern: a curve/gradient + speed range.

### Schema (`ParticleConfigSchema.cs`)

Add to `ParticleConfig`:
```csharp
public ColorBySpeedConfig colorBySpeed;
public SizeBySpeedConfig sizeBySpeed;
public RotationBySpeedConfig rotationBySpeed;
```

New classes:

```csharp
// Color by Speed — color changes based on particle velocity
public class ColorBySpeedConfig
{
    public GradientStop[] gradient;      // color mapped to speed range
    public float speedRangeMin;
    public float speedRangeMax = 1f;
}

// Size by Speed — size changes based on particle velocity
public class SizeBySpeedConfig
{
    public CurvePoint[] curve;           // size multiplier mapped to speed range
    public float speedRangeMin;
    public float speedRangeMax = 1f;
}

// Rotation by Speed — spin rate based on particle velocity
public class RotationBySpeedConfig
{
    public float angularVelocity;        // constant degrees/sec at max speed
    public CurvePoint[] curve;           // OR varying curve (degrees/sec)
    public float speedRangeMin;
    public float speedRangeMax = 1f;
}
```

### Applier (`ParticleSystemApplier.cs`)

Unity API:
- `ps.colorBySpeed` — `.color` (MinMaxGradient), `.range` (Vector2)
- `ps.sizeBySpeed` — `.size` (MinMaxCurve), `.range` (Vector2)
- `ps.rotationBySpeed` — `.z` (MinMaxCurve, radians), `.range` (Vector2)

### Prompt (`VFXPrompts.cs`)

Add design guidelines:
```
COLOR BY SPEED:
- Explosions: fast=white-hot, slow=red/orange — automatic heat-like coloring
- Debris: fast=bright, slow=dark — natural deceleration look

SIZE BY SPEED:
- Sparks: larger when fast, shrink as they slow
- Rain: elongate fast drops (combine with StretchedBillboard)

ROTATION BY SPEED:
- Debris/shrapnel: faster movement = faster tumble (90-360 deg/s)
- Leaves: gentle spin when slow (10-30), wild spin when fast (180+)
```

---

## Task 3: Texture Sheet Animation

**Modules:** Texture Sheet Animation

**Complexity:** Medium — needs tile config, frame curves, row modes.

### Schema (`ParticleConfigSchema.cs`)

Add to `ParticleConfig`:
```csharp
public TextureSheetAnimationConfig textureSheetAnimation;
```

New class:

```csharp
// Texture Sheet Animation — flipbook sprite sheet playback
public class TextureSheetAnimationConfig
{
    public int tilesX = 1;               // columns in sprite sheet
    public int tilesY = 1;               // rows in sprite sheet
    public string animationMode = "WholeSheet"; // "WholeSheet" or "SingleRow"
    public CurvePoint[] frameOverTime;   // which frame to show over lifetime (0-1 normalized)
    public float startFrame;             // starting frame (0-based)
    public int cycles = 1;              // how many times to loop the animation
    public string rowMode = "Random";    // "Custom" or "Random" (for SingleRow mode)
    public int rowIndex;                 // which row to use (for Custom rowMode)
    public float fps;                    // if > 0, use constant FPS instead of frameOverTime curve
}
```

### Applier (`ParticleSystemApplier.cs`)

Unity API:
- `ps.textureSheetAnimation` — `.numTilesX`, `.numTilesY`, `.animation` (WholeSheet/SingleRow), `.frameOverTime` (MinMaxCurve), `.startFrame` (MinMaxCurve), `.cycleCount`, `.rowMode` (Custom/Random), `.rowIndex`
- Note: `frameOverTime` range is 0-1 (normalized to total frames). If `fps` is set, calculate a linear curve: `frameOverTime = new MinMaxCurve(1f / (tilesX * tilesY / fps / startLifetime))`

### Prompt (`VFXPrompts.cs`)

Add design guidelines:
```
TEXTURE SHEET ANIMATION:
- Fire: 4x4 or 8x8 flipbook, WholeSheet, cycles 1, frameOverTime linear 0→1
- Smoke: 4x4, WholeSheet, frameOverTime linear, slower lifetime for slower playback
- Explosion: 4x4 or 8x8, WholeSheet, cycles 1, short lifetime (0.3-0.8)
- Stylized effects: 2x2, SingleRow for directional variants
- Always use with appropriate sprite sheet texture assigned to material _MainTex
- Common FPS: fire 15-24, smoke 8-12, explosion 24-30
- NOTE: sprite sheet texture must be provided by the user — configure tiling only
```

---

## Task 4: Lights & Collision

**Modules:** Lights, Collision

**Complexity:** Medium — Lights needs a Light component; Collision has many settings.

### Schema (`ParticleConfigSchema.cs`)

Add to `ParticleConfig`:
```csharp
public ParticleLightsConfig lights;
public CollisionConfig collision;
```

New classes:

```csharp
// Lights — each particle emits a 2D light
public class ParticleLightsConfig
{
    public float ratio = 1f;             // fraction of particles that get lights (0-1)
    public bool useParticleColor = true;
    public bool sizeAffectsRange = true;
    public bool alphaAffectsIntensity = true;
    public float rangeMultiplier = 1f;
    public float intensityMultiplier = 1f;
    public int maxLights = 20;
}

// Collision — particles collide with the world
public class CollisionConfig
{
    public string type = "World";        // "World" or "Planes"
    public string mode = "2D";           // "2D" or "3D"
    public float bounce = 0.5f;          // bounciness (0-1)
    public float lifetimeLoss;           // lifetime lost on collision (0-1)
    public float dampen = 0.5f;          // speed lost on collision (0-1)
    public float radiusScale = 0.5f;     // collision radius relative to particle size
    public float minKillSpeed;           // kill particles slower than this after collision
    public bool sendCollisionMessages;   // enable OnParticleCollision callbacks
}
```

### Applier (`ParticleSystemApplier.cs`)

Unity API for Lights:
- `ps.lights` — `.enabled`, `.ratio`, `.useParticleColor`, `.sizeAffectsRange`, `.alphaAffectsIntensity`, `.rangeMultiplier`, `.intensityMultiplier`, `.maxLights`
- `.light` requires a reference Light component. The applier must:
  1. Create a child GameObject with a `Light2D` (or `Light` if not URP 2D)
  2. Assign it to `ps.lights.light`
  3. Disable the GameObject (Unity clones it per particle)
- Use `UnityEngine.Rendering.Universal.Light2D` if available, fall back to `Light`

Unity API for Collision:
- `ps.collision` — `.enabled`, `.type` (World/Planes), `.mode` (2D/3D), `.bounce`, `.lifetimeLoss`, `.dampen`, `.radiusScale`, `.minKillSpeed`, `.sendCollisionMessages`
- For 2D: set `.mode = ParticleSystemCollisionMode.Collision2D`

### Prompt (`VFXPrompts.cs`)

Add design guidelines:
```
LIGHTS:
- Fire/explosions: ratio 0.3-0.5, useParticleColor true, rangeMultiplier 2-4, intensityMultiplier 1-2
- Magic sparkles: ratio 0.1-0.2, rangeMultiplier 1-2, lower intensity
- Caution: maxLights caps performance — 10-20 for most effects, 5 for heavy scenes
- Works best with HDR colors — light intensity comes from particle color brightness

COLLISION:
- Sparks hitting ground: bounce 0.3-0.6, dampen 0.5, mode "2D"
- Rain splashes: bounce 0, lifetimeLoss 1.0 (die on impact), sendCollisionMessages for splash sub-emitter
- Debris: bounce 0.2-0.4, dampen 0.3, radiusScale 0.5
- Blood drops: bounce 0.1, lifetimeLoss 0.8, low minKillSpeed
```

---

## Task 5: Sub Emitters

**Modules:** Sub Emitters

**Complexity:** High — recursive structure, creates child particle systems.

### Schema (`ParticleConfigSchema.cs`)

Add to `ParticleConfig`:
```csharp
public SubEmitterEntry[] subEmitters;
```

New classes:

```csharp
// Sub Emitters — spawn child particle systems on events
public class SubEmitterEntry
{
    public string trigger = "Birth";     // "Birth", "Collision", "Death", "FirstCollision", "Manual"
    public ParticleConfig config;        // full nested particle config for the sub-emitter
    public string inheritProperties = "Nothing"; // "Nothing", "Color", "Size", "Rotation", "Lifetime", "Everything"
    public float emitProbability = 1f;   // chance to emit (0-1)
}
```

### Applier (`ParticleSystemApplier.cs`)

This is the most complex module. The applier must:
1. For each `SubEmitterEntry`, recursively call `Apply()` to create a child GameObject + ParticleSystem
2. Parent it under the main particle system's transform
3. Disable `emission` on the child (it's triggered by the parent, not self-emitting)
4. Set `main.playOnAwake = false` on the child
5. Wire it up via `ps.subEmitters.AddSubEmitter(childPS, trigger, inheritProperties)`
6. Guard against infinite recursion — cap nesting depth (e.g., max 2 levels)

Unity API:
- `ps.subEmitters` — `.enabled`, `.AddSubEmitter(ParticleSystem, SubEmitterType, SubEmitterProperties)`
- `SubEmitterType`: Birth, Collision, Death, FirstCollision, Manual
- `SubEmitterProperties`: InheritNothing, InheritColor, InheritSize, InheritRotation, InheritLifetime, InheritEverything

Key implementation note: `Apply()` currently returns a `GameObject`. It needs a small refactor — add an optional `int depth = 0` parameter and skip sub-emitter processing when `depth >= 2`.

### Prompt (`VFXPrompts.cs`)

Add design guidelines:
```
SUB EMITTERS — spawn child particle systems on particle events:
- Explosion chain: main burst → Death sub-emitter with smoke (looping=false, short burst)
- Fireworks: main launch particle → Death sub-emitter with colorful burst
- Sparks + smoke: spark particles → Collision sub-emitter with small dust puff
- Fire with embers: main fire → Birth sub-emitter with rising ember particles
- Keep sub-emitter configs simple — avoid deep nesting (max 1 level of sub-emitters)
- Sub-emitters should generally be short-lived bursts, not looping effects
- Use inheritProperties "Color" to match parent particle's tint
- Use emitProbability < 1.0 for sparse effects (not every particle triggers)

SUB EMITTER RECIPES:
- Explosion: main cone burst (hot→transparent) → Death: sphere burst 5-10 smoke particles (gray, slow, size grows)
- Firework: single particle up with gravity → Death: sphere burst 20-40 (bright HDR colors, trails, gravity 0.5)
- Impact sparks: cone burst fast → Collision: 2-3 small bounce particles (dampen 0.5, short life)
```

---

## Task 6: Prompt Tuning & Integration Test

**Depends on:** Tasks 1-5 all complete.

**Complexity:** Low — no new code logic, just prompt refinement.

### Work

1. **Review the full `VFXPrompts.PARTICLE_SYSTEM` prompt** — ensure all new modules are documented in the schema section with correct field names, types, and comments.

2. **Update EFFECT RECIPES** — revise the existing recipes at the bottom of the prompt to use the new modules where appropriate:
   - Fire recipe: add forceOverLifetime (updraft), sub-emitter (embers on death)
   - Explosion recipe: add limitVelocityOverLifetime (dampen), collision (bounce), sub-emitter (smoke on death), textureSheetAnimation if sprite sheet
   - Slash impact: add colorBySpeed (fast=bright), lights
   - Electric: add rotationBySpeed
   - Rain (new recipe): collision + bounce 0, lifetimeLoss 1, sub-emitter splash on collision

3. **Add a MODULE COMBINATIONS section** to the prompt — teach the AI which modules pair well:
   - Force + Limit Velocity: realistic physics feel
   - Collision + Sub Emitters: impact spawns (splash, dust, sparks)
   - Texture Sheet Animation + Size over Lifetime: animated sprites that also scale
   - Lights + HDR colors: actual per-particle illumination
   - Color by Speed + Velocity over Lifetime: speed-reactive visual feedback

4. **Token budget check** — the prompt is getting large. Ensure it stays within reasonable size. If too long, move detailed recipes to a separate constant and reference conditionally.

---

## Summary

| Task | Modules | Complexity | Dependencies |
|------|---------|------------|--------------|
| 1 | Force, Limit Velocity, Inherit Velocity, Lifetime by Emitter Speed | Low | None |
| 2 | Color by Speed, Size by Speed, Rotation by Speed | Low | Task 1 (same files) |
| 3 | Texture Sheet Animation | Medium | Task 2 |
| 4 | Lights, Collision | Medium | Task 3 |
| 5 | Sub Emitters | High | Task 4 |
| 6 | Prompt Tuning & Integration | Low | Tasks 1-5 |

**Skipped modules (not practical for AI generation):**
- Triggers — requires pre-existing scene colliders and callbacks
- External Forces — requires Wind Zone GameObjects (3D concept)
- Custom Data — scripting bridge, not relevant to generated configs
