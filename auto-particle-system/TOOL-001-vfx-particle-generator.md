# TOOL-001: VFX Generation Toolset

## Metadata
| Field | Value |
|-------|-------|
| **Phase** | Tools |
| **Type** | tooling |
| **Priority** | P1 |
| **Owner** | Dev 1 |
| **Depends On** | None |
| **Blocks** | None |
| **Status** | DONE |

## Objective
A suite of Unity Editor tools that generate VFX particle systems and shaders from natural language prompts. Supports both API-powered (auto) and manual copy-paste workflows. Includes standalone particle generation, standalone shader generation, and a combined VFX Composer that creates matched shader+particle prefabs. All tools save prefabs to `Assets/Prefabs/VFX/` and shaders to `Assets/Shaders/Generated/`.

## Context
Tomato Fighters currently has zero particle system infrastructure. The game needs dozens of VFX effects across combat, roguelite, and world pillars. Manual ParticleSystem + shader tuning is tedious. This toolset accelerates VFX iteration by letting developers describe effects in plain English and get ready-to-use prefabs in seconds.

## Architecture

### Tool Suite Overview

| Tool | Menu Path | Mode | Purpose |
|------|-----------|------|---------|
| VFX Generator | `TomatoFighters/VFX Generator` | API | Particle-only generation with Generate + Refine |
| VFX Generator (Manual) | `TomatoFighters/VFX Generator (Manual)` | Copy-paste | Particle-only, 3-step prompt workflow |
| Shader Generator | `TomatoFighters/Shader Generator` | API | Shader-only generation with Generate + Refine |
| Shader Generator (Manual) | `TomatoFighters/Shader Generator (Manual)` | Copy-paste | Shader-only, 3-step prompt workflow |
| VFX Composer | `TomatoFighters/VFX Composer` | API | Full pipeline: shader selection/creation + particle config + prefab assembly |
| VFX Composer (Manual) | `TomatoFighters/VFX Composer (Manual)` | Copy-paste | 5-step wizard: describe → shader prompt → paste shader → particle prompt → paste particles |

### Data Flow (VFX Composer — full pipeline)
```
VFXComposer (EditorWindow)
  │
  ├── [Compose] button
  │     │
  │     ├── Step 1: Shader
  │     │     → If allowReuseShader: AI picks existing or creates new
  │     │     → If !allowReuseShader: AI always creates new shader
  │     │     → ShaderUtils.SaveShaderAndCreateMaterial() → Material
  │     │
  │     ├── Step 2: Particle System
  │     │     → ClaudeApiClient.SendRawPrompt(PARTICLE_SYSTEM prompt, description + shader name)
  │     │     → JSON → ParticleConfig → ParticleSystemApplier.Apply()
  │     │
  │     └── Step 3: Assembly
  │           → Override renderer material with composed shader material
  │           → PrefabUtility.SaveAsPrefabAssetAndConnect()
  │           → Play particle system in scene
  │
  └── [Refine] button
        → Same 3-step pipeline with previous shader code + particle JSON + refinement prompt
```

### File Plan

| # | File | Purpose |
|---|------|---------|
| 1 | `Assets/Editor/VFX/ParticleConfigSchema.cs` | 10-module JSON contract between LLM output and Unity ParticleSystem |
| 2 | `Assets/Editor/VFX/ClaudeApiClient.cs` | Multi-platform API client (Anthropic/OpenRouter/OpenAI) with .env key storage |
| 3 | `Assets/Editor/VFX/ParticleSystemApplier.cs` | Maps `ParticleConfig` onto Unity ParticleSystem modules |
| 4 | `Assets/Editor/VFX/ParticleSystemGenerator.cs` | EditorWindow — API-powered particle generation |
| 5 | `Assets/Editor/VFX/ParticleSystemManualGenerator.cs` | EditorWindow — manual copy-paste particle generation |
| 6 | `Assets/Editor/VFX/ShaderGenerator.cs` | EditorWindow — API-powered URP shader generation |
| 7 | `Assets/Editor/VFX/ShaderManualGenerator.cs` | EditorWindow — manual copy-paste shader generation |
| 8 | `Assets/Editor/VFX/VFXComposer.cs` | EditorWindow — API-powered combined shader + particle pipeline |
| 9 | `Assets/Editor/VFX/VFXComposerManual.cs` | EditorWindow — 5-step manual shader + particle wizard |
| 10 | `Assets/Editor/VFX/ShaderUtils.cs` | Shared shader utilities: save, load, fallback template, validation |
| 11 | `Assets/Editor/VFX/VFXPrompts.cs` | Single source of truth for all LLM system prompts |

All files live in `Assets/Editor/` — they are **Editor-only** and excluded from runtime builds.

