#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor Window hiá»ƒn thá»‹ danh sÃ¡ch scene Ä‘á»ƒ má»Ÿ nhanh.
/// TÃ­ch chá»n "Build Settings Only" hoáº·c hiá»‡n táº¥t cáº£ scene trong project.
/// Menu: Tools > Scene Switcher (Ctrl+Shift+S)
/// </summary>
public class SceneSwitcherWindow : EditorWindow
{
    private bool _buildSettingsOnly = true;
    private Vector2 _scrollPos;
    private string _searchFilter = "";
    private List<SceneEntry> _cachedScenes = new List<SceneEntry>();
    private bool _needsRefresh = true;

    private class SceneEntry
    {
        public string name;
        public string path;
        public bool inBuildSettings;
        public bool enabled;
        public int buildIndex;
    }

    [MenuItem("Tools/Scene Switcher")]
    public static void ShowWindow()
    {
        var window = GetWindow<SceneSwitcherWindow>("Scene Switcher");
        window.minSize = new Vector2(300, 200);
    }

    private void OnEnable()
    {
        _needsRefresh = true;
        EditorBuildSettings.sceneListChanged += OnSceneListChanged;
    }

    private void OnDisable()
    {
        EditorBuildSettings.sceneListChanged -= OnSceneListChanged;
    }

    private void OnSceneListChanged()
    {
        _needsRefresh = true;
        Repaint();
    }

    private void RefreshSceneList()
    {
        _cachedScenes.Clear();

        // Build Settings scenes
        var buildScenes = EditorBuildSettings.scenes;
        var buildPaths = new HashSet<string>(buildScenes.Select(s => s.path));

        foreach (var buildScene in buildScenes)
        {
            _cachedScenes.Add(new SceneEntry
            {
                name = Path.GetFileNameWithoutExtension(buildScene.path),
                path = buildScene.path,
                inBuildSettings = true,
                enabled = buildScene.enabled,
                buildIndex = _cachedScenes.Count
            });
        }

        // All scenes in project (náº¿u cáº§n)
        if (!_buildSettingsOnly)
        {
            var allSceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in allSceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!buildPaths.Contains(path))
                {
                    _cachedScenes.Add(new SceneEntry
                    {
                        name = Path.GetFileNameWithoutExtension(path),
                        path = path,
                        inBuildSettings = false,
                        enabled = false,
                        buildIndex = -1
                    });
                }
            }
        }

        _needsRefresh = false;
    }

    private void OnGUI()
    {
        if (_needsRefresh)
        {
            RefreshSceneList();
        }

        DrawToolbar();
        DrawSceneList();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Search field
        var newFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField,
            GUILayout.MinWidth(100));
        if (newFilter != _searchFilter)
        {
            _searchFilter = newFilter;
        }

        // Clear search button
        if (GUILayout.Button("âœ•", EditorStyles.toolbarButton, GUILayout.Width(22)))
        {
            _searchFilter = "";
            GUI.FocusControl(null);
        }

        GUILayout.FlexibleSpace();

        // Toggle Build Settings Only
        var newBuildOnly = GUILayout.Toggle(_buildSettingsOnly, "Build Settings Only",
            EditorStyles.toolbarButton, GUILayout.Width(130));
        if (newBuildOnly != _buildSettingsOnly)
        {
            _buildSettingsOnly = newBuildOnly;
            _needsRefresh = true;
        }

        // Refresh button
        if (GUILayout.Button("â†»", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            _needsRefresh = true;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSceneList()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        var currentScenePath = EditorSceneManager.GetActiveScene().path;
        var filteredScenes = _cachedScenes
            .Where(s => string.IsNullOrEmpty(_searchFilter) ||
                        s.name.ToLower().Contains(_searchFilter.ToLower()) ||
                        s.path.ToLower().Contains(_searchFilter.ToLower()))
            .ToList();

        if (filteredScenes.Count == 0)
        {
            EditorGUILayout.HelpBox(
                _buildSettingsOnly
                    ? "KhÃ´ng cÃ³ scene nÃ o trong Build Settings."
                    : "KhÃ´ng tÃ¬m tháº¥y scene nÃ o.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
            return;
        }

        foreach (var scene in filteredScenes)
        {
            var isCurrent = scene.path == currentScenePath;

            EditorGUILayout.BeginHorizontal(isCurrent
                ? CreateHighlightStyle()
                : EditorStyles.helpBox);

            // Checkbox: toggle enabled/disabled trong Build Settings
            if (scene.inBuildSettings)
            {
                var newEnabled = GUILayout.Toggle(scene.enabled, GUIContent.none, GUILayout.Width(18));
                if (newEnabled != scene.enabled)
                {
                    ToggleBuildScene(scene.path, newEnabled);
                    scene.enabled = newEnabled;
                }
            }
            else
            {
                GUILayout.Space(22); // Align vá»›i checkbox
            }

            // Icon
            var icon = scene.inBuildSettings
                ? (scene.enabled ? "d_SceneAsset Icon" : "d_SceneAsset On Icon")
                : "d_SceneAsset Icon";

            var statusLabel = "";
            if (isCurrent)
            {
                statusLabel = " â–¶";
            }
            else if (scene.inBuildSettings && !scene.enabled)
            {
                statusLabel = " (disabled)";
            }
            else if (!scene.inBuildSettings)
            {
                statusLabel = " (not in build)";
            }

            // Scene name
            var displayName = scene.name + statusLabel;
            if (scene.inBuildSettings)
            {
                displayName = $"[{scene.buildIndex}] {displayName}";
            }

            var iconContent = EditorGUIUtility.IconContent(icon);
            GUILayout.Label(iconContent, GUILayout.Width(20), GUILayout.Height(22));

            // Name label (clickable feel)
            var nameStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = isCurrent ? FontStyle.Bold : FontStyle.Normal,
                richText = true
            };

            if (isCurrent)
            {
                nameStyle.normal.textColor = new Color(0.3f, 0.8f, 0.4f);
            }
            else if (!scene.enabled && scene.inBuildSettings)
            {
                nameStyle.normal.textColor = Color.gray;
            }

            GUILayout.Label(displayName, nameStyle, GUILayout.Height(22));
            GUILayout.FlexibleSpace();

            // Path tooltip
            EditorGUILayout.LabelField(new GUIContent("", scene.path), GUILayout.Width(0));

            // Open button
            if (!isCurrent)
            {
                if (GUILayout.Button("Open", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    OpenScene(scene.path);
                }

                if (GUILayout.Button("+", GUILayout.Width(22), GUILayout.Height(20)))
                {
                    OpenSceneAdditive(scene.path);
                }
            }
            else
            {
                GUILayout.Label("current", EditorStyles.miniLabel, GUILayout.Width(50));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // Footer
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField($"Scenes: {filteredScenes.Count}", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Build Settings...", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OpenScene(string path)
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }

    private void OpenSceneAdditive(string path)
    {
        EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private void ToggleBuildScene(string path, bool enabled)
    {
        var scenes = EditorBuildSettings.scenes;
        for (var i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == path)
            {
                scenes[i].enabled = enabled;
                break;
            }
        }

        EditorBuildSettings.scenes = scenes;
    }

    private GUIStyle CreateHighlightStyle()
    {
        var style = new GUIStyle(EditorStyles.helpBox);
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, new Color(0.2f, 0.4f, 0.2f, 0.3f));
        tex.Apply();
        style.normal.background = tex;
        return style;
    }
}
#endif


