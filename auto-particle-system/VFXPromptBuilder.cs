using UnityEngine;

namespace TomatoFighters.Editor.VFX
{
    /// <summary>
    /// Builds structured AI prompts from user selections.
    /// Injects explicit context (intensity, glow, loop, trails, color) so the AI
    /// doesn't have to guess from vague free-text descriptions.
    /// </summary>
    public static class VFXPromptBuilder
    {
        public enum EffectCategory { Fire, Ice, Electric, Heal, Magic, Blood, Smoke, Explosion, Slash, Custom }
        public enum Intensity { Subtle, Medium, Intense }

        public static string BuildParticlePrompt(
            string freeText,
            EffectCategory category,
            Intensity intensity,
            bool glow,
            bool looping,
            bool trails,
            Color? colorHint)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append($"Create a {intensity.ToString().ToUpper()} intensity {category} effect.");

            if (glow)
            {
                switch (intensity)
                {
                    case Intensity.Subtle:
                        sb.Append(" It should GLOW (use HDR colors 1.5-2.5x intensity).");
                        break;
                    case Intensity.Medium:
                        sb.Append(" It should GLOW (use HDR colors 2.5-4.0x intensity).");
                        break;
                    case Intensity.Intense:
                        sb.Append(" It should GLOW BRIGHTLY (use HDR colors 4.0-8.0x intensity).");
                        break;
                }
            }
            else
            {
                sb.Append(" No glow — keep all color channels in 0.0-1.0 range (LDR).");
            }

            if (looping)
                sb.Append(" LOOP continuously (looping=true, use rateOverTime).");
            else
                sb.Append(" ONE-SHOT effect (looping=false, use bursts, rateOverTime=0).");

            if (trails)
                sb.Append(" Include TRAILS on particles.");

            if (colorHint.HasValue)
            {
                var c = colorHint.Value;
                sb.Append($" Color hint: ({c.r:F2}, {c.g:F2}, {c.b:F2}).");
                if (glow)
                    sb.Append(" Scale this color up to the HDR intensity range specified above.");
            }

            switch (intensity)
            {
                case Intensity.Subtle:
                    sb.Append(" Particle count: 10-30, small size, short duration.");
                    break;
                case Intensity.Medium:
                    sb.Append(" Particle count: 30-80, medium size.");
                    break;
                case Intensity.Intense:
                    sb.Append(" Particle count: 80-200+, large particles, longer duration.");
                    break;
            }

            sb.Append(" This is for a 2D side-scrolling game — keep Z velocity near 0.");

            if (!string.IsNullOrWhiteSpace(freeText))
                sb.Append($"\n\nAdditional details: {freeText.Trim()}");

            return sb.ToString();
        }

        public static string BuildShaderPrompt(
            string freeText,
            EffectCategory category,
            bool glow,
            string shaderName)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append($"Shader name: {shaderName}\n\n");
            sb.Append($"Create a particle shader for a {category} effect.");

            if (glow)
                sb.Append(" Use ADDITIVE blending (Blend One One) for glow/bloom.");
            else
                sb.Append(" Use ALPHA blending (Blend SrcAlpha OneMinusSrcAlpha).");

            sb.Append(" Support vertex colors for particle system tinting.");

            if (!string.IsNullOrWhiteSpace(freeText))
                sb.Append($"\n\nAdditional details: {freeText.Trim()}");

            return sb.ToString();
        }
    }
}
