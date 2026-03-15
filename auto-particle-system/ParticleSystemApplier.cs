using UnityEditor;
using UnityEngine;

namespace TomatoFighters.Editor.VFX
{
    public static class ParticleSystemApplier
    {
        private const int MAX_SUB_EMITTER_DEPTH = 2;

        public static GameObject Apply(ParticleConfig config, string name, int depth = 0)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            // Main module
            var main = ps.main;
            main.duration = config.main.duration;
            main.loop = config.main.looping;
            main.startLifetime = config.main.startLifetime;
            main.startSpeed = config.main.startSpeed;
            main.startSize = config.main.startSize;
            main.startColor = ToColor(config.main.startColor);
            main.startRotation = config.main.startRotation * Mathf.Deg2Rad;
            main.gravityModifier = config.main.gravityModifier;
            main.simulationSpace = ParseSimulationSpace(config.main.simulationSpace);
            main.maxParticles = config.main.maxParticles;

            // Emission module
            var emission = ps.emission;
            emission.rateOverTime = config.emission.rateOverTime;
            if (config.emission.bursts != null && config.emission.bursts.Length > 0)
            {
                emission.burstCount = config.emission.bursts.Length;
                for (int i = 0; i < config.emission.bursts.Length; i++)
                {
                    var burst = config.emission.bursts[i];
                    emission.SetBurst(i, new ParticleSystem.Burst(burst.time, burst.count));
                }
            }

            // Shape module
            var shape = ps.shape;
            shape.shapeType = ParseShapeType(config.shape.shapeType);
            shape.angle = config.shape.angle;
            shape.radius = config.shape.radius;
            shape.arc = config.shape.arc;
            shape.randomDirectionAmount = config.shape.randomDirection ? 1f : 0f;

            // Color over Lifetime
            if (config.colorOverLifetime?.gradient != null && config.colorOverLifetime.gradient.Length > 0)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = BuildGradient(config.colorOverLifetime.gradient);
            }

