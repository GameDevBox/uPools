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

[CustomEditor(typeof(PoolSystemManager))]
public class PoolSystemManagerEditor : Editor
{
    private SerializedProperty poolConfigsProp;
    private SerializedProperty poolParentProp;
    private SerializedProperty groupPoolsByCategoryProp;
    private SerializedProperty defaultCategoryNameProp;

    private string searchText = "";
    private Vector2 scrollPosition;
    private bool showConfigList = true;
    private bool showCategoryView = true;
    private string[] poolKeysInFile;
    private Dictionary<string, List<PoolConfig>> categorizedPools;

    private void OnEnable()
    {
        poolConfigsProp = serializedObject.FindProperty("poolConfigs");
        poolParentProp = serializedObject.FindProperty("poolParent");
        groupPoolsByCategoryProp = serializedObject.FindProperty("groupPoolsByCategory");
        defaultCategoryNameProp = serializedObject.FindProperty("defaultCategoryName");

        RefreshPoolKeysCache();
        CategorizePools();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();

        DrawPoolParentSettings();

        EditorGUILayout.Space();

        DrawConfigListSection();

        EditorGUILayout.Space();

        DrawSearchAndActions();

        EditorGUILayout.Space();

        DrawCategoryOverview();

        EditorGUILayout.Space();

        DrawPoolConfigurationsOverview();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPoolParentSettings()
    {
        EditorGUILayout.LabelField("Pool Parent Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.PropertyField(poolParentProp);
        EditorGUILayout.PropertyField(groupPoolsByCategoryProp);

        if (groupPoolsByCategoryProp.boolValue)
        {
            EditorGUILayout.PropertyField(defaultCategoryNameProp);

            var configs = GetAllPoolConfigs().ToList();
            var categories = configs.Select(c => GetPoolCategory(c)).Distinct().OrderBy(c => c).ToList();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Categories: {categories.Count}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"Pools: {configs.Count}", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (categories.Count > 0)
            {
                EditorGUILayout.HelpBox($"Categories: {string.Join(", ", categories)}", MessageType.Info);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawConfigListSection()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        showConfigList = EditorGUILayout.Foldout(showConfigList, "Pool Configurations List", true);

        if (showConfigList)
        {
            EditorGUILayout.HelpBox("Drag and drop PoolConfig assets here to register them with the manager.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(poolConfigsProp, true);

            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            if (GUILayout.Button("Refresh List"))
            {
                RefreshConfigList();
            }
            if (GUILayout.Button("Clean Nulls"))
            {
                RemoveNullConfigs();
            }
            if (GUILayout.Button("Refresh Keys"))
            {
                RefreshPoolKeysCache();
                CategorizePools();
                Repaint();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSearchAndActions()
    {
        EditorGUILayout.BeginHorizontal();
        searchText = EditorGUILayout.TextField("Search Pools", searchText);
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            searchText = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create New Pool"))
        {
            CreateNewPoolConfig();
        }

        if (GUILayout.Button("Sort A-Z"))
        {
            SortPoolConfigsAlphabetically();
        }

        if (GUILayout.Button("Sort by Category"))
        {
            SortPoolConfigsByCategory();
        }

        if (GUILayout.Button("Refresh View"))
        {
            RefreshPoolKeysCache();
            CategorizePools();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();

        var configs = GetAllPoolConfigs().ToList();
        var readyCount = configs.Count(c => GetConfigStatus(c) == "Ready");
        var warningCount = configs.Count(c => GetConfigStatus(c) == "Warning");
        var errorCount = configs.Count(c => GetConfigStatus(c).StartsWith("Error"));
        var categoryCount = configs.Select(c => GetPoolCategory(c)).Distinct().Count();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Total: {configs.Count}", GUILayout.Width(70));
        EditorGUILayout.LabelField($"Categories: {categoryCount}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"Ready: {readyCount}", GUILayout.Width(70));
        if (warningCount > 0)
        {
            EditorGUILayout.LabelField($"Warnings: {warningCount}", GetStatusStyle("Warning"), GUILayout.Width(80));
        }
        if (errorCount > 0)
        {
            EditorGUILayout.LabelField($"Errors: {errorCount}", GetStatusStyle("Error"), GUILayout.Width(70));
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCategoryOverview()
    {
        showCategoryView = EditorGUILayout.Foldout(showCategoryView, "Category Overview", true);
        if (!showCategoryView) return;

        EditorGUILayout.BeginVertical(GUI.skin.box);

        if (categorizedPools == null || categorizedPools.Count == 0)
        {
            EditorGUILayout.HelpBox("No pools categorized. Add pool configurations to see categories.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        foreach (var category in categorizedPools.Keys.OrderBy(k => k))
        {
            var pools = categorizedPools[category];
            if (pools == null || pools.Count == 0) continue;


            Color origColor = GUI.backgroundColor;
            GUI.backgroundColor = GetCategoryColor(category == defaultCategoryNameProp.stringValue ? null : category);
            EditorGUILayout.BeginVertical("Window");
            GUI.backgroundColor = origColor;

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginHorizontal(GUILayout.Width(250));
            GUIStyle categoryStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField(category, categoryStyle);

            int readyPools = pools.Count(p => GetConfigStatus(p) == "Ready");
            int totalObjects = pools.Sum(p => p.initialPoolSize);

            GUILayout.Space(8); // small spacing between name and counts
            EditorGUILayout.LabelField($"Ready {readyPools}/{pools.Count}", GUILayout.Width(70));
            EditorGUILayout.LabelField($"Total {totalObjects}", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);

            foreach (var pool in pools.OrderBy(p => p.poolKey))
            {
                var status = GetConfigStatus(pool);
                int validPrefabs = pool.prefabs?.Count(p => p != null) ?? 0;

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical(GUILayout.Width(220));
                EditorGUILayout.LabelField(pool.poolKey, EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Prefabs: {validPrefabs}", GUILayout.Width(70));
                EditorGUILayout.LabelField($"Size: {pool.initialPoolSize}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"Max: {pool.maxPoolSize}", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical(GUILayout.Width(100));
                EditorGUILayout.LabelField(status, GetStatusStyle(status), GUILayout.Width(80));
                GUIContent selectIcon = EditorGUIUtility.IconContent("d_ViewToolMove");
                selectIcon.tooltip = "Select PoolConfig";
                if (GUILayout.Button(selectIcon, GUILayout.Width(22), GUILayout.Height(18)))
                {
                    Selection.activeObject = pool;
                    EditorGUIUtility.PingObject(pool);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(3);
            }

            GUILayout.Space(6);
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
        }

        EditorGUILayout.EndVertical();
    }

    private Color GetCategoryColor(string category)
    {
        if (string.IsNullOrEmpty(category))
            return new Color(0.8f, 0.8f, 0.8f, 0.4f);

        int hash = category.GetHashCode();
        float r = ((hash >> 16) & 0xFF) / 255f;
        float g = ((hash >> 8) & 0xFF) / 255f;
        float b = (hash & 0xFF) / 255f;
        return new Color(r * 0.4f + 0.3f, g * 0.4f + 0.3f, b * 0.4f + 0.3f, 0.5f);
    }

    private void DrawPoolConfigurationsOverview()
    {
        var configs = GetFilteredConfigs().ToList();

        if (configs.Count == 0)
        {
            if (poolConfigsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No pool configurations added. Click 'Create New Pool' or drag PoolConfig assets into the list above.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("No pools match your search.", MessageType.Info);
            }
            return;
        }

        EditorGUILayout.LabelField($"Pool Overview ({configs.Count}):", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (groupPoolsByCategoryProp.boolValue)
        {
            var groupedConfigs = configs.GroupBy(c => GetPoolCategory(c))
                                      .OrderBy(g => g.Key);

            foreach (var group in groupedConfigs)
            {
                EditorGUILayout.LabelField($"Category: {group.Key} ({group.Count()} pools)", EditorStyles.boldLabel);

                foreach (var config in group.OrderBy(c => c.poolKey))
                {
                    DrawPoolConfigCard(config, GetCategoryColor(config.poolCategory));
                }

                EditorGUILayout.Space();
            }
        }
        else
        {
            foreach (var config in configs)
            {
                DrawPoolConfigCard(config, GetCategoryColor(config.poolCategory));
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPoolConfigCard(PoolConfig config, Color backgroundColor)
    {
        if (config == null) return;

        Color origColor = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor;
        EditorGUILayout.BeginVertical("Window");
        GUI.backgroundColor = origColor;

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(config.poolKey, EditorStyles.boldLabel);
        if (groupPoolsByCategoryProp.boolValue && !string.IsNullOrEmpty(config.poolCategory))
            EditorGUILayout.LabelField($"Category: {config.poolCategory}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        var status = GetConfigStatus(config);
        EditorGUILayout.LabelField(status, GetStatusStyle(status), GUILayout.Width(80));
        GUILayout.FlexibleSpace();

        GUIContent selectIcon = EditorGUIUtility.IconContent("d_ViewToolZoom On");
        selectIcon.tooltip = "Select & Ping this PoolConfig";
        if (GUILayout.Button(selectIcon, GUILayout.Width(25), GUILayout.Height(20)))
        {
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        GUIContent copyIcon = EditorGUIUtility.IconContent("Clipboard");
        copyIcon.tooltip = "Copy pool key";
        if (GUILayout.Button(copyIcon, GUILayout.Width(25), GUILayout.Height(20)))
        {
            GUIUtility.systemCopyBuffer = config.poolKey;
            Debug.Log($"Copied pool key: {config.poolKey}");
        }

        if (status == "Missing Key")
        {
            GUIContent addKeyIcon = EditorGUIUtility.IconContent("CreateAddNew");
            addKeyIcon.tooltip = "Add missing pool key";
            if (GUILayout.Button(addKeyIcon, GUILayout.Width(25), GUILayout.Height(20)))
                AddKeyToPoolKeys(config);
        }

        GUIContent removeIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
        removeIcon.tooltip = "Remove this config from list";
        if (GUILayout.Button(removeIcon, GUILayout.Width(25), GUILayout.Height(20)))
        {
            RemoveConfigFromList(config);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        var prefabCount = config.prefabs?.Length ?? 0;
        var validPrefabs = config.prefabs?.Count(p => p != null) ?? 0;

        EditorGUILayout.LabelField($"Prefabs: {validPrefabs}/{prefabCount}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"Size: {config.initialPoolSize}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"Max: {config.maxPoolSize}", GUILayout.Width(80));
        if (config.prewarmOnStart) EditorGUILayout.LabelField("Prewarm", GUILayout.Width(55));
        if (config.logPoolActivity) EditorGUILayout.LabelField("Logging", GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        if (status != "Ready")
            EditorGUILayout.HelpBox(GetConfigErrorMessage(config), MessageType.Warning);

        GUILayout.Space(4);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private string GetPoolCategory(PoolConfig config)
    {
        if (config == null) return "Unknown";

        if (!string.IsNullOrEmpty(config.poolCategory))
        {
            return config.poolCategory;
        }

        return defaultCategoryNameProp.stringValue;
    }

    private void CategorizePools()
    {
        categorizedPools = new Dictionary<string, List<PoolConfig>>();
        var configs = GetAllPoolConfigs().ToList();

        foreach (var config in configs)
        {
            if (config == null) continue;

            string category = GetPoolCategory(config);
            if (!categorizedPools.ContainsKey(category))
            {
                categorizedPools[category] = new List<PoolConfig>();
            }
            categorizedPools[category].Add(config);
        }
    }

    private string GetConfigStatus(PoolConfig config)
    {
        if (config == null) return "Error: Null Config";

        if (string.IsNullOrEmpty(config.poolKey))
            return "Error: No Key";

        if (!IsValidCSharpIdentifier(config.poolKey))
            return "Error: Invalid Key";

        if (!IsKeyInPoolKeysFile(config.poolKey))
            return "Missing Key";

        if (config.prefabs == null || config.prefabs.Length == 0)
            return "Error: No Prefabs";

        if (config.prefabs.All(p => p == null))
            return "Error: All Missing";

        if (config.initialPoolSize <= 0)
            return "Error: Bad Size";

        if (config.maxPoolSize < config.initialPoolSize)
            return "Warning: Size Mismatch";

        return "Ready";
    }

    private string GetConfigErrorMessage(PoolConfig config)
    {
        if (config == null) return "Configuration is null";

        var messages = new List<string>();

        if (string.IsNullOrEmpty(config.poolKey))
            messages.Add("Pool key is empty");
        else if (!IsValidCSharpIdentifier(config.poolKey))
            messages.Add("Pool key must be a valid C# identifier (letters, numbers, underscore)");
        else if (!IsKeyInPoolKeysFile(config.poolKey))
            messages.Add("Pool key not found in PoolKeys.cs - use 'Add Key' button");

        if (config.prefabs == null || config.prefabs.Length == 0)
            messages.Add("No prefabs assigned");
        else if (config.prefabs.All(p => p == null))
            messages.Add("All prefab references are missing");
        else if (config.prefabs.Any(p => p == null))
            messages.Add("Some prefab references are missing");

        if (config.initialPoolSize <= 0)
            messages.Add("Initial size must be greater than 0");

        if (config.maxPoolSize < config.initialPoolSize)
            messages.Add("Max size cannot be less than initial size");

        return string.Join(" • ", messages);
    }

    private GUIStyle GetStatusStyle(string status)
    {
        var style = new GUIStyle(EditorStyles.miniLabel);

        if (status == "Ready")
        {
            style.normal.textColor = Color.green;
        }
        else if (status.StartsWith("Error:"))
        {
            style.normal.textColor = Color.red;
        }
        else if (status.StartsWith("Warning:"))
        {
            style.normal.textColor = Color.yellow;
        }
        else if (status == "Missing Key")
        {
            style.normal.textColor = new Color(1f, 0.5f, 0f);
        }
        else
        {
            style.normal.textColor = Color.gray;
        }

        return style;
    }

    private IEnumerable<PoolConfig> GetFilteredConfigs()
    {
        var allConfigs = GetAllPoolConfigs();

        if (string.IsNullOrEmpty(searchText))
        {
            return allConfigs.OrderBy(c => c.poolKey);
        }

        var searchLower = searchText.ToLower();
        return allConfigs
            .Where(c => c != null && (c.poolKey?.ToLower().Contains(searchLower) == true ||
                       c.poolCategory?.ToLower().Contains(searchLower) == true ||
                       c.prefabs?.Any(p => p != null && p.name.ToLower().Contains(searchLower)) == true))
            .OrderBy(c => c.poolKey);
    }

    private IEnumerable<PoolConfig> GetAllPoolConfigs()
    {
        for (int i = 0; i < poolConfigsProp.arraySize; i++)
        {
            var config = poolConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue as PoolConfig;
            if (config != null)
                yield return config;
        }
    }

    private void RefreshPoolKeysCache()
    {
        var poolKeysFiles = AssetDatabase.FindAssets("PoolKeys t:Script")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(path => Path.GetFileNameWithoutExtension(path) == "PoolKeys")
            .ToArray();

        if (poolKeysFiles.Length == 0)
        {
            poolKeysInFile = new string[0];
            Debug.LogWarning("PoolKeys.cs file not found in project!");
            return;
        }

        try
        {
            var lines = File.ReadAllLines(poolKeysFiles[0]);
            poolKeysInFile = lines
                .Where(l => l.Trim().StartsWith("public const string"))
                .Select(l =>
                {
                    var parts = l.Trim().Split(new[] { ' ', '=' }, System.StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 4 ? parts[3] : null;
                })
                .Where(k => k != null && IsValidCSharpIdentifier(k))
                .ToArray();

            Debug.Log($"Refreshed PoolKeys cache: {poolKeysInFile.Length} keys found");
        }
        catch (System.Exception e)
        {
            poolKeysInFile = new string[0];
            Debug.LogError($"Failed to read PoolKeys.cs: {e.Message}");
        }
    }

    private bool IsKeyInPoolKeysFile(string key)
    {
        return poolKeysInFile != null && poolKeysInFile.Contains(key);
    }

    private bool IsValidCSharpIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return false;
        if (!char.IsLetter(identifier[0]) && identifier[0] != '_') return false;

        return identifier.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private void AddKeyToPoolKeys(PoolConfig config)
    {
        var poolKeysFiles = AssetDatabase.FindAssets("PoolKeys t:Script")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(path => Path.GetFileNameWithoutExtension(path) == "PoolKeys")
            .ToArray();

        if (poolKeysFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "PoolKeys.cs file not found in project!", "OK");
            return;
        }

        string poolKeysPath = poolKeysFiles[0];

        try
        {
            var lines = File.ReadAllLines(poolKeysPath).ToList();

            int insertIndex = lines.FindIndex(l => l.Contains("}"));
            if (insertIndex > 0)
            {
                lines.Insert(insertIndex, $"    public const string {config.poolKey} = \"{config.poolKey}\";");
                File.WriteAllLines(poolKeysPath, lines);
                AssetDatabase.Refresh();
                RefreshPoolKeysCache();
                EditorUtility.DisplayDialog("Success", $"Key '{config.poolKey}' added to PoolKeys!", "OK");
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to add key: {e.Message}", "OK");
        }
    }

    private void CreateNewPoolConfig()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Pool Config",
            "NewPoolConfig.asset",
            "asset",
            "Create a new pool configuration"
        );

        if (!string.IsNullOrEmpty(path))
        {
            var asset = ScriptableObject.CreateInstance<PoolConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            poolConfigsProp.arraySize++;
            poolConfigsProp.GetArrayElementAtIndex(poolConfigsProp.arraySize - 1).objectReferenceValue = asset;

            serializedObject.ApplyModifiedProperties();
            Selection.activeObject = asset;
        }
    }

    private void SortPoolConfigsAlphabetically()
    {
        var configs = GetAllPoolConfigs()
            .Where(c => !string.IsNullOrEmpty(c.poolKey))
            .OrderBy(c => c.poolKey)
            .ToList();

        for (int i = poolConfigsProp.arraySize - 1; i >= 0; i--)
        {
            poolConfigsProp.DeleteArrayElementAtIndex(i);
        }

        poolConfigsProp.arraySize = configs.Count;
        for (int i = 0; i < configs.Count; i++)
        {
            poolConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue = configs[i];
        }

        serializedObject.ApplyModifiedProperties();
        Debug.Log($"Sorted {configs.Count} pool configurations alphabetically");
    }

    private void SortPoolConfigsByCategory()
    {
        var configs = GetAllPoolConfigs()
            .Where(c => !string.IsNullOrEmpty(c.poolKey))
            .OrderBy(c => GetPoolCategory(c))
            .ThenBy(c => c.poolKey)
            .ToList();

        for (int i = poolConfigsProp.arraySize - 1; i >= 0; i--)
        {
            poolConfigsProp.DeleteArrayElementAtIndex(i);
        }

        poolConfigsProp.arraySize = configs.Count;
        for (int i = 0; i < configs.Count; i++)
        {
            poolConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue = configs[i];
        }

        serializedObject.ApplyModifiedProperties();
        CategorizePools();
        Debug.Log($"Sorted {configs.Count} pool configurations by category");
    }

    private void RefreshConfigList()
    {
        RemoveNullConfigs();

        CategorizePools();

        Repaint();
        Debug.Log("Refreshed pool configuration list");
    }

    private void RemoveNullConfigs()
    {
        int removedCount = 0;
        for (int i = poolConfigsProp.arraySize - 1; i >= 0; i--)
        {
            if (poolConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                poolConfigsProp.DeleteArrayElementAtIndex(i);
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Removed {removedCount} null references from pool list");
        }
    }

    private void RemoveConfigFromList(PoolConfig config)
    {
        for (int i = 0; i < poolConfigsProp.arraySize; i++)
        {
            if (poolConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue == config)
            {
                if (EditorUtility.DisplayDialog("Remove Pool Config",
                    $"Remove '{config.poolKey}' from the manager?\n(The asset will NOT be deleted)",
                    "Remove", "Cancel"))
                {
                    poolConfigsProp.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    CategorizePools();
                    Debug.Log($"Removed {config.poolKey} from PoolSystemManager");
                }
                break;
            }
        }
    }
}
#endif