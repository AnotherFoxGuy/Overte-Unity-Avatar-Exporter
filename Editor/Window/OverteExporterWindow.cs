using System;
using System.Collections.Generic;
using System.Linq;
using Overte.Exporter.Avatar;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class OverteExporterWindow : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;
    // private AvatarDescriptor[] m_avatarList = new AvatarDescriptor[0];
    // private AvatarDescriptor m_avatar = new AvatarDescriptor();

    private List<GameObject> avatarList;
    private DropdownField _dropdownField;

    [MenuItem("Overte/Show Avatar exporter window")]
    public static void ShowWindow()
    {
        OverteExporterWindow wnd = GetWindow<OverteExporterWindow>();
        wnd.titleContent = new GUIContent("Overte Exporter");
    }

    public void CreateGUI()
    {
        // Instantiate UXML
        var labelFromUXML = m_VisualTreeAsset.Instantiate();
        rootVisualElement.Add(labelFromUXML);
        _dropdownField = rootVisualElement.Q<DropdownField>("avatar_list");
        // rootVisualElement.Q<DropdownField>("avatar_list").b;
        _dropdownField.RegisterValueChangedCallback(SelectAvatar);

        var exButton = rootVisualElement.Q<Button>("export_button");
        exButton.clicked += RunExporter;

        OnFocus();
    }

    private void RunExporter()
    {
        var av = avatarList[_dropdownField.index];
        var path = EditorUtility.SaveFilePanel("Select .fst", "", av.name, "fst");
        if (path == "")
            return;

        var avex = new AvatarExporter(av);
        avex.ExportAvatar(path);
    }

    private void SelectAvatar(ChangeEvent<string> evt)
    {
        var av = avatarList[_dropdownField.index];
        Debug.Log($"AAAA {av.name}");
    }

    void OnFocus()
    {
        avatarList = FindObjectsByType<OverteAvatarDescriptor>(FindObjectsSortMode.None)
            .ToList()
            .Select(x => x.gameObject)
            .ToList();

        var uxmlField = rootVisualElement.Q<DropdownField>("avatar_list");

        if (uxmlField == null)
            return;

        uxmlField.choices = avatarList.ToList().Select(x => x.name).ToList();
    }

}
