namespace TomatoFighters.Editor.VFX
{
    [System.Serializable]
    public class ParticleConfig
    {
        public MainModuleConfig main;
        public EmissionConfig emission;
        public ShapeConfig shape;
        public ColorOverLifetimeConfig colorOverLifetime;
        public SizeOverLifetimeConfig sizeOverLifetime;
        public VelocityOverLifetimeConfig velocityOverLifetime;
        public NoiseConfig noise;
        public RotationOverLifetimeConfig rotationOverLifetime;
        public TrailsConfig trails;
        public RendererConfig renderer;

        // Task A1: Force & Velocity modules
        public ForceOverLifetimeConfig forceOverLifetime;
        public LimitVelocityOverLifetimeConfig limitVelocityOverLifetime;
        public InheritVelocityConfig inheritVelocity;
        public LifetimeByEmitterSpeedConfig lifetimeByEmitterSpeed;

        // Task A2: Speed-reactive modules
        public ColorBySpeedConfig colorBySpeed;
        public SizeBySpeedConfig sizeBySpeed;
        public RotationBySpeedConfig rotationBySpeed;

        // Task A3: Texture sheet animation
        public TextureSheetAnimationConfig textureSheetAnimation;

        // Task A4: Lights & Collision
        public ParticleLightsConfig lights;
        public CollisionConfig collision;

        // Task A5: Sub Emitters
        public SubEmitterEntry[] subEmitters;
    }

    [System.Serializable]
    public class MainModuleConfig
    {
        public float duration = 1.0f;
        public bool looping;
        public float startLifetime = 1.0f;
        public float startSpeed = 5.0f;
        public float startSize = 0.5f;
        public ColorRGBA startColor;
        public float startRotation;          // degrees
        public float gravityModifier;
        public string simulationSpace = "Local";
        public int maxParticles = 100;
    }

    [System.Serializable]
    public class EmissionConfig
    {
        public float rateOverTime = 10f;
        public BurstConfig[] bursts;
    }

    [System.Serializable]
    public class BurstConfig
    {
        public float time;
        public int count;
    }

    [System.Serializable]
    public class ShapeConfig
    {
        public string shapeType = "Cone";
        public float angle = 25f;
        public float radius = 1.0f;
        public float arc = 360f;
        public bool randomDirection;         // randomize particle direction within shape
    }

    [System.Serializable]
    public class ColorOverLifetimeConfig
    {
        public GradientStop[] gradient;
    }

    [System.Serializable]
    public class GradientStop
    {
        public float time;
        public ColorRGBA color;
    }

    [System.Serializable]
    public class SizeOverLifetimeConfig
    {
        public CurvePoint[] curve;
    }

    [System.Serializable]
    public class VelocityOverLifetimeConfig
    {
        public CurvePoint[] x;               // velocity curve over lifetime on X
        public CurvePoint[] y;               // velocity curve over lifetime on Y
        public CurvePoint[] z;               // velocity curve over lifetime on Z
        public float speedModifier = 1f;     // overall speed multiplier curve
        public string space = "Local";       // "Local" or "World"
    }

    [System.Serializable]
    public class NoiseConfig
    {
        public float strength = 1f;          // how much particles are displaced
        public float frequency = 0.5f;       // how fast the noise pattern changes
        public int octaves = 1;              // detail layers (1-3, higher = more complex)
        public float scrollSpeed;            // animates noise pattern over time
        public float damping;                // reduces noise strength over particle lifetime (0-1)
    }

    [System.Serializable]
    public class RotationOverLifetimeConfig
    {
        public float angularVelocity;        // degrees per second (constant)
        public CurvePoint[] curve;           // or use a curve for varying rotation speed
    }

    [System.Serializable]
    public class TrailsConfig
    {
        public float lifetime = 0.5f;        // trail duration in seconds
        public float widthMultiplier = 1f;   // trail width relative to particle size
        public int minimumVertexDistance = 1; // lower = smoother trail, more verts
        public GradientStop[] colorGradient; // trail color over its length
        public CurvePoint[] widthCurve;      // trail width over its length (0=head, 1=tail)
        public bool dieWithParticle = true;
    }

