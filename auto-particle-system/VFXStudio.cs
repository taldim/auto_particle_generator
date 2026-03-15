using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TomatoFighters.Editor.VFX
{
    public class VFXStudio : EditorWindow
    {
        // --- Enums ---
        private enum Tab { Particle, Shader, Composer }
        private enum Mode { API, Manual }
        private enum ComposerStep { Describe, ShaderPrompt, ShaderPaste, ParticlePrompt, ParticlePaste }

        // --- State ---
        private Tab currentTab = Tab.Particle;
        private Mode currentMode = Mode.API;
        private ComposerStep composerStep = ComposerStep.Describe;

        // Input fields
        private string prompt = "";
        private string effectName = "";
        private string refinementPrompt = "";

        // Structured prompt helpers
        private VFXPromptBuilder.EffectCategory category = VFXPromptBuilder.EffectCategory.Fire;
        private VFXPromptBuilder.Intensity intensity = VFXPromptBuilder.Intensity.Medium;
        private bool helperGlow = true;
        private bool helperLooping;
        private bool helperTrails;
        private Color colorHint = new Color(1f, 0.5f, 0.1f);
        private bool useColorHint;

        // Preset
        private int selectedPreset;

        // Composer options
        private bool allowReuseShader = true;

        // Generation state
        private string lastShaderCode = "";
        private string lastParticleJson = "";
        private string lastOutputPreview = "";
        private bool hasGenerated;
        private bool isProcessing;
        private float progressValue;
        private string progressLabel = "";

        // Manual mode
        private string generatedPrompt = "";
        private string pastedResponse = "";
        private Material composedMaterial;

        // History (last 5 generations)
        private List<string> generationHistory = new List<string>();
        private int selectedHistory;

        // Quality checks
        private List<VFXQualityChecker.QualityWarning> qualityWarnings = new List<VFXQualityChecker.QualityWarning>();

        // UI state
        private string apiKeyInput = "";
        private string statusLog = "";
        private Vector2 mainScroll;
        private Vector2 statusScroll;
        private Vector2 promptScroll;
        private Vector2 pasteScroll;
        private Vector2 outputScroll;
        private GameObject sceneInstance;

        // Foldout states (persisted via EditorPrefs)
        private bool foldDescription = true;
        private bool foldPreset;
        private bool foldHelpers;
        private bool foldShaderOptions;
        private bool foldRefinement;
        private bool foldOutput;
        private bool foldQuality = true;
        private bool foldStatus = true;
        private bool foldApiSettings;

        // EditorPrefs keys
        private const string PREF_PREFIX = "VFXStudio_";
        private const string SHADER_FOLDER = "Assets/Shaders/Generated";
        private const string PREFAB_FOLDER = "Assets/Prefabs/VFX";

        [MenuItem("TomatoFighters/VFX Studio")]
        public static void ShowWindow()
        {
            var window = GetWindow<VFXStudio>("VFX Studio");
            window.minSize = new Vector2(420, 600);
        }

        private void OnEnable()
        {
            apiKeyInput = ClaudeApiClient.GetApiKey();
            LoadFoldoutStates();
        }

        private void OnDisable()
        {
            SaveFoldoutStates();
        }

        private void OnGUI()
        {
            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

            DrawTabBar();
            DrawModeToggle();

            EditorGUILayout.Space(4);

            // Progress bar during processing
            if (isProcessing)
            {
                var rect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
                EditorGUI.ProgressBar(rect, progressValue, progressLabel);
                EditorGUILayout.Space(4);
            }

            DrawDescriptionSection();
            DrawPresetSection();
            DrawHelpersSection();

            if (currentTab == Tab.Composer)
                DrawShaderOptionsSection();

            if (currentMode == Mode.API)
                DrawApiMode();
            else
                DrawManualMode();

            DrawRefinementSection();
            DrawOutputSection();

            if (currentTab != Tab.Shader)
                DrawQualitySection();

            DrawStatusSection();
            DrawApiSettingsSection();

            EditorGUILayout.EndScrollView();
        }

        // =====================================================
        // TAB BAR & MODE TOGGLE
        // =====================================================

        private void DrawTabBar()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            var tabNames = new[] { "Particle", "Shader", "Composer" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                var style = (Tab)i == currentTab ? EditorStyles.toolbarButton : EditorStyles.toolbarButton;
                bool wasActive = (Tab)i == currentTab;
                GUI.backgroundColor = wasActive ? new Color(0.7f, 0.85f, 1f) : Color.white;
                if (GUILayout.Toggle(wasActive, tabNames[i], EditorStyles.toolbarButton))
                {
                    if (!wasActive)
                    {
                        currentTab = (Tab)i;
                        ResetManualState();
                    }
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModeToggle()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Mode:");
            if (GUILayout.Toggle(currentMode == Mode.API, "API", EditorStyles.miniButtonLeft))
                currentMode = Mode.API;
            if (GUILayout.Toggle(currentMode == Mode.Manual, "Manual", EditorStyles.miniButtonRight))
                currentMode = Mode.Manual;
            EditorGUILayout.EndHorizontal();
        }

        // =====================================================
        // SECTIONS
        // =====================================================

        private void DrawDescriptionSection()
        {
            foldDescription = EditorGUILayout.Foldout(foldDescription, "Effect Description", true);
            if (!foldDescription) return;

            EditorGUI.indentLevel++;
            prompt = EditorGUILayout.TextArea(prompt, GUILayout.Height(60));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name:", GUILayout.Width(40));
            effectName = EditorGUILayout.TextField(effectName);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("(auto-generated from prompt if blank)", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawPresetSection()
        {
            foldPreset = EditorGUILayout.Foldout(foldPreset, "Preset", true);
            if (!foldPreset) return;

            EditorGUI.indentLevel++;
            int newPreset = EditorGUILayout.Popup(selectedPreset, VFXPresets.GetDisplayNames());
            if (newPreset != selectedPreset)
            {
                selectedPreset = newPreset;
                if (selectedPreset > 0)
                    ApplyPreset(VFXPresets.ALL[selectedPreset - 1]);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawHelpersSection()
        {
            foldHelpers = EditorGUILayout.Foldout(foldHelpers, "Prompt Helpers (optional)", true);
            if (!foldHelpers) return;

            EditorGUI.indentLevel++;
            category = (VFXPromptBuilder.EffectCategory)EditorGUILayout.EnumPopup("Category", category);
            intensity = (VFXPromptBuilder.Intensity)EditorGUILayout.EnumPopup("Intensity", intensity);
            helperGlow = EditorGUILayout.Toggle("Glow", helperGlow);
            helperLooping = EditorGUILayout.Toggle("Looping", helperLooping);
            helperTrails = EditorGUILayout.Toggle("Trails", helperTrails);

            EditorGUILayout.BeginHorizontal();
            useColorHint = EditorGUILayout.Toggle("Color Hint", useColorHint);
            if (useColorHint)
                colorHint = EditorGUILayout.ColorField(colorHint);
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        private void DrawShaderOptionsSection()
        {
            foldShaderOptions = EditorGUILayout.Foldout(foldShaderOptions, "Shader Options", true);
            if (!foldShaderOptions) return;

            EditorGUI.indentLevel++;
            allowReuseShader = EditorGUILayout.Toggle("Allow reuse existing shader", allowReuseShader);
            EditorGUI.indentLevel--;
        }

        private void DrawRefinementSection()
        {
            if (!hasGenerated) return;

            foldRefinement = EditorGUILayout.Foldout(foldRefinement, "Refinement", true);
            if (!foldRefinement) return;

            EditorGUI.indentLevel++;
            refinementPrompt = EditorGUILayout.TextArea(refinementPrompt, GUILayout.Height(40));

            // History dropdown
            if (generationHistory.Count > 1)
            {
                var historyNames = new string[generationHistory.Count];
                for (int i = 0; i < generationHistory.Count; i++)
                {
                    string preview = generationHistory[i];
                    if (preview.Length > 60) preview = preview.Substring(0, 60) + "...";
                    historyNames[i] = $"#{i + 1}: {preview}";
                }

                int newHistory = EditorGUILayout.Popup("History", selectedHistory, historyNames);
                if (newHistory != selectedHistory)
                {
                    selectedHistory = newHistory;
                    if (currentTab == Tab.Shader)
                    {
                        lastShaderCode = generationHistory[selectedHistory];
                        lastOutputPreview = lastShaderCode;
                    }
                    else
                    {
                        lastParticleJson = generationHistory[selectedHistory];
                        lastOutputPreview = lastParticleJson;
                    }
                }
            }
            EditorGUI.indentLevel--;
        }

        private void DrawOutputSection()
        {
            if (string.IsNullOrEmpty(lastOutputPreview)) return;

            foldOutput = EditorGUILayout.Foldout(foldOutput, "Generated Output", true);
            if (!foldOutput) return;

            EditorGUI.indentLevel++;
            outputScroll = EditorGUILayout.BeginScrollView(outputScroll, GUILayout.Height(150));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(lastOutputPreview, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Copy Output to Clipboard", GUILayout.Height(20)))
            {
                GUIUtility.systemCopyBuffer = lastOutputPreview;
                AppendStatus("Output copied to clipboard.");
            }
            EditorGUI.indentLevel--;
        }

        private void DrawQualitySection()
        {
            if (qualityWarnings.Count == 0) return;

            foldQuality = EditorGUILayout.Foldout(foldQuality, $"Quality Checks ({qualityWarnings.Count})", true);
            if (!foldQuality) return;

            EditorGUI.indentLevel++;
            foreach (var w in qualityWarnings)
            {
                var icon = w.level == "warning" ? MessageType.Warning : MessageType.Info;
                string prefix = w.level == "warning" ? "!!" : "OK";
                EditorGUILayout.HelpBox($"{prefix}  {w.message}", icon);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawStatusSection()
        {
            foldStatus = EditorGUILayout.Foldout(foldStatus, "Status Log", true);
            if (!foldStatus) return;

            statusScroll = EditorGUILayout.BeginScrollView(statusScroll, GUILayout.Height(80));
            EditorGUILayout.TextArea(statusLog, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void DrawApiSettingsSection()
        {
            foldApiSettings = EditorGUILayout.Foldout(foldApiSettings, "API Settings", true);
            if (!foldApiSettings) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            apiKeyInput = EditorGUILayout.PasswordField("API Key:", apiKeyInput);
            if (GUILayout.Button("Save", GUILayout.Width(50)))
            {
                ClaudeApiClient.SetApiKey(apiKeyInput);
                var detected = ClaudeApiClient.DetectPlatform(apiKeyInput);
                AppendStatus($"API key saved. Platform: {ClaudeApiClient.PlatformLabel(detected)}");
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(apiKeyInput))
            {
                var platform = ClaudeApiClient.DetectPlatform(apiKeyInput);
                EditorGUILayout.LabelField($"Platform: {ClaudeApiClient.PlatformLabel(platform)}", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        // =====================================================
        // API MODE
        // =====================================================

        private void DrawApiMode()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(isProcessing || string.IsNullOrWhiteSpace(prompt));
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
                ApiGenerate();
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(isProcessing || !hasGenerated || string.IsNullOrWhiteSpace(refinementPrompt));
            if (GUILayout.Button("Refine", GUILayout.Height(30)))
                ApiRefine();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private async void ApiGenerate()
        {
            string name = GetEffectName();
            isProcessing = true;
            Repaint();

            try
            {
                string userPrompt = BuildFullPrompt();

                switch (currentTab)
                {
                    case Tab.Particle:
                        SetProgress(0.5f, "[1/1] Generating particles...");
                        var config = await ClaudeApiClient.SendPrompt(BuildFullPrompt());
                        lastParticleJson = JsonUtility.ToJson(config, true);
                        lastOutputPreview = lastParticleJson;
                        ApplyParticle(config, name, null);
                        RunQualityCheck(config);
                        AddToHistory(lastParticleJson);
                        AppendStatus($"Done! {PREFAB_FOLDER}/{name}.prefab");
                        break;

                    case Tab.Shader:
                        SetProgress(0.5f, "[1/1] Generating shader...");
                        string shaderUserPrompt = $"Shader name: {name}\n\nEffect description: {userPrompt}";
                        string code = await ClaudeApiClient.SendRawPrompt(VFXPrompts.SHADER_NEW, shaderUserPrompt);
                        lastShaderCode = code;
                        lastOutputPreview = code;
                        ShaderUtils.SaveShaderAndCreateMaterial(code, name);
                        AddToHistory(code);
                        AppendStatus($"Done! {SHADER_FOLDER}/{name}.shader");
                        break;

                    case Tab.Composer:
                        await ApiCompose(name, userPrompt, false);
                        break;
                }

                hasGenerated = true;
            }
            catch (Exception e)
            {
                AppendStatus($"Error: {e.Message}");
            }
            finally
            {
                isProcessing = false;
                progressValue = 0;
                progressLabel = "";
                Repaint();
            }
        }

        private async void ApiRefine()
        {
            string name = GetEffectName();
            isProcessing = true;
            Repaint();

            try
            {
                switch (currentTab)
                {
                    case Tab.Particle:
                        SetProgress(0.5f, "[1/1] Refining particles...");
                        var config = await ClaudeApiClient.SendRefinement(lastParticleJson, refinementPrompt);
                        lastParticleJson = JsonUtility.ToJson(config, true);
                        lastOutputPreview = lastParticleJson;
                        ApplyParticle(config, name, null);
                        RunQualityCheck(config);
                        AddToHistory(lastParticleJson);
                        AppendStatus($"Refined! {PREFAB_FOLDER}/{name}.prefab");
                        break;

                    case Tab.Shader:
                        SetProgress(0.5f, "[1/1] Refining shader...");
                        string shaderPrompt = $"Previous shader code:\n{lastShaderCode}\n\nRefinement request: {refinementPrompt}\n\nReturn the complete updated shader code.";
                        string code = await ClaudeApiClient.SendRawPrompt(VFXPrompts.SHADER_NEW, shaderPrompt);
                        lastShaderCode = code;
                        lastOutputPreview = code;
                        ShaderUtils.SaveShaderAndCreateMaterial(code, name);
                        AddToHistory(code);
                        AppendStatus($"Refined! {SHADER_FOLDER}/{name}.shader");
                        break;

                    case Tab.Composer:
                        string userPrompt = BuildFullPrompt();
                        await ApiCompose(name, userPrompt, true);
                        break;
                }
            }
            catch (Exception e)
            {
                AppendStatus($"Error: {e.Message}");
            }
            finally
            {
                isProcessing = false;
                progressValue = 0;
                progressLabel = "";
                Repaint();
            }
        }

        private async System.Threading.Tasks.Task ApiCompose(string name, string userPrompt, bool isRefine)
        {
            // Step 1: Shader
            SetProgress(0.33f, "[1/3] " + (isRefine ? "Refining" : (allowReuseShader ? "Choosing" : "Creating")) + " shader...");
            Repaint();

            string shaderUserPrompt;
            string systemPrompt;
            if (allowReuseShader)
            {
                string existingShaders = ShaderUtils.ListValidShaders();
                shaderUserPrompt = isRefine
                    ? $"Previous shader:\n{lastShaderCode}\n\nEffect description: {userPrompt}\nRefinement: {refinementPrompt}\n\nExisting shaders:\n{existingShaders}"
                    : $"Effect description: {userPrompt}\n\nExisting shaders in project:\n{existingShaders}";
                systemPrompt = VFXPrompts.SHADER_PICK_OR_CREATE;
            }
            else
            {
                shaderUserPrompt = isRefine
                    ? $"Previous shader:\n{lastShaderCode}\n\nShader name: {name}\n\nEffect description: {userPrompt}\nRefinement: {refinementPrompt}"
                    : $"Shader name: {name}\n\nEffect description: {userPrompt}";
                systemPrompt = VFXPrompts.SHADER_NEW;
            }

            string shaderResponse = await ClaudeApiClient.SendRawPrompt(systemPrompt, shaderUserPrompt);
            Material material = ProcessShaderResponse(shaderResponse, name);

            // Step 2: Particles
            SetProgress(0.66f, "[2/3] " + (isRefine ? "Refining" : "Generating") + " particles...");
            Repaint();

            string particleUserPrompt = isRefine
                ? $"Previous config:\n{lastParticleJson}\n\nEffect: {userPrompt}\nShader: {material.shader.name}\nRefinement: {refinementPrompt}\n\nReturn the complete updated ParticleConfig JSON."
                : $"Effect description: {userPrompt}\nShader being used: {material.shader.name}\n\nGenerate a ParticleConfig that works well with this shader.";
            string particleResponse = await ClaudeApiClient.SendRawPrompt(VFXPrompts.PARTICLE_SYSTEM, particleUserPrompt);
            var config = JsonUtility.FromJson<ParticleConfig>(particleResponse);
            lastParticleJson = JsonUtility.ToJson(config, true);
            lastOutputPreview = lastParticleJson;

            // Step 3: Assemble
            SetProgress(1f, "[3/3] Assembling prefab...");
            Repaint();

            ApplyParticle(config, name, material);
            RunQualityCheck(config);
            AddToHistory(lastParticleJson);
            hasGenerated = true;
            AppendStatus($"Done! {PREFAB_FOLDER}/{name}.prefab");
        }

        // =====================================================
        // MANUAL MODE
        // =====================================================

        private void DrawManualMode()
        {
            EditorGUILayout.Space(8);

            if (currentTab == Tab.Composer)
                DrawManualComposerMode();
            else
                DrawManualSimpleMode();
        }

        private void DrawManualSimpleMode()
        {
            // Generate Prompt button
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(prompt));
            if (GUILayout.Button("Generate Prompt", GUILayout.Height(25)))
            {
                string fullPrompt = BuildFullPrompt();
                string sysPrompt = currentTab == Tab.Shader ? VFXPrompts.SHADER_NEW : VFXPrompts.PARTICLE_SYSTEM;

                if (hasGenerated && !string.IsNullOrWhiteSpace(refinementPrompt))
                {
                    string prev = currentTab == Tab.Shader ? lastShaderCode : lastParticleJson;
                    generatedPrompt = sysPrompt + "\n\n---\n\nPrevious " +
                        (currentTab == Tab.Shader ? "shader code" : "configuration") +
                        ":\n" + prev +
                        "\n\nRefinement request: " + refinementPrompt +
                        "\n\nReturn the complete updated " +
                        (currentTab == Tab.Shader ? "shader code" : "ParticleConfig JSON") + ".";
                }
                else
                {
                    generatedPrompt = sysPrompt + "\n\n---\n\n" +
                        (currentTab == Tab.Shader ? $"Shader name: {GetEffectName()}\n\nEffect description: " : "Effect description: ") +
                        fullPrompt;
                }
                AppendStatus("Prompt generated.");
            }
            EditorGUI.EndDisabledGroup();

            // Prompt display
            if (!string.IsNullOrEmpty(generatedPrompt))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Copy this prompt to Claude:", EditorStyles.boldLabel);
                promptScroll = EditorGUILayout.BeginScrollView(promptScroll, GUILayout.Height(120));
                EditorGUILayout.TextArea(generatedPrompt, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(20)))
                {
                    GUIUtility.systemCopyBuffer = generatedPrompt;
                    AppendStatus("Copied to clipboard.");
                }
            }

            // Paste response
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Paste Claude's {(currentTab == Tab.Shader ? "shader code" : "JSON response")}:", EditorStyles.boldLabel);
            pasteScroll = EditorGUILayout.BeginScrollView(pasteScroll, GUILayout.Height(120));
            pastedResponse = EditorGUILayout.TextArea(pastedResponse, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(pastedResponse));
            if (GUILayout.Button("Apply", GUILayout.Height(30)))
                ManualApplySimple();
            EditorGUI.EndDisabledGroup();
        }

        private void ManualApplySimple()
        {
            try
            {
                string name = GetEffectName();
                string response = ClaudeApiClient.StripMarkdownFencing(pastedResponse.Trim());

                if (currentTab == Tab.Shader)
                {
                    lastShaderCode = response;
                    lastOutputPreview = response;
                    ShaderUtils.SaveShaderAndCreateMaterial(response, name);
                    AddToHistory(response);
                    hasGenerated = true;
                    AppendStatus($"Applied: {SHADER_FOLDER}/{name}.shader");
                }
                else
                {
                    var config = JsonUtility.FromJson<ParticleConfig>(response);
                    lastParticleJson = JsonUtility.ToJson(config, true);
                    lastOutputPreview = lastParticleJson;
                    ApplyParticle(config, name, null);
                    RunQualityCheck(config);
                    AddToHistory(lastParticleJson);
                    hasGenerated = true;
                    AppendStatus($"Applied: {PREFAB_FOLDER}/{name}.prefab");
                }
            }
            catch (Exception e)
            {
                AppendStatus($"Error: {e.Message}");
            }
        }

        // --- Manual Composer (5-step wizard) ---

        private void DrawManualComposerMode()
        {
            // Progress indicator
            string[] stepNames = { "Describe", "Shader Prompt", "Paste Shader", "Particle Prompt", "Paste Particles" };
            int stepIndex = (int)composerStep;
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
            EditorGUI.ProgressBar(rect, (stepIndex + 1) / 5f, $"Step {stepIndex + 1}/5: {stepNames[stepIndex]}");
            EditorGUILayout.Space(4);

            switch (composerStep)
            {
                case ComposerStep.Describe:
                    DrawComposerDescribeStep();
                    break;
                case ComposerStep.ShaderPrompt:
                    DrawComposerPromptStep("shader");
                    break;
                case ComposerStep.ShaderPaste:
                    DrawComposerPasteStep("shader response (USE_EXISTING: ... or full shader code)", true);
                    break;
                case ComposerStep.ParticlePrompt:
                    DrawComposerPromptStep("particle config");
                    break;
                case ComposerStep.ParticlePaste:
                    DrawComposerPasteStep("particle config JSON", false);
                    break;
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Start Over", GUILayout.Height(20)))
            {
                composerStep = ComposerStep.Describe;
                generatedPrompt = "";
                pastedResponse = "";
                AppendStatus("Reset composer wizard.");
            }
        }

        private void DrawComposerDescribeStep()
        {
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(prompt));
            if (GUILayout.Button("Next: Generate Shader Prompt", GUILayout.Height(30)))
            {
                string name = GetEffectName();
                string fullPrompt = BuildFullPrompt();

                if (allowReuseShader)
                {
                    string existingShaders = ShaderUtils.ListValidShaders();
                    generatedPrompt = VFXPrompts.SHADER_PICK_OR_CREATE +
                        "\n\n---\n\nEffect description: " + fullPrompt +
                        "\nShader name (if new): " + name +
                        "\n\nExisting shaders in project:\n" + existingShaders;
                }
                else
                {
                    generatedPrompt = VFXPrompts.SHADER_NEW +
                        "\n\n---\n\nShader name: " + name +
                        "\n\nEffect description: " + fullPrompt;
                }

                pastedResponse = "";
                composerStep = ComposerStep.ShaderPrompt;
                AppendStatus("Shader prompt generated.");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawComposerPromptStep(string label)
        {
            EditorGUILayout.LabelField($"Copy this {label} prompt to Claude:", EditorStyles.boldLabel);
            promptScroll = EditorGUILayout.BeginScrollView(promptScroll, GUILayout.Height(180));
            EditorGUILayout.TextArea(generatedPrompt, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(25)))
            {
                GUIUtility.systemCopyBuffer = generatedPrompt;
                AppendStatus("Copied to clipboard.");
            }
            if (GUILayout.Button("Next: Paste Response", GUILayout.Height(25)))
            {
                pastedResponse = "";
                composerStep = composerStep == ComposerStep.ShaderPrompt
                    ? ComposerStep.ShaderPaste
                    : ComposerStep.ParticlePaste;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawComposerPasteStep(string label, bool isShaderStep)
        {
            EditorGUILayout.LabelField($"Paste Claude's {label}:", EditorStyles.boldLabel);
            pasteScroll = EditorGUILayout.BeginScrollView(pasteScroll, GUILayout.Height(180));
            pastedResponse = EditorGUILayout.TextArea(pastedResponse, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(pastedResponse));
            string buttonLabel = isShaderStep ? "Apply Shader & Next" : "Apply & Create Prefab";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(30)))
            {
                if (isShaderStep)
                    ComposerApplyShaderAndAdvance();
                else
                    ComposerApplyParticleAndFinish();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ComposerApplyShaderAndAdvance()
        {
            try
            {
                string name = GetEffectName();
                string response = ClaudeApiClient.StripMarkdownFencing(pastedResponse.Trim());
                composedMaterial = ProcessShaderResponse(response, name);

                // Generate particle prompt
                string shaderUsed = composedMaterial != null ? composedMaterial.shader.name : "Default-Particle";
                string fullPrompt = BuildFullPrompt();
                generatedPrompt = VFXPrompts.PARTICLE_SYSTEM +
                    "\n\n---\n\nEffect description: " + fullPrompt +
                    "\nShader being used: " + shaderUsed +
                    "\n\nGenerate a ParticleConfig that works well with this shader.";
                pastedResponse = "";
                composerStep = ComposerStep.ParticlePrompt;
                AppendStatus("Shader applied. Now generate particle config.");
            }
            catch (Exception e)
            {
                AppendStatus($"Error: {e.Message}");
            }
        }

        private void ComposerApplyParticleAndFinish()
        {
            try
            {
                string name = GetEffectName();
                string json = ClaudeApiClient.StripMarkdownFencing(pastedResponse.Trim());
                var config = JsonUtility.FromJson<ParticleConfig>(json);
                lastParticleJson = JsonUtility.ToJson(config, true);
                lastOutputPreview = lastParticleJson;

                ApplyParticle(config, name, composedMaterial);
                RunQualityCheck(config);
                AddToHistory(lastParticleJson);
                hasGenerated = true;
                AppendStatus($"Done! {PREFAB_FOLDER}/{name}.prefab");
            }
            catch (Exception e)
            {
                AppendStatus($"Error: {e.Message}");
            }
        }

        // =====================================================
        // SHARED LOGIC
        // =====================================================

        private string BuildFullPrompt()
        {
            if (foldHelpers && (category != VFXPromptBuilder.EffectCategory.Custom ||
                                intensity != VFXPromptBuilder.Intensity.Medium ||
                                helperGlow || helperLooping || helperTrails || useColorHint))
            {
                return VFXPromptBuilder.BuildParticlePrompt(
                    prompt, category, intensity,
                    helperGlow, helperLooping, helperTrails,
                    useColorHint ? (Color?)colorHint : null);
            }
            return prompt;
        }

        private void ApplyPreset(VFXPresets.Preset preset)
        {
            prompt = preset.prompt;
            category = preset.category;
            intensity = preset.intensity;
            helperGlow = preset.glow;
            helperLooping = preset.looping;
            helperTrails = preset.trails;
            foldHelpers = true;
            AppendStatus($"Preset loaded: {preset.name}");
        }

        private Material ProcessShaderResponse(string response, string name)
        {
            response = response.Trim();

            if (response.StartsWith("USE_EXISTING:"))
            {
                string existingName = response.Substring("USE_EXISTING:".Length).Trim();
                if (existingName.StartsWith("TomatoFighters/"))
                    existingName = existingName.Substring("TomatoFighters/".Length);

                string matPath = $"{SHADER_FOLDER}/{existingName}_Mat.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat != null)
                {
                    AppendStatus($"Using existing shader: {existingName}");
                    lastShaderCode = "";
                    return mat;
                }

                AppendStatus($"Existing shader \"{existingName}\" not found, creating fallback");
                var fallbackMat = ShaderUtils.SaveShaderAndCreateMaterial(null, name);
                AppendStatus($"Fallback shader created: {SHADER_FOLDER}/{name}.shader");
                return fallbackMat;
            }

            lastShaderCode = response;
            lastOutputPreview = response;
            var material = ShaderUtils.SaveShaderAndCreateMaterial(response, name);
            AppendStatus($"Shader created: {SHADER_FOLDER}/{name}.shader");
            return material;
        }

        private void ApplyParticle(ParticleConfig config, string name, Material material)
        {
            if (sceneInstance != null)
                DestroyImmediate(sceneInstance);

            var go = ParticleSystemApplier.Apply(config, name);

            if (material != null)
            {
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.material = material;
                    renderer.trailMaterial = material;
                }
            }

            EnsureFolder(PREFAB_FOLDER);
            string prefabPath = $"{PREFAB_FOLDER}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
            sceneInstance = go;

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        private void RunQualityCheck(ParticleConfig config)
        {
            qualityWarnings = VFXQualityChecker.Check(config);
        }

        private void AddToHistory(string output)
        {
            generationHistory.Insert(0, output);
            if (generationHistory.Count > 5)
                generationHistory.RemoveAt(5);
            selectedHistory = 0;
        }

        private void SetProgress(float value, string label)
        {
            progressValue = value;
            progressLabel = label;
        }

        private void ResetManualState()
        {
            composerStep = ComposerStep.Describe;
            generatedPrompt = "";
            pastedResponse = "";
        }

        private string GetEffectName()
        {
            if (!string.IsNullOrWhiteSpace(effectName))
                return effectName;

            string cleaned = Regex.Replace(prompt.Trim(), @"[^a-zA-Z0-9\s]", "");
            string[] words = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            int count = 0;
            foreach (string word in words)
            {
                if (word.Length > 0 && count < 5)
                {
                    sb.Append(char.ToUpper(word[0])).Append(word.Substring(1).ToLower());
                    count++;
                }
            }
            return sb.Length > 0 ? sb.ToString() : "GeneratedEffect";
        }

        private void AppendStatus(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            statusLog = $"[{timestamp}] {message}\n{statusLog}";
            Repaint();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // --- EditorPrefs persistence for foldouts ---

        private void SaveFoldoutStates()
        {
            EditorPrefs.SetBool(PREF_PREFIX + "foldDescription", foldDescription);
            EditorPrefs.SetBool(PREF_PREFIX + "foldPreset", foldPreset);
            EditorPrefs.SetBool(PREF_PREFIX + "foldHelpers", foldHelpers);
            EditorPrefs.SetBool(PREF_PREFIX + "foldShaderOptions", foldShaderOptions);
            EditorPrefs.SetBool(PREF_PREFIX + "foldRefinement", foldRefinement);
            EditorPrefs.SetBool(PREF_PREFIX + "foldOutput", foldOutput);
            EditorPrefs.SetBool(PREF_PREFIX + "foldQuality", foldQuality);
            EditorPrefs.SetBool(PREF_PREFIX + "foldStatus", foldStatus);
            EditorPrefs.SetBool(PREF_PREFIX + "foldApiSettings", foldApiSettings);
        }

        private void LoadFoldoutStates()
        {
            foldDescription = EditorPrefs.GetBool(PREF_PREFIX + "foldDescription", true);
            foldPreset = EditorPrefs.GetBool(PREF_PREFIX + "foldPreset", false);
            foldHelpers = EditorPrefs.GetBool(PREF_PREFIX + "foldHelpers", false);
            foldShaderOptions = EditorPrefs.GetBool(PREF_PREFIX + "foldShaderOptions", false);
            foldRefinement = EditorPrefs.GetBool(PREF_PREFIX + "foldRefinement", false);
            foldOutput = EditorPrefs.GetBool(PREF_PREFIX + "foldOutput", false);
            foldQuality = EditorPrefs.GetBool(PREF_PREFIX + "foldQuality", true);
            foldStatus = EditorPrefs.GetBool(PREF_PREFIX + "foldStatus", true);
            foldApiSettings = EditorPrefs.GetBool(PREF_PREFIX + "foldApiSettings", false);
        }
    }
}