## Requirements

### 1. ParticleConfigSchema.cs — JSON Contract (10 Modules)

The schema defines what the LLM returns. Uses simple arrays (`GradientStop[]`, `CurvePoint[]`) instead of Unity's complex types for reliable LLM output.

**Modules:**

| Module | Key Fields |
|--------|------------|
| `MainModuleConfig` | duration, looping, startLifetime, startSpeed, startSize, startRotation, startColor, gravityModifier, simulationSpace, maxParticles |
| `EmissionConfig` | rateOverTime, bursts[] (time, count) |
| `ShapeConfig` | shapeType (Cone/Sphere/Circle/Edge/Box), angle, radius, arc, randomDirection |
| `ColorOverLifetimeConfig` | gradient[] (time, color RGBA) |
| `SizeOverLifetimeConfig` | curve[] (time, value) |
| `VelocityOverLifetimeConfig` | x/y/z curves[], speedModifier, space (Local/World) |
| `NoiseConfig` | strength, frequency, octaves, scrollSpeed, damping |
| `RotationOverLifetimeConfig` | angularVelocity (degrees/sec), curve[] |
| `TrailsConfig` | lifetime, widthMultiplier, minimumVertexDistance, colorGradient[], widthCurve[], dieWithParticle |
| `RendererConfig` | renderMode (Billboard/Stretch/Mesh), lengthScale, speedScale, sortMode, normalDirection |

### 2. ClaudeApiClient.cs — Multi-Platform API Client

**Platform auto-detection from key prefix:**
| Prefix | Platform | Endpoint | Model |
|--------|----------|----------|-------|
| `sk-ant-` | Anthropic | `api.anthropic.com/v1/messages` | `claude-sonnet-4-20250514` |
| `sk-or-` | OpenRouter | `openrouter.ai/api/v1/chat/completions` | `anthropic/claude-sonnet-4.6` |
| Other | OpenAI | `api.openai.com/v1/chat/completions` | `gpt-4o` |

**API key storage:** `.env` file at Unity project root (`{Application.dataPath}/../.env`). Reads any key ending in `_API_KEY`. Handles UTF-16/BOM encoding from Windows Notepad.