    [System.Serializable]
    public class RendererConfig
    {
        public string renderMode = "Billboard";  // "Billboard", "StretchedBillboard", "Mesh"
        public float lengthScale = 1f;            // for StretchedBillboard: stretch by speed
        public float speedScale;                  // for StretchedBillboard: additional speed stretch
        public string sortMode = "None";          // "None", "Distance", "OldestInFront", "YoungestInFront"
        public float normalDirection = 1f;        // 0 = face camera, 1 = face up (for 2D)
    }

    [System.Serializable]
    public class CurvePoint
    {
        public float time;
        public float value;
    }

    [System.Serializable]
    public class ColorRGBA
    {
        public float r = 1f;
        public float g = 1f;
        public float b = 1f;
        public float a = 1f;
    }

    // --- Task A1: Force & Velocity modules ---

    [System.Serializable]
    public class ForceOverLifetimeConfig
    {
        public CurvePoint[] x;
        public CurvePoint[] y;
        public CurvePoint[] z;
        public string space = "Local";
        public bool randomized;
    }

    [System.Serializable]
    public class LimitVelocityOverLifetimeConfig
    {
        public float speed = 1f;
        public CurvePoint[] speedCurve;
        public float dampen = 0.1f;
        public float drag;
        public bool multiplyDragBySize;
        public bool multiplyDragByVelocity;
        public string space = "Local";
    }

    [System.Serializable]
    public class InheritVelocityConfig
    {
        public string mode = "Initial";
        public float multiplier = 1f;
    }

    [System.Serializable]
    public class LifetimeByEmitterSpeedConfig
    {
        public CurvePoint[] curve;
        public float speedRangeMin;
        public float speedRangeMax = 1f;
    }

    // --- Task A2: Speed-reactive modules ---

    [System.Serializable]
    public class ColorBySpeedConfig
    {
        public GradientStop[] gradient;
        public float speedRangeMin;
        public float speedRangeMax = 1f;
    }

    [System.Serializable]
    public class SizeBySpeedConfig
    {
        public CurvePoint[] curve;
        public float speedRangeMin;
        public float speedRangeMax = 1f;
    }

    [System.Serializable]
    public class RotationBySpeedConfig
    {
        public float angularVelocity;
        public CurvePoint[] curve;
        public float speedRangeMin;
        public float speedRangeMax = 1f;
    }

    // --- Task A3: Texture sheet animation ---

    [System.Serializable]
    public class TextureSheetAnimationConfig
    {
        public int tilesX = 1;
        public int tilesY = 1;
        public string animationMode = "WholeSheet";
        public CurvePoint[] frameOverTime;
        public float startFrame;
        public int cycles = 1;
        public string rowMode = "Random";
        public int rowIndex;
        public float fps;
    }

    // --- Task A4: Lights & Collision ---

    [System.Serializable]
    public class ParticleLightsConfig
    {
        public float ratio = 1f;
        public bool useParticleColor = true;
        public bool sizeAffectsRange = true;
        public bool alphaAffectsIntensity = true;
        public float rangeMultiplier = 1f;
        public float intensityMultiplier = 1f;
        public int maxLights = 20;
    }

    [System.Serializable]
    public class CollisionConfig
    {
        public string type = "World";
        public string mode = "2D";
        public float bounce = 0.5f;
        public float lifetimeLoss;
        public float dampen = 0.5f;
        public float radiusScale = 0.5f;
        public float minKillSpeed;
        public bool sendCollisionMessages;
    }

    // --- Task A5: Sub Emitters ---

    [System.Serializable]
    public class SubEmitterEntry
    {
        public string trigger = "Birth";
        public ParticleConfig config;
        public string inheritProperties = "Nothing";
        public float emitProbability = 1f;
    }
}
