using System.Collections.Generic;

namespace TomatoFighters.Editor.VFX
{
    /// <summary>
    /// Post-generation validation for ParticleConfig.
    /// Checks for common issues and provides actionable warnings.
    /// </summary>
    public static class VFXQualityChecker
    {
        public struct QualityWarning
        {
            public string level; // "warning" or "info"
            public string message;

            public QualityWarning(string level, string message)
            {
                this.level = level;
                this.message = message;
            }
        }

        public static List<QualityWarning> Check(ParticleConfig config)
        {
            var warnings = new List<QualityWarning>();
            if (config == null) return warnings;

            // Color over lifetime
            if (config.colorOverLifetime?.gradient == null || config.colorOverLifetime.gradient.Length == 0)
            {
                warnings.Add(new QualityWarning("warning", "No colorOverLifetime — particles won't fade, will pop out of existence"));
            }
            else
            {
                // Check if alpha fades to 0 at the end
                var stops = config.colorOverLifetime.gradient;
                var last = stops[stops.Length - 1];
                if (last.color != null && last.color.a > 0.1f)
                {
                    warnings.Add(new QualityWarning("warning", "No alpha fade at gradient end — particles will pop out of existence"));
                }
            }

            // Size over lifetime
            if (config.sizeOverLifetime?.curve == null || config.sizeOverLifetime.curve.Length == 0)
            {
                warnings.Add(new QualityWarning("info", "No sizeOverLifetime — particles stay static size"));
            }

            // Noise
            if (config.noise == null || config.noise.strength <= 0)
            {
                warnings.Add(new QualityWarning("info", "No noise module — may look mechanical/uniform"));
            }

            // HDR glow check
            bool hasHdr = false;
            if (config.main?.startColor != null)
            {
                var c = config.main.startColor;
                if (c.r > 1f || c.g > 1f || c.b > 1f) hasHdr = true;
            }
            if (config.colorOverLifetime?.gradient != null)
            {
                foreach (var stop in config.colorOverLifetime.gradient)
                {
                    if (stop.color != null && (stop.color.r > 1f || stop.color.g > 1f || stop.color.b > 1f))
                    {
                        hasHdr = true;
                        break;
                    }
                }
            }
            if (!hasHdr)
            {
                warnings.Add(new QualityWarning("info", "All colors <= 1.0 — no glow/bloom effect"));
            }

            // Check for equally-high HDR channels (white glow)
            if (config.main?.startColor != null)
            {
                var c = config.main.startColor;
                if (c.r > 2f && c.g > 2f && c.b > 2f)
                {
                    float min = System.Math.Min(c.r, System.Math.Min(c.g, c.b));
                    float max = System.Math.Max(c.r, System.Math.Max(c.g, c.b));
                    if (max > 0 && min / max > 0.8f)
                    {
                        warnings.Add(new QualityWarning("warning", "HDR channels nearly equal — will glow white instead of colored"));
                    }
                }
            }

            // Burst + looping check
            bool hasBursts = config.emission?.bursts != null && config.emission.bursts.Length > 0;
            if (hasBursts && config.main != null && config.main.looping)
            {
                warnings.Add(new QualityWarning("info", "Burst + looping=true — usually bursts are for one-shot effects (looping=false)"));
            }

            // Burst + rateOverTime check
            if (hasBursts && config.emission != null && config.emission.rateOverTime > 0)
            {
                warnings.Add(new QualityWarning("info", "Burst with rateOverTime > 0 — may want rateOverTime=0 for pure burst effects"));
            }

            // Z velocity in 2D
            if (config.velocityOverLifetime != null)
            {
                if (HasNonZeroValues(config.velocityOverLifetime.z))
                {
                    warnings.Add(new QualityWarning("warning", "Z velocity non-zero — may cause depth issues in 2D"));
                }
            }
            if (config.forceOverLifetime != null)
            {
                if (HasNonZeroValues(config.forceOverLifetime.z))
                {
                    warnings.Add(new QualityWarning("warning", "Z force non-zero — may cause depth issues in 2D"));
                }
            }

            // Low maxParticles for looping
            if (config.main != null && config.main.looping && config.main.maxParticles < 50)
            {
                warnings.Add(new QualityWarning("warning", $"maxParticles={config.main.maxParticles} for looping effect — may look sparse (recommend 50+)"));
            }

            // Trails without compatible render mode
            if (config.trails != null && config.trails.lifetime > 0 && config.renderer != null)
            {
                string rm = config.renderer.renderMode?.ToLower() ?? "billboard";
                if (rm == "mesh" || rm == "horizontalbillboard")
                {
                    warnings.Add(new QualityWarning("warning", "Trails with incompatible renderMode — use Billboard or StretchedBillboard"));
                }
            }

            return warnings;
        }

        private static bool HasNonZeroValues(CurvePoint[] points)
        {
            if (points == null) return false;
            foreach (var p in points)
            {
                if (System.Math.Abs(p.value) > 0.01f) return true;
            }
            return false;
        }
    }
}