**Key methods:**
- `GetApiKey()` / `SetApiKey()` — .env file I/O
- `DetectPlatform(string apiKey)` — prefix-based detection
- `SendPrompt(userPrompt)` — particle-specific, returns `ParticleConfig`
- `SendRefinement(previousJson, refinementPrompt)` — particle refinement
- `SendRawPrompt(systemPrompt, userPrompt)` — generic, returns raw text (used by shader tools)
- `StripMarkdownFencing(text)` — removes ```json fencing from responses

**MAX_TOKENS:** 4096

### 3. ParticleSystemApplier.cs — Config to ParticleSystem

Maps all 10 `ParticleConfig` modules to Unity `ParticleSystem` components with null checks on each module.

**Key implementation details:**
- `emission.burstCount` must be set before `SetBurst()` calls
- Noise: `separateAxes = true` with Z strength reduced to 50% for 2D
- Rotation: converts degrees to radians via `Mathf.Deg2Rad`
- Trails: assigns `trailMaterial` on the renderer
- `BuildCurve()` accepts optional `scale` parameter for degree-to-radian conversion
- Helper parsers: `ParseRenderMode()`, `ParseSortMode()`, `ParseShapeType()`, `ParseSimulationSpace()`

### 4. ShaderUtils.cs — Shared Shader Utilities

- `SaveShaderAndCreateMaterial(string code, string name)` — saves `.shader` file, creates matching `_Mat.mat`. Accepts `null` code to use fallback template.
- `LoadOrCreateShader(name)` — tries `AssetDatabase.LoadAssetAtPath` → `Shader.Find` → writes fallback shader
- `ListValidShaders()` — uses `AssetDatabase.FindAssets("t:Shader")` + `ShaderUtil.ShaderHasError()` to list only working shaders with their properties. Ensures deleted/broken shaders are never suggested.
- `FALLBACK_SHADER_TEMPLATE` — basic URP alpha-blend particle shader used when AI-generated shader fails

### 5. VFXPrompts.cs — System Prompts (Single Source of Truth)

Three prompt constants used by all tools:

| Constant | Used By | Content |
|----------|---------|---------|
| `PARTICLE_SYSTEM` | ParticleSystemGenerator, Manual, VFXComposer | Full 10-module JSON schema + VFX design guidelines with effect recipes (fire, smoke, explosion, magic aura, slash, electric, heal, blood, dust) |
| `SHADER_NEW` | ShaderGenerator, ShaderManualGenerator | URP shader generation rules with compile-error prevention (NEVER CGPROGRAM, use HLSLPROGRAM) |
| `SHADER_PICK_OR_CREATE` | VFXComposer (when allowReuseShader=true) | Same as SHADER_NEW but with `USE_EXISTING: ShaderName` option |

### 6. Editor Windows

**API-powered windows** (ParticleSystemGenerator, ShaderGenerator, VFXComposer):
- Generate + Refine buttons with `async void` API calls
- `isProcessing` flag disables buttons during API calls
- API Settings foldout with masked key input + Save button + platform detection display
- Status log with timestamps

**Manual windows** (ParticleSystemManualGenerator, ShaderManualGenerator, VFXComposerManual):
- Step-by-step workflow: describe → copy generated prompt → paste LLM response → apply
- No API key required — user copies prompt to any LLM chat
- Copy to Clipboard button
- VFXComposerManual has a 5-step wizard with progress bar and Start Over button

**VFX Composer features:**
- `allowReuseShader` toggle: when enabled, AI can respond with `USE_EXISTING: ShaderName` to reuse an existing shader instead of creating new
- When reuse disabled, always uses `SHADER_NEW` prompt (no existing shader list)
- `ProcessShaderResponse()` strips `TomatoFighters/` prefix from shader names, falls back to template on lookup failure

## Design Decisions

### DD-1: LLM-Powered Generation
**Decision:** Use LLM API to generate particle configs and shaders from natural language.
**Rationale:** Maximum expressiveness without predefined keyword vocabulary. API dependency acceptable for Editor-only tools.

### DD-2: System.Net.HttpClient
**Decision:** Use `System.Net.Http.HttpClient` (static, reused) for API calls.
**Rationale:** Editor-only context. Cleaner async code than `UnityWebRequest` + `EditorCoroutineUtility`.

### DD-3: 10-Module Schema
**Decision:** Support Main, Emission, Shape, ColorOverLifetime, SizeOverLifetime, VelocityOverLifetime, Noise, RotationOverLifetime, Trails, and Renderer modules.
**Rationale:** Covers ~95% of common particle effects. All modules are optional (null-checked) so the LLM only returns what's needed.

### DD-4: Simple Array Schema
**Decision:** JSON uses `GradientStop[]` and `CurvePoint[]` instead of Unity's `MinMaxGradient`/`MinMaxCurve`.
**Rationale:** LLMs generate simple arrays reliably. The Applier handles conversion to Unity types.

### DD-5: Prefab + Scene Instance Output
**Decision:** Tools save a prefab AND instantiate a preview in the active scene.
**Rationale:** Fastest iteration — see results immediately without drag-and-drop.

### DD-6: Refine Workflow
**Decision:** Refine sends previous config + new instruction, returns complete updated config.
**Rationale:** Full context for the LLM. Complete response simplifies the applier (overwrite everything).

### DD-7: .env File Key Storage
**Decision:** Store API key in `.env` file at project root, not EditorPrefs.
**Rationale:** Editable with any text editor. `.env` added to `.gitignore`. Reads any key ending in `_API_KEY` for flexibility.

### DD-8: Multi-Platform API Support
**Decision:** Auto-detect platform (Anthropic/OpenRouter/OpenAI) from API key prefix.
**Rationale:** Team members may have different API providers. Zero configuration — just paste your key.

### DD-9: Dual Workflow (API + Manual)
**Decision:** Every tool has an API-powered and a manual copy-paste variant.
**Rationale:** Manual mode works without API key setup, lets users use any LLM (Claude web, ChatGPT, etc.), and is useful when API is unavailable.

### DD-10: Combined VFX Composer
**Decision:** A dedicated tool that creates matched shader + particle system pairs.
**Rationale:** Shader and particle system are interdependent — the AI can make better particle configs when it knows which shader is being used, and vice versa.

### DD-11: Shader Validation
**Decision:** `ListValidShaders()` checks `ShaderUtil.ShaderHasError()` before listing existing shaders.
**Rationale:** Prevents the AI from referencing deleted or broken shaders. Only working shaders appear in the "reuse" list.

### DD-12: URP Shader Rules
**Decision:** System prompts explicitly forbid `CGPROGRAM/ENDCG` and require `HLSLPROGRAM/ENDHLSL`.
**Rationale:** Project uses URP. Legacy built-in pipeline shader syntax causes compile errors in URP.

## Future Extensions
- Preset library: save/load named configs as ScriptableObjects
- Batch generation: generate multiple variants from one prompt
- Custom particle textures: let the prompt specify texture
- SubEmitters module support
- TextureSheetAnimation module support
- Integration with Animation Events: auto-wire VFX to attack hitbox timing