            // Size over Lifetime
            if (config.sizeOverLifetime?.curve != null && config.sizeOverLifetime.curve.Length > 0)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = BuildCurve(config.sizeOverLifetime.curve);
            }

            // Velocity over Lifetime
            if (config.velocityOverLifetime != null &&
                (HasPoints(config.velocityOverLifetime.x) ||
                 HasPoints(config.velocityOverLifetime.y) ||
                 HasPoints(config.velocityOverLifetime.z)))
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParseSimulationSpace(config.velocityOverLifetime.space);

                if (HasPoints(config.velocityOverLifetime.x))
                    vel.x = BuildCurve(config.velocityOverLifetime.x);
                if (HasPoints(config.velocityOverLifetime.y))
                    vel.y = BuildCurve(config.velocityOverLifetime.y);
                if (HasPoints(config.velocityOverLifetime.z))
                    vel.z = BuildCurve(config.velocityOverLifetime.z);

                vel.speedModifier = config.velocityOverLifetime.speedModifier;
            }

            // Noise module
            if (config.noise != null && config.noise.strength > 0)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = config.noise.strength;
                noise.frequency = config.noise.frequency;
                noise.octaveCount = Mathf.Clamp(config.noise.octaves, 1, 4);
                noise.scrollSpeed = config.noise.scrollSpeed;
                noise.damping = config.noise.damping > 0;
                noise.separateAxes = true;
                noise.strengthX = config.noise.strength;
                noise.strengthY = config.noise.strength;
                noise.strengthZ = config.noise.strength * 0.5f;
            }

            // Rotation over Lifetime
            if (config.rotationOverLifetime != null)
            {
                bool hasCurve = config.rotationOverLifetime.curve != null &&
                                config.rotationOverLifetime.curve.Length > 0;
                bool hasConstant = config.rotationOverLifetime.angularVelocity != 0;

                if (hasCurve || hasConstant)
                {
                    var rot = ps.rotationOverLifetime;
                    rot.enabled = true;
                    if (hasCurve)
                        rot.z = BuildCurve(config.rotationOverLifetime.curve, Mathf.Deg2Rad);
                    else
                        rot.z = config.rotationOverLifetime.angularVelocity * Mathf.Deg2Rad;
                }
            }

            // Trails
            if (config.trails != null && config.trails.lifetime > 0)
            {
                var trails = ps.trails;
                trails.enabled = true;
                trails.lifetime = config.trails.lifetime;
                trails.widthOverTrail = config.trails.widthMultiplier;
                trails.minVertexDistance = config.trails.minimumVertexDistance;
                trails.dieWithParticles = config.trails.dieWithParticle;

                if (config.trails.colorGradient != null && config.trails.colorGradient.Length > 0)
                    trails.colorOverLifetime = BuildGradient(config.trails.colorGradient);

                if (config.trails.widthCurve != null && config.trails.widthCurve.Length > 0)
                    trails.widthOverTrail = BuildCurve(config.trails.widthCurve);
            }

            // --- Task A1: Force over Lifetime ---
            if (config.forceOverLifetime != null &&
                (HasPoints(config.forceOverLifetime.x) ||
                 HasPoints(config.forceOverLifetime.y) ||
                 HasPoints(config.forceOverLifetime.z)))
            {
                var force = ps.forceOverLifetime;
                force.enabled = true;
                force.space = ParseSimulationSpace(config.forceOverLifetime.space);
                force.randomized = config.forceOverLifetime.randomized;

                if (HasPoints(config.forceOverLifetime.x))
                    force.x = BuildCurve(config.forceOverLifetime.x);
                if (HasPoints(config.forceOverLifetime.y))
                    force.y = BuildCurve(config.forceOverLifetime.y);
                if (HasPoints(config.forceOverLifetime.z))
                    force.z = BuildCurve(config.forceOverLifetime.z);
            }

            // --- Task A1: Limit Velocity over Lifetime ---
            if (config.limitVelocityOverLifetime != null)
            {
                bool hasCurve = HasPoints(config.limitVelocityOverLifetime.speedCurve);
                bool hasSpeed = config.limitVelocityOverLifetime.speed > 0;
                bool hasDrag = config.limitVelocityOverLifetime.drag > 0;

                if (hasCurve || hasSpeed || hasDrag)
                {
                    var lv = ps.limitVelocityOverLifetime;
                    lv.enabled = true;
                    lv.space = ParseSimulationSpace(config.limitVelocityOverLifetime.space);
                    lv.dampen = config.limitVelocityOverLifetime.dampen;

                    if (hasCurve)
                        lv.limit = BuildCurve(config.limitVelocityOverLifetime.speedCurve);
                    else
                        lv.limit = config.limitVelocityOverLifetime.speed;

                    lv.drag = config.limitVelocityOverLifetime.drag;
                    lv.multiplyDragByParticleSize = config.limitVelocityOverLifetime.multiplyDragBySize;
                    lv.multiplyDragByParticleVelocity = config.limitVelocityOverLifetime.multiplyDragByVelocity;
                }
            }

            // --- Task A1: Inherit Velocity ---
            if (config.inheritVelocity != null && config.inheritVelocity.multiplier != 0)
            {
                var iv = ps.inheritVelocity;
                iv.enabled = true;
                iv.mode = ParseInheritVelocityMode(config.inheritVelocity.mode);
                iv.curve = config.inheritVelocity.multiplier;
            }

            // --- Task A1: Lifetime by Emitter Speed ---
            if (config.lifetimeByEmitterSpeed != null && HasPoints(config.lifetimeByEmitterSpeed.curve))
            {
                var lbes = ps.lifetimeByEmitterSpeed;
                lbes.enabled = true;
                lbes.curve = BuildCurve(config.lifetimeByEmitterSpeed.curve);
                lbes.range = new Vector2(
                    config.lifetimeByEmitterSpeed.speedRangeMin,
                    config.lifetimeByEmitterSpeed.speedRangeMax);
            }

            // --- Task A2: Color by Speed ---
            if (config.colorBySpeed?.gradient != null && config.colorBySpeed.gradient.Length > 0)
            {
                var cbs = ps.colorBySpeed;
                cbs.enabled = true;
                cbs.color = BuildGradient(config.colorBySpeed.gradient);
                cbs.range = new Vector2(
                    config.colorBySpeed.speedRangeMin,
                    config.colorBySpeed.speedRangeMax);
            }

            // --- Task A2: Size by Speed ---
            if (config.sizeBySpeed != null && HasPoints(config.sizeBySpeed.curve))
            {
                var sbs = ps.sizeBySpeed;
                sbs.enabled = true;
                sbs.size = BuildCurve(config.sizeBySpeed.curve);
                sbs.range = new Vector2(
                    config.sizeBySpeed.speedRangeMin,
                    config.sizeBySpeed.speedRangeMax);
            }

            // --- Task A2: Rotation by Speed ---
            if (config.rotationBySpeed != null)
            {
                bool hasCurve = HasPoints(config.rotationBySpeed.curve);
                bool hasConstant = config.rotationBySpeed.angularVelocity != 0;

                if (hasCurve || hasConstant)
                {
                    var rbs = ps.rotationBySpeed;
                    rbs.enabled = true;
                    rbs.range = new Vector2(
                        config.rotationBySpeed.speedRangeMin,
                        config.rotationBySpeed.speedRangeMax);

                    if (hasCurve)
                        rbs.z = BuildCurve(config.rotationBySpeed.curve, Mathf.Deg2Rad);
                    else
                        rbs.z = config.rotationBySpeed.angularVelocity * Mathf.Deg2Rad;
                }
            }

            // --- Task A3: Texture Sheet Animation ---
            if (config.textureSheetAnimation != null && config.textureSheetAnimation.tilesX > 1 || config.textureSheetAnimation != null && config.textureSheetAnimation.tilesY > 1)
            {
                var tsa = ps.textureSheetAnimation;
                tsa.enabled = true;
                tsa.numTilesX = config.textureSheetAnimation.tilesX;
                tsa.numTilesY = config.textureSheetAnimation.tilesY;
                tsa.animation = ParseAnimationMode(config.textureSheetAnimation.animationMode);
                tsa.cycleCount = config.textureSheetAnimation.cycles;

                if (tsa.animation == ParticleSystemAnimationType.SingleRow)
                {
                    tsa.rowMode = ParseRowMode(config.textureSheetAnimation.rowMode);
                    if (tsa.rowMode == ParticleSystemAnimationRowMode.Custom)
                        tsa.rowIndex = config.textureSheetAnimation.rowIndex;
                }

                if (HasPoints(config.textureSheetAnimation.frameOverTime))
                {
                    tsa.frameOverTime = BuildCurve(config.textureSheetAnimation.frameOverTime);
                }
                else if (config.textureSheetAnimation.fps > 0)
                {
                    // Linear 0-1 curve — particle lifetime controls actual playback speed
                    tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f)));
                }

                if (config.textureSheetAnimation.startFrame > 0)
                    tsa.startFrame = config.textureSheetAnimation.startFrame;
            }

            // --- Task A4: Lights ---
            if (config.lights != null)
            {
                var lights = ps.lights;
                lights.enabled = true;
                lights.ratio = config.lights.ratio;
                lights.useParticleColor = config.lights.useParticleColor;
                lights.sizeAffectsRange = config.lights.sizeAffectsRange;
                lights.alphaAffectsIntensity = config.lights.alphaAffectsIntensity;
                lights.rangeMultiplier = config.lights.rangeMultiplier;
                lights.intensityMultiplier = config.lights.intensityMultiplier;
                lights.maxLights = config.lights.maxLights;

                // Create a child light for the module to clone per particle
                var lightGo = new GameObject("ParticleLight");
                lightGo.transform.SetParent(go.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 2f;
                light.intensity = 1f;
                lights.light = light;
                lightGo.SetActive(false);
            }

            // --- Task A4: Collision ---
            if (config.collision != null)
            {
                var col = ps.collision;
                col.enabled = true;
                col.type = ParseCollisionType(config.collision.type);
                col.mode = ParseCollisionMode(config.collision.mode);
                col.bounce = config.collision.bounce;
                col.lifetimeLoss = config.collision.lifetimeLoss;
                col.dampen = config.collision.dampen;
                col.radiusScale = config.collision.radiusScale;
                col.minKillSpeed = config.collision.minKillSpeed;
                col.sendCollisionMessages = config.collision.sendCollisionMessages;
            }

            // --- Task A5: Sub Emitters ---
            if (depth < MAX_SUB_EMITTER_DEPTH &&
                config.subEmitters != null && config.subEmitters.Length > 0)
            {
                var subEmitters = ps.subEmitters;
                subEmitters.enabled = true;

                for (int i = 0; i < config.subEmitters.Length; i++)
                {
                    var entry = config.subEmitters[i];
                    if (entry.config == null) continue;

                    // Recursively create child particle system
                    var childGo = Apply(entry.config, $"{name}_Sub{i}", depth + 1);
                    childGo.transform.SetParent(go.transform, false);

                    var childPS = childGo.GetComponent<ParticleSystem>();
                    var childMain = childPS.main;
                    childMain.playOnAwake = false;

                    // Sub emitters are triggered by parent, not self-emitting
                    var childEmission = childPS.emission;
                    childEmission.enabled = false;

                    subEmitters.AddSubEmitter(
                        childPS,
                        ParseSubEmitterType(entry.trigger),
                        ParseSubEmitterProperties(entry.inheritProperties),
                        entry.emitProbability);
                }
            }

            // Renderer
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>(
                "Default-Particle.mat");

            if (config.renderer != null)
            {
                renderer.renderMode = ParseRenderMode(config.renderer.renderMode);
                renderer.lengthScale = config.renderer.lengthScale;
                renderer.velocityScale = config.renderer.speedScale;
                renderer.sortMode = ParseSortMode(config.renderer.sortMode);
                renderer.normalDirection = config.renderer.normalDirection;
            }

            // Trail material uses the same material as the main renderer
            if (config.trails != null && config.trails.lifetime > 0)
            {
                renderer.trailMaterial = renderer.material;
            }

            return go;
        }

        // --- Helpers ---

        private static bool HasPoints(CurvePoint[] points)
        {
            return points != null && points.Length > 0;
        }

        private static Color ToColor(ColorRGBA c)
        {
            if (c == null) return Color.white;
            return new Color(c.r, c.g, c.b, c.a);
        }

        private static ParticleSystemSimulationSpace ParseSimulationSpace(string space)
        {
            if (string.IsNullOrEmpty(space)) return ParticleSystemSimulationSpace.Local;
            switch (space.ToLower())
            {
                case "world": return ParticleSystemSimulationSpace.World;
                case "custom": return ParticleSystemSimulationSpace.Custom;
                default: return ParticleSystemSimulationSpace.Local;
            }
        }

        private static ParticleSystemShapeType ParseShapeType(string shapeType)
        {
            if (string.IsNullOrEmpty(shapeType)) return ParticleSystemShapeType.Cone;
            switch (shapeType.ToLower())
            {
                case "sphere": return ParticleSystemShapeType.Sphere;
                case "circle": return ParticleSystemShapeType.Circle;
                case "edge": return ParticleSystemShapeType.SingleSidedEdge;
                case "box": return ParticleSystemShapeType.Box;
                case "hemisphere": return ParticleSystemShapeType.Hemisphere;
                case "donut": return ParticleSystemShapeType.Donut;
                default: return ParticleSystemShapeType.Cone;
            }
        }

        private static ParticleSystemRenderMode ParseRenderMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return ParticleSystemRenderMode.Billboard;
            switch (mode.ToLower())
            {
                case "stretchedbillboard": return ParticleSystemRenderMode.Stretch;
                case "stretch": return ParticleSystemRenderMode.Stretch;
                case "horizontalbillboard": return ParticleSystemRenderMode.HorizontalBillboard;
                case "verticalbillboard": return ParticleSystemRenderMode.VerticalBillboard;
                case "mesh": return ParticleSystemRenderMode.Mesh;
                default: return ParticleSystemRenderMode.Billboard;
            }
        }

        private static ParticleSystemSortMode ParseSortMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return ParticleSystemSortMode.None;
            switch (mode.ToLower())
            {
                case "distance": return ParticleSystemSortMode.Distance;
                case "oldestinfront": return ParticleSystemSortMode.OldestInFront;
                case "youngestinfront": return ParticleSystemSortMode.YoungestInFront;
                default: return ParticleSystemSortMode.None;
            }
        }

        private static ParticleSystemInheritVelocityMode ParseInheritVelocityMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return ParticleSystemInheritVelocityMode.Initial;
            switch (mode.ToLower())
            {
                case "current": return ParticleSystemInheritVelocityMode.Current;
                default: return ParticleSystemInheritVelocityMode.Initial;
            }
        }

        private static ParticleSystemAnimationType ParseAnimationMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return ParticleSystemAnimationType.WholeSheet;
            switch (mode.ToLower())
            {
                case "singlerow": return ParticleSystemAnimationType.SingleRow;
                default: return ParticleSystemAnimationType.WholeSheet;
            }
        }

        private static ParticleSystemAnimationRowMode ParseRowMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return ParticleSystemAnimationRowMode.Random;
            switch (mode.ToLower())
            {
                case "custom": return ParticleSystemAnimationRowMode.Custom;
                default: return ParticleSystemAnimationRowMode.Random;
            }
        }

        private static ParticleSystemCollisionType ParseCollisionType(string type)
        {
            if (string.IsNullOrEmpty(type)) return ParticleSystemCollisionType.World;
            switch (type.ToLower())
            {
                case "planes": return ParticleSystemCollisionType.Planes;
                default: return ParticleSystemCollisionType.World;
            }
        }

        private static ParticleSystemCollisionMode ParseCollisionMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return ParticleSystemCollisionMode.Collision2D;
            switch (mode.ToLower())
            {
                case "3d": return ParticleSystemCollisionMode.Collision3D;
                default: return ParticleSystemCollisionMode.Collision2D;
            }
        }

        private static ParticleSystemSubEmitterType ParseSubEmitterType(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return ParticleSystemSubEmitterType.Birth;
            switch (trigger.ToLower())
            {
                case "collision": return ParticleSystemSubEmitterType.Collision;
                case "death": return ParticleSystemSubEmitterType.Death;
                default: return ParticleSystemSubEmitterType.Birth;
            }
        }

        private static ParticleSystemSubEmitterProperties ParseSubEmitterProperties(string props)
        {
            if (string.IsNullOrEmpty(props)) return ParticleSystemSubEmitterProperties.InheritNothing;
            switch (props.ToLower())
            {
                case "color": return ParticleSystemSubEmitterProperties.InheritColor;
                case "size": return ParticleSystemSubEmitterProperties.InheritSize;
                case "rotation": return ParticleSystemSubEmitterProperties.InheritRotation;
                case "lifetime": return ParticleSystemSubEmitterProperties.InheritLifetime;
                case "everything": return ParticleSystemSubEmitterProperties.InheritEverything;
                default: return ParticleSystemSubEmitterProperties.InheritNothing;
            }
        }

        private static ParticleSystem.MinMaxGradient BuildGradient(GradientStop[] stops)
        {
            var gradient = new Gradient();
            var colorKeys = new GradientColorKey[stops.Length];
            var alphaKeys = new GradientAlphaKey[stops.Length];

            for (int i = 0; i < stops.Length; i++)
            {
                var c = ToColor(stops[i].color);
                colorKeys[i] = new GradientColorKey(c, stops[i].time);
                alphaKeys[i] = new GradientAlphaKey(c.a, stops[i].time);
            }

            gradient.SetKeys(colorKeys, alphaKeys);
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static ParticleSystem.MinMaxCurve BuildCurve(CurvePoint[] points, float scale = 1f)
        {
            var keyframes = new Keyframe[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                keyframes[i] = new Keyframe(points[i].time, points[i].value * scale);
            }
            return new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keyframes));
        }
    }
}
