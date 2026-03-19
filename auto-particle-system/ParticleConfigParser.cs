using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TomatoFighters.Editor.VFX
{
    /// <summary>
    /// Custom JSON parser for ParticleConfig that handles recursive sub-emitter nesting.
    /// JsonUtility cannot deserialize ParticleConfig directly because
    /// ParticleConfig → SubEmitterEntry → ParticleConfig is a recursive type
    /// that exceeds the serialization depth limit.
    /// </summary>
    public static class ParticleConfigParser
    {
        /// <summary>
        /// Deserialize a ParticleConfig from JSON, handling sub-emitters manually.
        /// </summary>
        public static ParticleConfig Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return new ParticleConfig();

            // JsonUtility handles all fields except subEmitters (marked NonSerialized)
            var config = JsonUtility.FromJson<ParticleConfig>(json);

            // Manually parse subEmitters from the raw JSON
            int keyIdx = FindKeyIndex(json, "subEmitters");
            if (keyIdx < 0) return config;

            int arrStart = json.IndexOf('[', keyIdx);
            if (arrStart < 0) return config;

            int arrEnd = FindMatchingBracket(json, arrStart, '[', ']');
            if (arrEnd < 0) return config;

            var entries = ExtractTopLevelObjects(json, arrStart + 1, arrEnd);
            if (entries.Count == 0) return config;

            config.subEmitters = new SubEmitterEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                string entryJson = entries[i];
                config.subEmitters[i] = new SubEmitterEntry
                {
                    trigger = ExtractStringValue(entryJson, "trigger") ?? "Birth",
                    inheritProperties = ExtractStringValue(entryJson, "inheritProperties") ?? "Nothing",
                    emitProbability = ExtractFloatValue(entryJson, "emitProbability", 1f)
                };

                // Recursively parse the nested config object
                int configKey = FindKeyIndex(entryJson, "config");
                if (configKey >= 0)
                {
                    int objStart = entryJson.IndexOf('{', configKey);
                    if (objStart >= 0)
                    {
                        int objEnd = FindMatchingBracket(entryJson, objStart, '{', '}');
                        if (objEnd >= 0)
                        {
                            string nestedJson = entryJson.Substring(objStart, objEnd - objStart + 1);
                            config.subEmitters[i].config = Parse(nestedJson);
                        }
                    }
                }
            }

            return config;
        }

        /// <summary>
        /// Serialize a ParticleConfig to JSON, including sub-emitters that
        /// JsonUtility skips due to [NonSerialized].
        /// </summary>
        public static string ToJson(ParticleConfig config, bool prettyPrint = false)
        {
            string json = JsonUtility.ToJson(config, prettyPrint);

            if (config.subEmitters == null || config.subEmitters.Length == 0)
                return json;

            // Build subEmitters JSON and insert before the closing brace
            var sb = new System.Text.StringBuilder();
            string indent = prettyPrint ? "    " : "";
            string nl = prettyPrint ? "\n" : "";

            sb.Append(",");
            sb.Append(nl);
            sb.Append(indent);
            sb.Append("\"subEmitters\": [");

            for (int i = 0; i < config.subEmitters.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(nl);
                sb.Append(indent).Append(indent);
                sb.Append("{");
                sb.Append(nl);

                var entry = config.subEmitters[i];
                string innerIndent = indent + indent + indent;

                sb.Append(innerIndent);
                sb.AppendFormat("\"trigger\": \"{0}\",", entry.trigger ?? "Birth");
                sb.Append(nl);

                sb.Append(innerIndent);
                sb.AppendFormat("\"inheritProperties\": \"{0}\",", entry.inheritProperties ?? "Nothing");
                sb.Append(nl);

                sb.Append(innerIndent);
                sb.AppendFormat(CultureInfo.InvariantCulture, "\"emitProbability\": {0},", entry.emitProbability);
                sb.Append(nl);

                sb.Append(innerIndent);
                sb.Append("\"config\": ");
                if (entry.config != null)
                    sb.Append(ToJson(entry.config, prettyPrint));
                else
                    sb.Append("null");
                sb.Append(nl);

                sb.Append(indent).Append(indent);
                sb.Append("}");
            }

            sb.Append(nl);
            sb.Append(indent);
            sb.Append("]");

            int lastBrace = json.LastIndexOf('}');
            return json.Substring(0, lastBrace) + sb.ToString() + nl + "}";
        }

        // --- JSON helpers ---

        private static int FindKeyIndex(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            bool inString = false;
            bool escape = false;

            for (int i = 0; i <= json.Length - pattern.Length; i++)
            {
                char c = json[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"' && !inString)
                {
                    // Check if this starts our pattern
                    if (json.Substring(i, pattern.Length) == pattern)
                    {
                        // Verify it's followed by a colon (it's a key, not a value)
                        int after = i + pattern.Length;
                        while (after < json.Length && char.IsWhiteSpace(json[after])) after++;
                        if (after < json.Length && json[after] == ':')
                            return after;
                    }
                    // Skip past the closing quote of whatever string this is
                    int end = json.IndexOf('"', i + 1);
                    if (end >= 0) i = end;
                    continue;
                }
            }
            return -1;
        }

        private static int FindMatchingBracket(string s, int openIdx, char open, char close)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = openIdx; i < s.Length; i++)
            {
                char c = s[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == open) depth++;
                if (c == close) { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static List<string> ExtractTopLevelObjects(string json, int start, int end)
        {
            var objects = new List<string>();
            int i = start;
            while (i < end)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart < 0 || objStart >= end) break;

                int objEnd = FindMatchingBracket(json, objStart, '{', '}');
                if (objEnd < 0 || objEnd > end) break;

                objects.Add(json.Substring(objStart, objEnd - objStart + 1));
                i = objEnd + 1;
            }
            return objects;
        }

        private static string ExtractStringValue(string json, string key)
        {
            int keyIdx = FindKeyIndex(json, key);
            if (keyIdx < 0) return null;

            // Find opening quote of the value
            int quoteStart = json.IndexOf('"', keyIdx + 1);
            if (quoteStart < 0) return null;

            // Find closing quote (handle escaped quotes)
            int pos = quoteStart + 1;
            while (pos < json.Length)
            {
                if (json[pos] == '\\') { pos += 2; continue; }
                if (json[pos] == '"') break;
                pos++;
            }
            if (pos >= json.Length) return null;

            return json.Substring(quoteStart + 1, pos - quoteStart - 1);
        }

        private static float ExtractFloatValue(string json, string key, float defaultValue)
        {
            int keyIdx = FindKeyIndex(json, key);
            if (keyIdx < 0) return defaultValue;

            int start = keyIdx + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            int numEnd = start;
            while (numEnd < json.Length && (char.IsDigit(json[numEnd]) || json[numEnd] == '.' || json[numEnd] == '-' || json[numEnd] == 'e' || json[numEnd] == 'E' || json[numEnd] == '+'))
                numEnd++;

            if (numEnd > start && float.TryParse(
                    json.Substring(start, numEnd - start),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                return val;

            return defaultValue;
        }
    }
}
