using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TomatoFighters.Editor.VFX
{
    public static class ShaderUtils
    {
        private const string SHADER_FOLDER = "Assets/Shaders/Generated";

        private const string FALLBACK_SHADER_TEMPLATE = @"Shader ""TomatoFighters/{0}""
{{
    Properties
    {{
        _MainTex (""Texture"", 2D) = ""white"" {{}}
        _Color (""Tint"", Color) = (1,1,1,1)
    }}
    SubShader
    {{
        Tags {{ ""Queue""=""Transparent"" ""RenderType""=""Transparent"" ""RenderPipeline""=""UniversalPipeline"" }}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {{
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {{
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            }};

            struct Varyings
            {{
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            }};

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {{
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }}

            half4 frag(Varyings IN) : SV_Target
            {{
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return tex * IN.color;
            }}
            ENDHLSL
        }}
    }}
}}";

        /// <summary>
        /// Load a shader from the generated shaders folder by asset path.
        /// Falls back to Shader.Find, then creates a basic URP shader if all else fails.
        /// </summary>
        public static Shader LoadOrCreateShader(string shaderAssetPath, string shaderName)
        {
            // Try loading by asset path first (most reliable after Refresh)
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);
            if (shader != null) return shader;

            // Try Shader.Find as fallback
            shader = Shader.Find($"TomatoFighters/{shaderName}");
            if (shader != null) return shader;

            // Create a basic fallback shader
            Debug.Log($"[VFX] Shader not found, creating fallback URP shader: {shaderName}");
            string code = string.Format(FALLBACK_SHADER_TEMPLATE, shaderName);

            string fullDir = Path.Combine(Application.dataPath,
                SHADER_FOLDER.Substring("Assets/".Length));
            if (!Directory.Exists(fullDir))
                Directory.CreateDirectory(fullDir);

            string diskPath = Path.Combine(Application.dataPath,
                shaderAssetPath.Substring("Assets/".Length));
            File.WriteAllText(diskPath, code);
            AssetDatabase.Refresh();

            shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);
            if (shader != null) return shader;

            // Last resort
            return Shader.Find("Sprites/Default");
        }

        /// <summary>
        /// Save shader code to disk, load it, and create a material.
        /// Pass null for code to use the built-in fallback URP shader.
        /// Returns the material.
        /// </summary>
        public static Material SaveShaderAndCreateMaterial(string code, string name)
        {
            // Use fallback template if no code provided
            if (string.IsNullOrEmpty(code))
                code = string.Format(FALLBACK_SHADER_TEMPLATE, name);

            string fullDir = Path.Combine(Application.dataPath,
                SHADER_FOLDER.Substring("Assets/".Length));
            if (!Directory.Exists(fullDir))
                Directory.CreateDirectory(fullDir);

            string shaderAssetPath = $"{SHADER_FOLDER}/{name}.shader";
            string diskPath = Path.Combine(Application.dataPath,
                shaderAssetPath.Substring("Assets/".Length));
            File.WriteAllText(diskPath, code);
            AssetDatabase.Refresh();

            var shader = LoadOrCreateShader(shaderAssetPath, name);

            // Validate shader compiled successfully — purple squares come from broken shaders
            if (ShaderUtil.ShaderHasError(shader))
            {
                // Try auto-repair before falling back
                string repairedCode = TryRepairShader(code);
                if (repairedCode != code)
                {
                    Debug.Log($"[VFX] Shader '{name}' has errors. Attempting auto-repair...");
                    File.WriteAllText(diskPath, repairedCode);
                    AssetDatabase.Refresh();
                    shader = LoadOrCreateShader(shaderAssetPath, name);
                }

                if (ShaderUtil.ShaderHasError(shader))
                {
                    Debug.LogWarning($"[VFX] Shader '{name}' could not be repaired. Falling back to default URP particle shader.");
                    string fallbackCode = string.Format(FALLBACK_SHADER_TEMPLATE, name);
                    File.WriteAllText(diskPath, fallbackCode);
                    AssetDatabase.Refresh();
                    shader = LoadOrCreateShader(shaderAssetPath, name);
                }
                else
                {
                    Debug.Log($"[VFX] Shader '{name}' repaired successfully.");
                }
            }

            var mat = new Material(shader);

            // Assign a soft-circle particle texture so particles render as round
            // instead of flat squares. Generated shaders default to a 1x1 white _MainTex.
            if (mat.HasProperty("_MainTex"))
            {
                var particleTex = GetOrCreateParticleTexture();
                if (particleTex != null)
                    mat.SetTexture("_MainTex", particleTex);
            }

            // Reset ALL color properties to white so the particle system's vertex
            // colors (from colorOverLifetime) drive coloring. AI-generated shaders
            // often set custom color properties (_GlowColor, _RingColor, etc.) to
            // near-white HDR values that wash out the particle colors.
            int propCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.Color)
                {
                    string propName = ShaderUtil.GetPropertyName(shader, i);
                    mat.SetColor(propName, Color.white);
                }
            }

            string matPath = $"{SHADER_FOLDER}/{name}_Mat.mat";
            // Overwrite if exists
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(matPath);

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// List existing shaders that compile without errors.
        /// Only returns shaders that Unity can actually load and use.
        /// </summary>
        public static string ListValidShaders()
        {
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { SHADER_FOLDER });
            if (guids.Length == 0)
                return "(none)";

            var sb = new System.Text.StringBuilder();
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                if (shader == null) continue;

                // Skip shaders with compile errors
                if (ShaderUtil.ShaderHasError(shader)) continue;

                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                string shaderName = shader.name;
                int propertyCount = ShaderUtil.GetPropertyCount(shader);

                // Build a brief description from shader properties
                var props = new System.Text.StringBuilder();
                for (int i = 0; i < propertyCount && i < 5; i++)
                {
                    if (i > 0) props.Append(", ");
                    props.Append(ShaderUtil.GetPropertyName(shader, i));
                }

                sb.AppendLine($"- {fileName} (path: \"{shaderName}\", properties: {props})");
            }

            return sb.Length > 0 ? sb.ToString() : "(none — all shaders have errors)";
        }

        /// <summary>
        /// Delete all generated shaders and materials from the shader folder.
        /// Returns the number of assets deleted.
        /// </summary>
        public static int DeleteAllGenerated()
        {
            if (!AssetDatabase.IsValidFolder(SHADER_FOLDER))
                return 0;

            string[] guids = AssetDatabase.FindAssets("", new[] { SHADER_FOLDER });
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Keep the shared particle texture
                if (path.EndsWith("_DefaultParticle.png")) continue;
                if (AssetDatabase.DeleteAsset(path))
                    count++;
            }

            AssetDatabase.Refresh();
            return count;
        }

        /// <summary>
        /// Get or create a procedural soft-circle particle texture.
        /// Saved as an asset so materials keep their reference across reloads.
        /// </summary>
        private static Texture2D GetOrCreateParticleTexture()
        {
            string texPath = SHADER_FOLDER + "/_DefaultParticle.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (existing != null) return existing;

            // Ensure folder exists
            string fullDir = Path.Combine(Application.dataPath,
                SHADER_FOLDER.Substring("Assets/".Length));
            if (!Directory.Exists(fullDir))
                Directory.CreateDirectory(fullDir);

            // Generate a soft radial gradient (white circle with alpha falloff)
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    alpha *= alpha; // quadratic falloff for smooth edges
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            // Save to disk
            byte[] png = tex.EncodeToPNG();
            string diskPath = Path.Combine(Application.dataPath,
                texPath.Substring("Assets/".Length));
            File.WriteAllBytes(diskPath, png);
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();

            // Configure import settings
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }

        /// <summary>
        /// Attempt to fix common AI-generated shader mistakes that cause compile errors.
        /// Returns the repaired code, or the original if no repairs were applicable.
        /// </summary>
        private static string TryRepairShader(string code)
        {
            string repaired = code;

            // Fix 1: Missing #pragma vertex/fragment directives
            if (!repaired.Contains("#pragma vertex"))
            {
                int hlslIdx = repaired.IndexOf("HLSLPROGRAM");
                if (hlslIdx >= 0)
                {
                    int lineEnd = repaired.IndexOf('\n', hlslIdx);
                    if (lineEnd >= 0)
                    {
                        repaired = repaired.Insert(lineEnd + 1,
                            "            #pragma vertex vert\n            #pragma fragment frag\n");
                    }
                }
            }
            else if (!repaired.Contains("#pragma fragment"))
            {
                int pragmaVertex = repaired.IndexOf("#pragma vertex");
                int lineEnd = repaired.IndexOf('\n', pragmaVertex);
                if (lineEnd >= 0)
                {
                    repaired = repaired.Insert(lineEnd + 1,
                        "            #pragma fragment frag\n");
                }
            }

            // Fix 2: TransformObjectToHClip takes float3, not float4
            // Match TransformObjectToHClip(X) where X doesn't end with .xyz
            repaired = Regex.Replace(repaired,
                @"TransformObjectToHClip\((\w+(?:\.\w+)*)\)",
                match =>
                {
                    string arg = match.Groups[1].Value;
                    if (!arg.EndsWith(".xyz"))
                        return $"TransformObjectToHClip({arg}.xyz)";
                    return match.Value;
                });

            // Fix 3: Legacy built-in syntax replacements
            repaired = repaired.Replace("CGPROGRAM", "HLSLPROGRAM");
            repaired = repaired.Replace("ENDCG", "ENDHLSL");
            repaired = repaired.Replace("UnityObjectToClipPos", "TransformObjectToHClip");

            // Fix 4: Remove GrabPass and _GrabTexture — not supported in URP
            repaired = Regex.Replace(repaired, @"GrabPass\s*\{[^}]*\}\s*", "");
            repaired = Regex.Replace(repaired, @"^\s*.*_GrabTexture.*$", "",
                RegexOptions.Multiline);
            // Re-run fix 2 in case fix 3 introduced a new TransformObjectToHClip call
            repaired = Regex.Replace(repaired,
                @"TransformObjectToHClip\((\w+(?:\.\w+)*)\)",
                match =>
                {
                    string arg = match.Groups[1].Value;
                    if (!arg.EndsWith(".xyz"))
                        return $"TransformObjectToHClip({arg}.xyz)";
                    return match.Value;
                });

            return repaired;
        }
    }
}
