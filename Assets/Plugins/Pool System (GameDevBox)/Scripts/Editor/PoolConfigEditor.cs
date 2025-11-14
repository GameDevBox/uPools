// =======================================================
//  GameDevBox – YouTube
//  Author: Arian
//  Link: https://www.youtube.com/@GameDevBox
// =======================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PoolConfig))]
public class PoolConfigEditor : Editor
{
    private SerializedProperty poolKeyProperty;
    private SerializedProperty prefabsProperty;
    private SerializedProperty initialPoolSizeProperty;
    private SerializedProperty maxPoolSizeProperty;
    private SerializedProperty prewarmOnStartProperty;
    private SerializedProperty logPoolActivityProperty;
    private SerializedProperty poolKeysPathProperty;
    private SerializedProperty poolCategoryProperty;
    private SerializedProperty instantiationMode;
    private SerializedProperty prefabWeights;
    private SerializedProperty overflowBehavior;


    private string[] existingPoolKeys;
    private string[] poolKeysInFile;
    private bool showValidation = true;
    private bool showQuickActions = true;
    private Vector2 validationScrollPos;

    private void OnEnable()
    {
        poolKeyProperty = serializedObject.FindProperty("poolKey");
        poolCategoryProperty = serializedObject.FindProperty("poolCategory");
        prefabsProperty = serializedObject.FindProperty("prefabs");
        initialPoolSizeProperty = serializedObject.FindProperty("initialPoolSize");
        maxPoolSizeProperty = serializedObject.FindProperty("maxPoolSize");
        prewarmOnStartProperty = serializedObject.FindProperty("prewarmOnStart");
        logPoolActivityProperty = serializedObject.FindProperty("logPoolActivity");
        poolKeysPathProperty = serializedObject.FindProperty("poolKeysPath");
        instantiationMode = serializedObject.FindProperty("instantiationMode");
        prefabWeights = serializedObject.FindProperty("prefabWeights");
        overflowBehavior = serializedObject.FindProperty("overflowBehavior");

        EnsureDefaultPoolKeysPath();

        RefreshExistingPoolKeys();
        RefreshPoolKeysInFile();
    }

    private void EnsureDefaultPoolKeysPath()
    {
        string currentPath = poolKeysPathProperty.stringValue;

        if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
            return;

        string[] guids = AssetDatabase.FindAssets("PoolKeys t:Script");

        if (guids.Length > 0)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            poolKeysPathProperty.stringValue = assetPath;
            serializedObject.ApplyModifiedProperties();
            return;
        }

