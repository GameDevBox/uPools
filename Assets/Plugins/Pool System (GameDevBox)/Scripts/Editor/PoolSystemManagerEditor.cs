using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

    // Minimalist color scheme
    private static readonly Color SECTION_BG = new Color(0.2f, 0.2f, 0.2f, 0.3f);
    private static readonly Color CARD_BG = new Color(0.15f, 0.15f, 0.15f, 0.2f);
    private static readonly Color ACCENT_COLOR = new Color(0.3f, 0.5f, 0.9f, 0.1f);

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

        EditorGUILayout.Space(5);

        DrawHeader();

        EditorGUILayout.Space(10);
        DrawPoolParentSettings();
        EditorGUILayout.Space(10);
        DrawConfigListSection();
        EditorGUILayout.Space(10);
        DrawSearchAndActions();
        EditorGUILayout.Space(10);
        DrawCategoryOverview();
        EditorGUILayout.Space(10);
        DrawPoolConfigurationsOverview();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical();
        var titleStyle = new GUIStyle(EditorStyles.largeLabel)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        EditorGUILayout.LabelField("Pool System Manager", titleStyle);
        EditorGUILayout.EndVertical();
    }

    private void DrawPoolParentSettings()
    {
        DrawSectionHeader("Settings");
        EditorGUILayout.BeginVertical(GetSectionStyle());

        EditorGUILayout.PropertyField(poolParentProp);
        EditorGUILayout.PropertyField(groupPoolsByCategoryProp);

        if (groupPoolsByCategoryProp.boolValue)
        {
            EditorGUILayout.PropertyField(defaultCategoryNameProp);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawConfigListSection()
    {
        EditorGUILayout.BeginVertical(GetSectionStyle());

        showConfigList = EditorGUILayout.Foldout(showConfigList, "Pool Configurations", true);

        if (showConfigList)
        {
            EditorGUILayout.HelpBox("Drag and drop PoolConfig assets here to register them.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(poolConfigsProp, true);

            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            if (GUILayout.Button("Refresh", EditorStyles.miniButton))
            {
                RefreshConfigList();
            }
            if (GUILayout.Button("Clean Nulls", EditorStyles.miniButton))
            {
                RemoveNullConfigs();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSearchAndActions()
    {
        // Search field
        EditorGUILayout.BeginHorizontal();
        searchText = EditorGUILayout.TextField("", searchText, GUI.skin.FindStyle("SearchTextField"));
        if (GUILayout.Button("", GUI.skin.FindStyle("SearchCancelButton")))
        {
            searchText = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        // Action buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create New", EditorStyles.miniButton))
        {
            CreateNewPoolConfig();
        }
        if (GUILayout.Button("Sort A-Z", EditorStyles.miniButton))
        {
            SortPoolConfigsAlphabetically();
        }
        if (GUILayout.Button("Refresh Keys", EditorStyles.miniButton))
        {
            RefreshPoolKeysCache();
            CategorizePools();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        // Stats
        var configs = GetAllPoolConfigs().ToList();
        var readyCount = configs.Count(c => GetConfigStatus(c) == "Ready");
        var warningCount = configs.Count(c => GetConfigStatus(c) == "Warning");
        var errorCount = configs.Count(c => GetConfigStatus(c).StartsWith("Error"));

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{configs.Count} Pools", GetMiniLabelStyle(), GUILayout.Width(70));
        EditorGUILayout.LabelField($"{readyCount} Ready", GetStatusStyle("Ready"), GUILayout.Width(60));
        if (warningCount > 0)
            EditorGUILayout.LabelField($"{warningCount} Warn", GetStatusStyle("Warning"), GUILayout.Width(50));
        if (errorCount > 0)
            EditorGUILayout.LabelField($"{errorCount} Error", GetStatusStyle("Error"), GUILayout.Width(50));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCategoryOverview()
    {
        showCategoryView = EditorGUILayout.Foldout(showCategoryView, "Categories", true);
        if (!showCategoryView) return;

        EditorGUILayout.BeginVertical(GetSectionStyle());

        if (categorizedPools == null || categorizedPools.Count == 0)
        {
            EditorGUILayout.HelpBox("No pools categorized.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        foreach (var category in categorizedPools.Keys.OrderBy(k => k))
        {
            var pools = categorizedPools[category];
            if (pools == null || pools.Count == 0) continue;

            EditorGUILayout.BeginVertical(GetCardStyle());

            // Category header
            EditorGUILayout.BeginHorizontal();
            var categoryStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
            EditorGUILayout.LabelField(category, categoryStyle, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            int readyPools = pools.Count(p => GetConfigStatus(p) == "Ready");
            int totalObjects = pools.Sum(p => p.initialPoolSize);

            EditorGUILayout.LabelField($"{readyPools}/{pools.Count} ready", GetMiniLabelStyle(), GUILayout.Width(70));
            EditorGUILayout.LabelField($"{totalObjects} total", GetMiniLabelStyle(), GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
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
                EditorGUILayout.HelpBox("No pool configurations added.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("No pools match your search.", MessageType.Info);
            }
            return;
        }

        EditorGUILayout.LabelField($"Pool Overview ({configs.Count})", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

        foreach (var config in configs)
        {
            DrawPoolConfigCard(config);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPoolConfigCard(PoolConfig config)
    {
        if (config == null) return;

        EditorGUILayout.BeginVertical(GetCardStyle());

        // Header row
        EditorGUILayout.BeginHorizontal();

        // Pool info
        EditorGUILayout.BeginVertical();
        var keyStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11
        };
        EditorGUILayout.LabelField(config.poolKey, keyStyle);

        if (groupPoolsByCategoryProp.boolValue && !string.IsNullOrEmpty(config.poolCategory))
        {
            EditorGUILayout.LabelField(config.poolCategory, GetMiniLabelStyle());
        }
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        // Status
        var status = GetConfigStatus(config);
        EditorGUILayout.LabelField(status, GetStatusStyle(status), GUILayout.Width(60));

        // Action buttons
        DrawIconButton("d_ViewToolZoom On", "Select", () =>
        {
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        });

        DrawIconButton("Clipboard", "Copy Key", () =>
        {
            GUIUtility.systemCopyBuffer = config.poolKey;
            Debug.Log($"Copied pool key: {config.poolKey}");
        });

        if (status == "Missing Key")
        {
            DrawIconButton("CreateAddNew", "Add Key", () => AddKeyToPoolKeys(config));
        }

        DrawIconButton("TreeEditor.Trash", "Remove", () => RemoveConfigFromList(config));

        EditorGUILayout.EndHorizontal();

        // Details row
        EditorGUILayout.BeginHorizontal();
        var prefabCount = config.prefabs?.Length ?? 0;
        var validPrefabs = config.prefabs?.Count(p => p != null) ?? 0;

        EditorGUILayout.LabelField($"Prefabs: {validPrefabs}/{prefabCount}", GetMiniLabelStyle(), GUILayout.Width(80));
        EditorGUILayout.LabelField($"Size: {config.initialPoolSize}", GetMiniLabelStyle(), GUILayout.Width(60));
        EditorGUILayout.LabelField($"Max: {config.maxPoolSize}", GetMiniLabelStyle(), GUILayout.Width(60));

        if (config.prewarmOnStart)
            EditorGUILayout.LabelField("Prewarm", GetMiniLabelStyle(), GUILayout.Width(50));
        if (config.logPoolActivity)
            EditorGUILayout.LabelField("Logging", GetMiniLabelStyle(), GUILayout.Width(50));

        EditorGUILayout.EndHorizontal();

        // Error message if needed
        if (status != "Ready")
        {
            EditorGUILayout.HelpBox(GetConfigErrorMessage(config), MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(4);
    }

    private void DrawIconButton(string iconName, string tooltip, System.Action action)
    {
        GUIContent icon = EditorGUIUtility.IconContent(iconName);
        icon.tooltip = tooltip;
        if (GUILayout.Button(icon, GUILayout.Width(20), GUILayout.Height(16)))
        {
            action?.Invoke();
        }
    }

    private void DrawSectionHeader(string title)
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            margin = new RectOffset(0, 0, 5, 5)
        };
        EditorGUILayout.LabelField(title, style);
    }

    private GUIStyle GetSectionStyle()
    {
        var style = new GUIStyle(GUI.skin.box);
        style.margin = new RectOffset(2, 2, 2, 2);
        style.padding = new RectOffset(8, 8, 8, 8);
        return style;
    }

    private GUIStyle GetCardStyle()
    {
        var style = new GUIStyle(GUI.skin.box);
        style.margin = new RectOffset(2, 2, 2, 2);
        style.padding = new RectOffset(6, 6, 6, 6);
        return style;
    }

    private GUIStyle GetMiniLabelStyle()
    {
        return new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9
        };
    }

    private string GetPoolCategory(PoolConfig config)
    {
        if (config == null) return "Unknown";
        return !string.IsNullOrEmpty(config.poolCategory) ? config.poolCategory : defaultCategoryNameProp.stringValue;
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
                categorizedPools[category] = new List<PoolConfig>();
            categorizedPools[category].Add(config);
        }
    }

    private string GetConfigStatus(PoolConfig config)
    {
        if (config == null) return "Error: Null Config";
        if (string.IsNullOrEmpty(config.poolKey)) return "Error: No Key";
        if (!IsValidCSharpIdentifier(config.poolKey)) return "Error: Invalid Key";
        if (!IsKeyInPoolKeysFile(config.poolKey)) return "Missing Key";
        if (config.prefabs == null || config.prefabs.Length == 0) return "Error: No Prefabs";
        if (config.prefabs.All(p => p == null)) return "Error: All Missing";
        if (config.initialPoolSize <= 0) return "Error: Bad Size";
        if (config.maxPoolSize < config.initialPoolSize) return "Warning: Size Mismatch";
        return "Ready";
    }

    private string GetConfigErrorMessage(PoolConfig config)
    {
        if (config == null) return "Configuration is null";
        var messages = new List<string>();
        if (string.IsNullOrEmpty(config.poolKey)) messages.Add("Pool key is empty");
        else if (!IsValidCSharpIdentifier(config.poolKey)) messages.Add("Pool key must be valid C# identifier");
        else if (!IsKeyInPoolKeysFile(config.poolKey)) messages.Add("Pool key not found in PoolKeys.cs");
        if (config.prefabs == null || config.prefabs.Length == 0) messages.Add("No prefabs assigned");
        else if (config.prefabs.All(p => p == null)) messages.Add("All prefab references missing");
        else if (config.prefabs.Any(p => p == null)) messages.Add("Some prefab references missing");
        if (config.initialPoolSize <= 0) messages.Add("Initial size must be > 0");
        if (config.maxPoolSize < config.initialPoolSize) messages.Add("Max size < initial size");
        return string.Join(" • ", messages);
    }

    private GUIStyle GetStatusStyle(string status)
    {
        var style = new GUIStyle(EditorStyles.miniLabel);
        if (status == "Ready") style.normal.textColor = Color.green;
        else if (status.StartsWith("Error:")) style.normal.textColor = Color.red;
        else if (status.StartsWith("Warning:")) style.normal.textColor = Color.yellow;
        else if (status == "Missing Key") style.normal.textColor = new Color(1f, 0.5f, 0f);
        else style.normal.textColor = Color.gray;
        return style;
    }

    private IEnumerable<PoolConfig> GetFilteredConfigs()
    {
        var allConfigs = GetAllPoolConfigs();
        if (string.IsNullOrEmpty(searchText)) return allConfigs.OrderBy(c => c.poolKey);
        var searchLower = searchText.ToLower();
        return allConfigs.Where(c => c != null && (c.poolKey?.ToLower().Contains(searchLower) == true ||
                           c.poolCategory?.ToLower().Contains(searchLower) == true ||
                           c.prefabs?.Any(p => p != null && p.name.ToLower().Contains(searchLower)) == true))
            .OrderBy(c => c.poolKey);
    }

    private IEnumerable<PoolConfig> GetAllPoolConfigs()
    {
        for (int i = 0; i < poolConfigsProp.arraySize; i++)
        {
            var config = poolConfigsProp.GetArrayElementAtIndex(i).objectReferenceValue as PoolConfig;
            if (config != null) yield return config;
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
