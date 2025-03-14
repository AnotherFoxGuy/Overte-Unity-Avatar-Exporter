using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Overte.Exporter.Avatar;

namespace Overte.Exporter.Avatar.Editor
{
    [CustomEditor(typeof(OverteAvatarDescriptor))]
    public class OverteAvatarDescriptorEditor : UnityEditor.Editor
    {
        private SerializedProperty _avatarNameProperty;
        private SerializedProperty _remapedBlendShapeListProperty;
        private SerializedProperty _optimizeBlendShapesProperty;

        private bool _showBlendshapeList = true;
        private Vector2 _scrollPosition;
        private SkinnedMeshRenderer[] _skinnedMeshRenderers;
        private AvatarExporter _exporter;
        private Dictionary<Constants.AvatarRule, string> _warnings = new();

        private void OnEnable()
        {
            _exporter = new AvatarExporter();
            _avatarNameProperty = serializedObject.FindProperty("AvatarName");
            _remapedBlendShapeListProperty = serializedObject.FindProperty("RemapedBlendShapeList");
            _optimizeBlendShapesProperty = serializedObject.FindProperty("OptimizeBlendShapes");

            // if (string.IsNullOrEmpty(avatarNameProperty.stringValue))
            // {
            //     var av = (OverteAvatarDescriptor)target;
            //     avatarNameProperty.stringValue = av.gameObject.name;
            // }

            // Cache skinned mesh renderers
            RefreshSkinnedMeshRenderers();
            CheckForErrors();
        }

        private void CheckForErrors()
        {
            var av = (OverteAvatarDescriptor)target;
            _warnings = _exporter.CheckForErrors(av.gameObject);
        }

        private void RefreshSkinnedMeshRenderers()
        {
            var descriptor = (OverteAvatarDescriptor)target;
            _skinnedMeshRenderers = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_avatarNameProperty);
            
            EditorGUILayout.PropertyField(_optimizeBlendShapesProperty);

            // Refresh button for skinned mesh renderers
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Skinned Mesh Renderers", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshSkinnedMeshRenderers();
            }

            EditorGUILayout.EndHorizontal();