        Debug.LogWarning("PoolKeys.cs not found! Please ensure it exists under Assets/");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        DrawPoolSettings();
        EditorGUILayout.Space();
        DrawAdvancedSettings();
        EditorGUILayout.Space();
        DrawValidationSection();
        EditorGUILayout.Space();
        DrawQuickActions();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPoolSettings()
    {
        EditorGUILayout.LabelField("Pool Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Pool Key", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(poolKeyProperty, GUIContent.none);

        if (GUILayout.Button("Refresh", GUILayout.Width(60)))
        {
            RefreshExistingPoolKeys();
            RefreshPoolKeysInFile();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        string currentKey = poolKeyProperty.stringValue;
        if (!string.IsNullOrEmpty(currentKey))
        {
            if (IsDuplicateKey(currentKey))
            {
                EditorGUILayout.HelpBox("⚠️ This key is used by another PoolConfig", MessageType.Warning);
            }
            else if (!IsValidCSharpIdentifier(currentKey))
            {
                EditorGUILayout.HelpBox("⚠️ Key must be a valid C# identifier (letters, numbers, underscore, no spaces)", MessageType.Warning);
            }
            else if (!IsKeyInPoolKeysFile(currentKey))
            {
                EditorGUILayout.HelpBox("⚠️ Key not found in PoolKeys.cs - Use 'Add Pool Key to PoolKeys' button below", MessageType.Warning);
            }
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(poolCategoryProperty);

        EditorGUILayout.PropertyField(overflowBehavior);
        EditorGUILayout.Space();

        // ========== ADD TRANSFORM SETTINGS HERE ==========
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Transform Settings", EditorStyles.boldLabel);

        // Get the transform properties
        SerializedProperty transformResetMode = serializedObject.FindProperty("transformResetMode");
        SerializedProperty defaultPosition = serializedObject.FindProperty("defaultPosition");
        SerializedProperty defaultRotation = serializedObject.FindProperty("defaultRotation");
        SerializedProperty defaultScale = serializedObject.FindProperty("defaultScale");

        EditorGUILayout.PropertyField(transformResetMode);

        // Show custom defaults only when that mode is selected
        if ((TransformResetMode)transformResetMode.enumValueIndex == TransformResetMode.UseCustomDefaults)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Custom Transform Defaults", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(defaultPosition);
            EditorGUILayout.PropertyField(defaultRotation);
            EditorGUILayout.PropertyField(defaultScale);

            EditorGUILayout.EndVertical();
        }

        // Add help box explaining the modes
        switch ((TransformResetMode)transformResetMode.enumValueIndex)
        {
            case TransformResetMode.UsePrefabDefaults:
                EditorGUILayout.HelpBox("Objects will reset to their original prefab transform values", MessageType.Info);
                break;
            case TransformResetMode.UseCustomDefaults:
                EditorGUILayout.HelpBox("Objects will reset to the custom values defined above", MessageType.Info);
                break;
            case TransformResetMode.UseProvidedValues:
                EditorGUILayout.HelpBox("Objects will use position/rotation/scale provided when calling Get()", MessageType.Info);
                break;
            case TransformResetMode.KeepCurrent:
                EditorGUILayout.HelpBox("Objects will keep their current transform (useful for animated objects)", MessageType.Info);
                break;
        }
        // ========== END TRANSFORM SETTINGS ==========

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(instantiationMode);

        var instantiationModeProp = instantiationMode;
        var prefabsproperty = prefabsProperty;
        var weightsProperty = prefabWeights;

        var config = (PoolConfig)target;
        config.SyncWeightsWithPrefabs();
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();

        if ((InstantiationMode)instantiationModeProp.enumValueIndex == InstantiationMode.WeightedRandom)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefab Weights", EditorStyles.boldLabel);

            if (prefabsproperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No prefabs assigned. Add prefabs to set weights.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Adjust weights between 0 (never selected) and 1 (highest probability).", MessageType.Info);

                for (int i = 0; i < prefabsproperty.arraySize; i++)
                {
                    var prefabProperty = prefabsproperty.GetArrayElementAtIndex(i);
                    var weightProperty = weightsProperty.GetArrayElementAtIndex(i);

                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.BeginHorizontal();

                    GameObject prefab = prefabProperty.objectReferenceValue as GameObject;
                    Texture2D prefabIcon = AssetPreview.GetMiniThumbnail(prefab);
                    string prefabName = prefab != null ? prefab.name : "Null Prefab";

                    GUIContent prefabLabel = new GUIContent($" {prefabName}", prefabIcon);
                    EditorGUILayout.LabelField(prefabLabel, GUILayout.Width(150));

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();

                    float weightValue = weightProperty.floatValue;
                    EditorGUILayout.LabelField("Weight:", GUILayout.Width(50));

                    float newWeight = EditorGUILayout.Slider(weightValue, 0f, 1f);

                    if (Mathf.Abs(newWeight - weightValue) > 0.001f)
                    {
                        weightProperty.floatValue = newWeight;
                    }

                    EditorGUILayout.EndHorizontal();

                    float normalizedPercent = config.GetNormalizedWeight(i) * 100f;
                    EditorGUILayout.BeginHorizontal();

                    Rect rect = GUILayoutUtility.GetRect(100, 16);
                    EditorGUI.ProgressBar(rect, normalizedPercent / 100f, $"{normalizedPercent:F1}% chance");

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }

                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField("Weight Summary", EditorStyles.miniBoldLabel);

                float totalWeight = config.GetTotalWeight();
                EditorGUILayout.LabelField($"Total Raw Weight: {totalWeight:F2}", EditorStyles.miniLabel);

                if (totalWeight <= 0)
                {
                    EditorGUILayout.HelpBox("Warning: All weights are zero! No prefabs will be selected.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Actual Probabilities:", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < prefabsproperty.arraySize; i++)
                    {
                        var prefabProperty = prefabsproperty.GetArrayElementAtIndex(i);
                        string prefabName = prefabProperty.objectReferenceValue ? prefabProperty.objectReferenceValue.name : "Null Prefab";
                        float probability = config.GetNormalizedWeight(i) * 100f;
                        EditorGUILayout.LabelField($"• {prefabName}: {probability:F1}%", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Equal Weights", EditorStyles.miniButton))
                {
                    SetEqualWeights(config);
                }
                if (GUILayout.Button("Reset All", EditorStyles.miniButton))
                {
                    ResetAllWeights(config);
                }
                if (GUILayout.Button("Normalize", EditorStyles.miniButton))
                {
                    NormalizeWeights(config);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(prefabsproperty, true);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(initialPoolSizeProperty, GUILayout.MinWidth(120));
        EditorGUILayout.PropertyField(maxPoolSizeProperty, GUILayout.MinWidth(120));
        EditorGUILayout.EndHorizontal();

        if (initialPoolSizeProperty.intValue <= 0)
        {
            EditorGUILayout.HelpBox("Initial pool size must be greater than 0.", MessageType.Error);
        }
        if (maxPoolSizeProperty.intValue < initialPoolSizeProperty.intValue)
        {
            EditorGUILayout.HelpBox("Max pool size should be greater than or equal to initial size.", MessageType.Warning);
        }
    }

    private void SetEqualWeights(PoolConfig config)
    {
        if (config.prefabWeights == null || config.prefabWeights.Length == 0) return;

        float equalWeight = 1f / config.prefabWeights.Length;
        for (int i = 0; i < config.prefabWeights.Length; i++)
        {
            config.prefabWeights[i] = equalWeight;
        }
        EditorUtility.SetDirty(config);
    }

    private void ResetAllWeights(PoolConfig config)
    {
        if (config.prefabWeights == null) return;

        for (int i = 0; i < config.prefabWeights.Length; i++)
        {
            config.prefabWeights[i] = 1f;
        }
        EditorUtility.SetDirty(config);
    }

    private void NormalizeWeights(PoolConfig config)
    {
        if (config.prefabWeights == null || config.prefabWeights.Length == 0) return;

        float total = config.GetTotalWeight();
        if (total <= 0) return;

        for (int i = 0; i < config.prefabWeights.Length; i++)
        {
            config.prefabWeights[i] = config.prefabWeights[i] / total;
        }
        EditorUtility.SetDirty(config);
    }

    private void DrawQuickActions()
    {
        showQuickActions = EditorGUILayout.Foldout(showQuickActions, "Quick Actions", true);
        if (!showQuickActions) return;

        EditorGUILayout.BeginVertical(GUI.skin.box);

        string currentKey = poolKeyProperty.stringValue;
        bool canAddToPoolKeys = !string.IsNullOrEmpty(currentKey) &&
                               IsValidCSharpIdentifier(currentKey) &&
                               !IsKeyInPoolKeysFile(currentKey);

        EditorGUI.BeginDisabledGroup(!canAddToPoolKeys);
        if (GUILayout.Button("Add Pool Key to PoolKeys"))
        {
            AddKeyToPoolKeys();
        }
        EditorGUI.EndDisabledGroup();

        if (!canAddToPoolKeys && !string.IsNullOrEmpty(currentKey))
        {
            if (IsKeyInPoolKeysFile(currentKey))
            {
                EditorGUILayout.HelpBox("✓ Key already exists in PoolKeys", MessageType.Info);
            }
            else if (!IsValidCSharpIdentifier(currentKey))
            {
                EditorGUILayout.HelpBox("Key must be valid before adding", MessageType.Warning);
            }
        }

        if (GUILayout.Button("Show Existing Pool Keys"))
        {
            ShowExistingPoolKeys();
        }

        if (GUILayout.Button("Validate All Configs"))
        {
            ValidateAllPoolConfigs();
        }

        EditorGUILayout.EndVertical();
    }

    private void AddKeyToPoolKeys()
    {
        var config = (PoolConfig)target;
        string poolKey = config.poolKey?.Trim();
        string poolKeysPath = GetPoolKeysPath();

        // Validate filename
        string fileName = Path.GetFileNameWithoutExtension(poolKeysPath);
        if (fileName != "PoolKeys")
        {
            EditorUtility.DisplayDialog("Invalid File", "The target file must be named 'PoolKeys.cs'", "OK");
            return;
        }

        if (string.IsNullOrEmpty(poolKey))
        {
            EditorUtility.DisplayDialog("Error", "Pool Key is empty!", "OK");
            return;
        }

        if (!IsValidCSharpIdentifier(poolKey))
        {
            EditorUtility.DisplayDialog("Error", "Pool Key must be a valid C# identifier (letters, numbers, underscore, no spaces)!", "OK");
            return;
        }

        if (!File.Exists(poolKeysPath))
        {
            EditorUtility.DisplayDialog("Error", $"PoolKeys.cs not found at:\n{poolKeysPath}", "OK");
            return;
        }

        var lines = File.ReadAllLines(poolKeysPath).ToList();

        if (IsKeyInPoolKeysFile(poolKey))
        {
            EditorUtility.DisplayDialog("Duplicate", $"Key '{poolKey}' already exists in PoolKeys!", "OK");
            return;
        }

        int classStartIndex = lines.FindIndex(l => l.Contains("public static class PoolKeys"));
        if (classStartIndex < 0)
        {
            EditorUtility.DisplayDialog("Error", "PoolKeys class not found!", "OK");
            return;
        }

        // Find the class opening brace
        int classBraceIndex = -1;
        for (int i = classStartIndex; i < Mathf.Min(classStartIndex + 5, lines.Count); i++)
        {
            if (lines[i].Contains("{"))
            {
                classBraceIndex = i;
                break;
            }
        }

        if (classBraceIndex < 0)
        {
            EditorUtility.DisplayDialog("Error", "Could not find class opening brace!", "OK");
            return;
        }

        int insertIndex = FindBestInsertionPoint(lines, classBraceIndex, poolKey);

        if (insertIndex < 0)
        {
            EditorUtility.DisplayDialog("Error", "Could not find suitable insertion point!", "OK");
            return;
        }

        string newLine = $"    public const string {poolKey} = \"{poolKey}\";";
        lines.Insert(insertIndex, newLine);

        EnsureProperSpacing(lines, insertIndex);

        try
        {
            File.WriteAllLines(poolKeysPath, lines);
            AssetDatabase.Refresh();
            RefreshPoolKeysInFile();
            EditorUtility.DisplayDialog("Success", $"Key '{poolKey}' added to PoolKeys!", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to write file: {e.Message}", "OK");
        }
    }

    private int FindBestInsertionPoint(List<string> lines, int classBraceIndex, string newKey)
    {
        var existingKeys = new List<(int index, string key)>();

        for (int i = classBraceIndex + 1; i < lines.Count; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("public const string"))
            {
                // Extract the key name
                var parts = line.Split(new[] { ' ', '=' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    string key = parts[3];
                    existingKeys.Add((i, key));
                }
            }
            else if (line == "}" && !IsInComment(lines, i))
            {
                break;
            }
        }

        if (existingKeys.Count > 0)
        {
            for (int i = 0; i < existingKeys.Count; i++)
            {
                if (string.Compare(newKey, existingKeys[i].key, System.StringComparison.Ordinal) < 0)
                {
                    return existingKeys[i].index;
                }
            }
            return existingKeys[existingKeys.Count - 1].index + 1;
        }

        return classBraceIndex + 1;
    }

    private void EnsureProperSpacing(List<string> lines, int insertIndex)
    {
        if (insertIndex > 0 && !string.IsNullOrEmpty(lines[insertIndex - 1].Trim()))
        {
            string prevLine = lines[insertIndex - 1].Trim();
            if (!prevLine.StartsWith("//") && !string.IsNullOrEmpty(prevLine))
            {
                lines.Insert(insertIndex, "    ");
                insertIndex++;
            }
        }

        if (insertIndex < lines.Count - 1 && !string.IsNullOrEmpty(lines[insertIndex + 1].Trim()))
        {
            string nextLine = lines[insertIndex + 1].Trim();
            if (nextLine != "}" && !nextLine.StartsWith("//") && !string.IsNullOrEmpty(nextLine))
            {
                lines.Insert(insertIndex + 1, "    ");
            }
        }
    }

    private string GetPoolKeysPath()
    {
        string path = poolKeysPathProperty.stringValue;

        if (!File.Exists(path))
        {
            EnsureDefaultPoolKeysPath();
            path = poolKeysPathProperty.stringValue;
        }

        return path;
    }

    private void ShowExistingPoolKeys()
    {
        string poolKeysPath = GetPoolKeysPath();

        if (!File.Exists(poolKeysPath))
        {
            EditorUtility.DisplayDialog("Error", $"PoolKeys.cs not found at:\n{poolKeysPath}", "OK");
            return;
        }

        RefreshPoolKeysInFile();

        if (poolKeysInFile.Length == 0)
        {
            EditorUtility.DisplayDialog("Pool Keys", "No pool keys found in file!", "OK");
            return;
        }

        string message = $"Found {poolKeysInFile.Length} pool keys in PoolKeys.cs:\n\n• " + string.Join("\n• ", poolKeysInFile);
        EditorUtility.DisplayDialog("Pool Keys", message, "OK");
    }

    private void ValidateAllPoolConfigs()
    {
        var allConfigs = Resources.FindObjectsOfTypeAll<PoolConfig>();
        var issues = new List<string>();
        var warnings = new List<string>();
        var keyCounts = new Dictionary<string, int>();

        RefreshPoolKeysInFile();

        foreach (var config in allConfigs)
        {
            if (!string.IsNullOrEmpty(config.poolKey))
            {
                if (keyCounts.ContainsKey(config.poolKey))
                    keyCounts[config.poolKey]++;
                else
                    keyCounts[config.poolKey] = 1;
            }

            if (string.IsNullOrEmpty(config.poolKey))
            {
                issues.Add($"{config.name}: Missing Pool Key");
            }
            else
            {
                if (!IsValidCSharpIdentifier(config.poolKey))
                {
                    issues.Add($"{config.name}: Invalid Pool Key '{config.poolKey}'");
                }

                if (!IsKeyInPoolKeysFile(config.poolKey))
                {
                    warnings.Add($"{config.name}: Key '{config.poolKey}' not found in PoolKeys.cs");
                }
            }

            if (config.prefabs == null || config.prefabs.Length == 0)
            {
                issues.Add($"{config.name}: No prefabs assigned");
            }
            else if (config.prefabs.Any(p => p == null))
            {
                warnings.Add($"{config.name}: Has null prefab references");
            }

            if (config.initialPoolSize <= 0)
            {
                issues.Add($"{config.name}: Invalid initial pool size ({config.initialPoolSize})");
            }

            if (config.maxPoolSize < config.initialPoolSize)
            {
                warnings.Add($"{config.name}: Max pool size ({config.maxPoolSize}) < Initial size ({config.initialPoolSize})");
            }
        }

        // Add duplicate key issues
        foreach (var kvp in keyCounts.Where(kvp => kvp.Value > 1))
        {
            issues.Add($"Duplicate key '{kvp.Key}' used by {kvp.Value} configs");
        }

        if (issues.Count == 0 && warnings.Count == 0)
        {
            EditorUtility.DisplayDialog("Validation", $"All {allConfigs.Length} PoolConfigs are valid!", "OK");
        }
        else
        {
            string message = $"Found {issues.Count + warnings.Count} issues in {allConfigs.Length} configs:\n\n";

            if (issues.Count > 0)
            {
                message += "❌ ERRORS:\n• " + string.Join("\n• ", issues) + "\n\n";
            }

            if (warnings.Count > 0)
            {
                message += "⚠️ WARNINGS:\n• " + string.Join("\n• ", warnings);
            }

            EditorUtility.DisplayDialog("Validation Results", message, "OK");
        }
    }

    private void DrawAdvancedSettings()
    {
        EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(prewarmOnStartProperty);
        EditorGUILayout.PropertyField(logPoolActivityProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(poolKeysPathProperty, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFilePanel("Select PoolKeys.cs", "Assets", "cs");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }

                // Validate filename
                string fileName = Path.GetFileNameWithoutExtension(selectedPath);
                if (fileName != "PoolKeys")
                {
                    EditorUtility.DisplayDialog("Invalid File", "The selected file must be named 'PoolKeys.cs'", "OK");
                }
                else
                {
                    poolKeysPathProperty.stringValue = selectedPath;
                    serializedObject.ApplyModifiedProperties();
                    RefreshPoolKeysInFile();
                }
            }
        }
        EditorGUILayout.EndHorizontal();


        string currentPath = poolKeysPathProperty.stringValue;
        if (!string.IsNullOrEmpty(currentPath))
        {
            string fileName = Path.GetFileNameWithoutExtension(currentPath);
            if (fileName != "PoolKeys")
            {
                EditorGUILayout.HelpBox("File must be named 'PoolKeys.cs'", MessageType.Error);
            }
            else if (!File.Exists(currentPath))
            {
                EditorGUILayout.HelpBox("File does not exist at the specified path", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("✓ Valid PoolKeys.cs file", MessageType.Info);
            }
        }

        if (GUILayout.Button("Reset to Default Path"))
        {
            poolKeysPathProperty.stringValue = "Assets/_Content/_Script/Runtime/Others/PoolKeys.cs";
            serializedObject.ApplyModifiedProperties();
            RefreshPoolKeysInFile();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawValidationSection()
    {
        showValidation = EditorGUILayout.Foldout(showValidation, "Validation", true);
        if (!showValidation) return;

        EditorGUILayout.BeginVertical(GUI.skin.box);

        var config = (PoolConfig)target;
        var issues = new List<string>();
        var warnings = new List<string>();

        // Pool Key validation
        if (string.IsNullOrEmpty(config.poolKey))
        {
            issues.Add("Pool Key is required");
        }
        else
        {
            if (!IsValidCSharpIdentifier(config.poolKey))
            {
                issues.Add("Pool Key must be a valid C# identifier");
            }

            if (IsDuplicateKey(config.poolKey))
            {
                warnings.Add("This Pool Key is used by another PoolConfig");
            }

            if (!IsKeyInPoolKeysFile(config.poolKey))
            {
                warnings.Add("Key not found in PoolKeys.cs - Use 'Add Pool Key to PoolKeys' button");
            }
        }

        // Prefabs validation
        if (config.prefabs == null || config.prefabs.Length == 0)
        {
            issues.Add("At least one prefab is required");
        }
        else if (config.prefabs.Any(p => p == null))
        {
            warnings.Add("Some prefab references are null");
        }

        // Pool size validation
        if (config.initialPoolSize <= 0)
        {
            issues.Add("Initial pool size must be greater than 0");
        }
        if (config.maxPoolSize < config.initialPoolSize)
        {
            warnings.Add("Max pool size is less than initial size");
        }

        // Display results
        if (issues.Count == 0 && warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("✅ Configuration is valid", MessageType.Info);
        }
        else
        {
            validationScrollPos = EditorGUILayout.BeginScrollView(validationScrollPos, GUILayout.Height(Mathf.Min((issues.Count + warnings.Count) * 40, 200)));

            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox($"❌ {issue}", MessageType.Error);
            }

            foreach (var warning in warnings)
            {
                EditorGUILayout.HelpBox($"⚠️ {warning}", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private void RefreshExistingPoolKeys()
    {
        var allConfigs = Resources.FindObjectsOfTypeAll<PoolConfig>();
        var keys = new List<string>();

        foreach (var config in allConfigs)
        {
            if (config != null && config != target && !string.IsNullOrEmpty(config.poolKey))
            {
                keys.Add(config.poolKey);
            }
        }

        existingPoolKeys = keys.Distinct().OrderBy(k => k).ToArray();
    }

    private void RefreshPoolKeysInFile()
    {
        string poolKeysPath = GetPoolKeysPath();

        if (!File.Exists(poolKeysPath))
        {
            poolKeysInFile = new string[0];
            return;
        }

        try
        {
            var lines = File.ReadAllLines(poolKeysPath);
            poolKeysInFile = lines
                .Where(l => l.Trim().StartsWith("public const string"))
                .Select(l =>
                {
                    var parts = l.Trim().Split(new[] { ' ', '=' }, System.StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 4 ? parts[3] : null;
                })
                .Where(k => k != null && IsValidCSharpIdentifier(k))
                .ToArray();
        }
        catch
        {
            poolKeysInFile = new string[0];
        }
    }

    private bool IsDuplicateKey(string key)
    {
        return existingPoolKeys.Contains(key);
    }

    private bool IsKeyInPoolKeysFile(string key)
    {
        return poolKeysInFile.Contains(key);
    }

    private bool IsValidCSharpIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return false;
        if (!char.IsLetter(identifier[0]) && identifier[0] != '_') return false;

        return identifier.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private bool IsInComment(List<string> lines, int lineIndex)
    {
        string line = lines[lineIndex];
        int commentIndex = line.IndexOf("//");
        int braceIndex = line.IndexOf("}");

        return commentIndex >= 0 && commentIndex < braceIndex;
    }
}
#endif