namespace TomatoFighters.Editor.VFX
{
    /// <summary>
    /// Built-in effect presets that pre-fill prompt + structured helpers.
    /// </summary>
    public static class VFXPresets
    {
        public struct Preset
        {
            public string name;
            public string prompt;
            public VFXPromptBuilder.EffectCategory category;
            public VFXPromptBuilder.Intensity intensity;
            public bool glow;
            public bool looping;
            public bool trails;
        }

        public static readonly Preset[] ALL = new[]
        {
            new Preset
            {
                name = "Fire Burst",
                prompt = "Explosive fire burst with white-hot core fading to orange and red embers. Particles rise with updraft and fade out. Include smoke sub-emitter on death.",
                category = VFXPromptBuilder.EffectCategory.Fire,
                intensity = VFXPromptBuilder.Intensity.Intense,
                glow = true, looping = false, trails = false
            },
            new Preset
            {
                name = "Campfire",
                prompt = "Steady campfire with flickering flames rising upward, occasional embers floating up. Warm orange-yellow glow. Noise for organic flicker.",
                category = VFXPromptBuilder.EffectCategory.Fire,
                intensity = VFXPromptBuilder.Intensity.Medium,
                glow = true, looping = true, trails = false
            },
            new Preset
            {
                name = "Heal Aura",
                prompt = "Gentle healing aura with green-white particles spiraling upward from a circle shape. Soft glow, particles shrink and fade. Calming and smooth.",
                category = VFXPromptBuilder.EffectCategory.Heal,
                intensity = VFXPromptBuilder.Intensity.Medium,
                glow = true, looping = true, trails = false
            },
            new Preset
            {
                name = "Lightning Strike",
                prompt = "Fast electric discharge with bright cyan-white sparks. Very short lifetime, high speed, chaotic noise. Thin bright trails.",
                category = VFXPromptBuilder.EffectCategory.Electric,
                intensity = VFXPromptBuilder.Intensity.Intense,
                glow = true, looping = false, trails = true
            },
            new Preset
            {
                name = "Blood Splatter",
                prompt = "Directional blood burst with red droplets affected by gravity. No glow. Particles slow down and fall. Short lifetime.",
                category = VFXPromptBuilder.EffectCategory.Blood,
                intensity = VFXPromptBuilder.Intensity.Medium,
                glow = false, looping = false, trails = false
            },
            new Preset
            {
                name = "Magic Projectile",
                prompt = "Glowing magic orb trail with purple-cyan particles streaming behind. Long trails, noise for organic motion. Particles shrink over time.",
                category = VFXPromptBuilder.EffectCategory.Magic,
                intensity = VFXPromptBuilder.Intensity.Medium,
                glow = true, looping = true, trails = true
            },
            new Preset
            {
                name = "Dust Cloud",
                prompt = "Brown-gray dust cloud expanding outward. No glow. Particles grow in size and fade. Low gravity, slow noise. Good for ground impacts.",
                category = VFXPromptBuilder.EffectCategory.Smoke,
                intensity = VFXPromptBuilder.Intensity.Subtle,
                glow = false, looping = false, trails = false
            },
            new Preset
            {
                name = "Ice Shatter",
                prompt = "Ice crystal burst with blue-white shards flying outward. Slight glow. Particles tumble with rotation by speed. Some collision bounce.",
                category = VFXPromptBuilder.EffectCategory.Ice,
                intensity = VFXPromptBuilder.Intensity.Medium,
                glow = true, looping = false, trails = false
            },
            new Preset
            {
                name = "Sword Slash",
                prompt = "Fast slash arc with bright white core fading to weapon color. Stretched billboard, very short lifetime. Speed-based color change.",
                category = VFXPromptBuilder.EffectCategory.Slash,
                intensity = VFXPromptBuilder.Intensity.Medium,
                glow = true, looping = false, trails = true
            },
            new Preset
            {
                name = "Explosion",
                prompt = "Large explosion with white-hot core expanding to orange fireball then black smoke. Debris with collision. Smoke sub-emitter on death. Screen-filling burst.",
                category = VFXPromptBuilder.EffectCategory.Explosion,
                intensity = VFXPromptBuilder.Intensity.Intense,
                glow = true, looping = false, trails = false
            }
        };

        /// <summary>Returns preset names for dropdown display. First entry is "-- Select Preset --".</summary>
        public static string[] GetDisplayNames()
        {
            var names = new string[ALL.Length + 1];
            names[0] = "-- Select Preset --";
            for (int i = 0; i < ALL.Length; i++)
                names[i + 1] = ALL[i].name;
            return names;
        }
    }
}