            if (_skinnedMeshRenderers.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No SkinnedMeshRenderer components found in children. Add meshes to your avatar to map blendshapes.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"Found {_skinnedMeshRenderers.Length} SkinnedMeshRenderer(s)",
                    EditorStyles.miniLabel);
            }

            // Custom GUI for the RemapedBlendShapeList
            DrawBlendShapeList();

            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.Separator();

            foreach (var warning in _warnings)
            {
                EditorGUILayout.HelpBox(warning.Value, MessageType.Warning);
            }
            
            if (_avatarNameProperty.stringValue == "")
            {
                EditorGUILayout.HelpBox("Avatar name not set!", MessageType.Error);
                GUI.enabled = false;
            }
            if (GUILayout.Button("Export avatar"))
            {
                ExportAvatar();
            }
            GUI.enabled = true;
        }

        private void ExportAvatar()
        {
            var path = EditorUtility.SaveFilePanel("Select .fst", "", _avatarNameProperty.stringValue, "fst");
            if (path == "")
                return;

            var av = (OverteAvatarDescriptor)target;
            Debug.Log(path);
            _exporter.ExportAvatar(_avatarNameProperty.stringValue, path, av.gameObject);
        }

        private void DrawBlendShapeList()
        {
            EditorGUILayout.Space();

            // Header with foldout and buttons
            EditorGUILayout.BeginHorizontal();
            _showBlendshapeList = EditorGUILayout.Foldout(_showBlendshapeList, "Blend Shape Remapping", true,
                EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Add Mapping", GUILayout.Width(100)))
            {
                AddNewBlendshapeMapping();
            }

            EditorGUILayout.EndHorizontal();

            if (!_showBlendshapeList)
                return;

            // Scrollable area for blend shape mappings
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition,
                GUILayout.Height(Mathf.Min(_remapedBlendShapeListProperty.arraySize * 120f, 300f)));

            for (var i = 0; i < _remapedBlendShapeListProperty.arraySize; i++)
            {
                DrawBlendshapeElement(i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBlendshapeElement(int index)
        {
            var blendshapeElement = _remapedBlendShapeListProperty.GetArrayElementAtIndex(index);
            var fromProperty = blendshapeElement.FindPropertyRelative("from");
            var toProperty = blendshapeElement.FindPropertyRelative("to");
            var multiplierProperty = blendshapeElement.FindPropertyRelative("multiplier");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Heading with delete button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Mapping {index + 1}", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
            {
                RemoveBlendshapeMapping(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            // From field with blendshape selector
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(fromProperty, new GUIContent("From Blendshape"));

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                BlendshapeSelectorWindow.ShowWindow(_skinnedMeshRenderers, fromProperty, serializedObject);
            }

            EditorGUILayout.EndHorizontal();

            // To field with enum selector
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(toProperty, new GUIContent("To Overte Shape"));

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                OverteBLendshapeSelectorWindow.ShowWindow(toProperty, serializedObject);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Slider(multiplierProperty, 0f, 2f, new GUIContent("Multiplier"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void AddNewBlendshapeMapping()
        {
            var index = _remapedBlendShapeListProperty.arraySize;
            _remapedBlendShapeListProperty.arraySize++;

            var newElement = _remapedBlendShapeListProperty.GetArrayElementAtIndex(index);
            newElement.FindPropertyRelative("from").stringValue = "";
            newElement.FindPropertyRelative("to").stringValue = "";
            newElement.FindPropertyRelative("multiplier").floatValue = 1.0f;

            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveBlendshapeMapping(int index)
        {
            _remapedBlendShapeListProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
        }
    }

    // Popup window for selecting source blendshapes from SkinnedMeshRenderers
    public class BlendshapeSelectorWindow : EditorWindow
    {
        private SkinnedMeshRenderer[] skinnedMeshRenderers;
        private SerializedProperty targetProperty;
        private SerializedObject serializedObject;

        private Vector2 scrollPosition;
        private string searchText = "";
        private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

        public static void ShowWindow(SkinnedMeshRenderer[] renderers, SerializedProperty property,
            SerializedObject serializedObj)
        {
            if (renderers == null || renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("No Skinned Mesh Renderers",
                    "No SkinnedMeshRenderer components found in the avatar hierarchy.", "OK");
                return;
            }

            var window = GetWindow<BlendshapeSelectorWindow>(true, "Blendshape Selector");
            window.minSize = new Vector2(350, 400);
            window.maxSize = new Vector2(500, 600);
            window.skinnedMeshRenderers = renderers;
            window.targetProperty = property;
            window.serializedObject = serializedObj;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Select a Blendshape", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            var newSearch = EditorGUILayout.TextField(searchText);
            if (newSearch != searchText)
            {
                searchText = newSearch;
                // Automatically expand all foldouts when searching
                if (!string.IsNullOrEmpty(searchText))
                {
                    foreach (var renderer in skinnedMeshRenderers)
                    {
                        if (renderer && renderer.sharedMesh)
                        {
                            foldoutStates[renderer.name] = true;
                        }
                    }
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchText = "";
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Blendshape list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var anyBlendshapesFound = false;

            foreach (var renderer in skinnedMeshRenderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;

                var rendererName = string.IsNullOrEmpty(renderer.name) ? "Unnamed Mesh" : renderer.name;

                // Skip renderers with no blendshapes
                if (renderer.sharedMesh.blendShapeCount == 0) continue;

                // Filter renderers by search text
                var matchingBlendshapeIndices = new List<int>();
                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var blendshapeName = renderer.sharedMesh.GetBlendShapeName(i);
                    if (string.IsNullOrEmpty(searchText) ||
                        blendshapeName.ToLower().Contains(searchText.ToLower()) ||
                        rendererName.ToLower().Contains(searchText.ToLower()))
                    {
                        matchingBlendshapeIndices.Add(i);
                    }
                }

                if (matchingBlendshapeIndices.Count == 0) continue;

                anyBlendshapesFound = true;

                // Initialize foldout state if not exists
                if (!foldoutStates.ContainsKey(rendererName))
                {
                    foldoutStates[rendererName] = false;
                }

                // Renderer foldout
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Renderer header with count
                EditorGUILayout.BeginHorizontal();
                foldoutStates[rendererName] = EditorGUILayout.Foldout(
                    foldoutStates[rendererName],
                    $"{rendererName} ({matchingBlendshapeIndices.Count} blendshapes)",
                    true
                );

                EditorGUILayout.EndHorizontal();

                // List blendshapes if foldout is open
                if (foldoutStates[rendererName])
                {
                    EditorGUI.indentLevel++;

                    foreach (var i in matchingBlendshapeIndices)
                    {
                        var blendshapeName = renderer.sharedMesh.GetBlendShapeName(i);

                        EditorGUILayout.BeginHorizontal();

                        // Highlight search matches
                        var style = new GUIStyle(EditorStyles.label);
                        if (!string.IsNullOrEmpty(searchText) &&
                            blendshapeName.ToLower().Contains(searchText.ToLower()))
                        {
                            style.fontStyle = FontStyle.Bold;
                        }

                        EditorGUILayout.LabelField(blendshapeName, style);

                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            SelectBlendshape(blendshapeName);
                            GUIUtility.ExitGUI(); // Close window after selection
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (!anyBlendshapesFound)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(searchText)
                        ? "No blendshapes found in any SkinnedMeshRenderer."
                        : $"No blendshapes matching '{searchText}' found.",
                    MessageType.Info
                );
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Cancel button at bottom
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }
        }

        private void SelectBlendshape(string blendshapeName)
        {
            targetProperty.stringValue = blendshapeName;
            serializedObject.ApplyModifiedProperties();
            Close();
        }
    }

    // Popup window for selecting Overte blendshape enum values
    public class OverteBLendshapeSelectorWindow : EditorWindow
    {
        private SerializedProperty targetProperty;
        private SerializedObject serializedObject;

        private Vector2 scrollPosition;
        private string searchText = "";
        private string[] enumNames;

        public static void ShowWindow(SerializedProperty property, SerializedObject serializedObj)
        {
            var window = GetWindow<OverteBLendshapeSelectorWindow>(true, "Overte Blendshape Selector");
            window.minSize = new Vector2(350, 400);
            window.maxSize = new Vector2(500, 600);
            window.targetProperty = property;
            window.serializedObject = serializedObj;
            window.enumNames = System.Enum.GetNames(typeof(Constants.Blendshapes));
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Select Overte Blendshape", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            var newSearch = EditorGUILayout.TextField(searchText);
            if (newSearch != searchText)
            {
                searchText = newSearch;
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchText = "";
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Group blendshapes by category
            var groupedBlendshapes = new Dictionary<string, List<string>>();

            // Define groups based on enum naming patterns
            groupedBlendshapes["Basic"] = new List<string> { "EyeBlink_L", "EyeBlink_R", "JawOpen" };
            groupedBlendshapes["Eye"] = new List<string>();
            groupedBlendshapes["Brows"] = new List<string>();
            groupedBlendshapes["Jaw"] = new List<string>();
            groupedBlendshapes["Mouth"] = new List<string>();
            groupedBlendshapes["Lips"] = new List<string>();
            groupedBlendshapes["Cheek"] = new List<string>();
            groupedBlendshapes["Nose"] = new List<string>();
            groupedBlendshapes["Tongue"] = new List<string>();
            groupedBlendshapes["User"] = new List<string>();
            groupedBlendshapes["Other"] = new List<string>();

            // Populate groups
            foreach (var enumName in enumNames)
            {
                var added = false;
                foreach (var group in groupedBlendshapes.Keys.ToArray())
                {
                    if (group != "Other" && enumName.Contains(group))
                    {
                        groupedBlendshapes[group].Add(enumName);
                        added = true;
                        break;
                    }
                }

                // Add to User group for UserBlendshape items
                if (!added && enumName.StartsWith("UserBlendshape"))
                {
                    groupedBlendshapes["User"].Add(enumName);
                    added = true;
                }

                // Add to Other if no match was found
                if (!added)
                {
                    groupedBlendshapes["Other"].Add(enumName);
                }
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var anyFound = false;

            // Display grouped blendshapes
            foreach (var group in groupedBlendshapes)
            {
                // Filter by search text
                var matchingItems = group.Value.Where(item =>
                    string.IsNullOrEmpty(searchText) ||
                    item.ToLower().Contains(searchText.ToLower())).ToList();

                if (matchingItems.Count == 0)
                    continue;

                anyFound = true;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{group.Key} Blendshapes ({matchingItems.Count})", EditorStyles.boldLabel);

                foreach (var item in matchingItems)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Highlight search matches
                    var style = new GUIStyle(EditorStyles.label);
                    if (!string.IsNullOrEmpty(searchText) &&
                        item.ToLower().Contains(searchText.ToLower()))
                    {
                        style.fontStyle = FontStyle.Bold;
                    }

                    EditorGUILayout.LabelField(item, style);

                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        SelectBlendshape(item);
                        GUIUtility.ExitGUI(); // Close window after selection
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (!anyFound)
            {
                EditorGUILayout.HelpBox(
                    $"No Overte blendshapes matching '{searchText}' found.",
                    MessageType.Info
                );
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Cancel button at bottom
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }
        }

        private void SelectBlendshape(string blendshapeName)
        {
            targetProperty.stringValue = blendshapeName;
            serializedObject.ApplyModifiedProperties();
            Close();
        }
    }
}