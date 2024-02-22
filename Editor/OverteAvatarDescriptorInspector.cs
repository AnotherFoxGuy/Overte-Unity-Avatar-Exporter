using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Overte.Exporter.Avatar
{
    [CustomEditor(typeof(OverteAvatarDescriptor))]
    public class OverteAvatarDescriptorInspector : Editor
    {

        // SerializedProperty exportPath;

        // void OnEnable()
        // {
        //     exportPath = serializedObject.FindProperty("m_exportPath");
        // }

        public override VisualElement CreateInspectorGUI()
        {
            // Create a new VisualElement to be the root of our inspector UI
            var _ins = new VisualElement();

            // Add a simple label
            // _ins.Add(new Label("This is a custom inspector"));

            var nBox = new TextField("Name");
            nBox.bindingPath = "AvatarName";
            _ins.Add(nBox);

            // var pathButton = new Button();
            // var bl = new Label("Update");
            // pathButton.Add(bl);
            // pathButton.clicked += UpdatePath;

            // var eBox = new TextField("Export path");
            // eBox.isReadOnly = true;
            // eBox.bindingPath = "m_exportPath";
            // eBox.Add(pathButton);
            // _ins.Add(eBox);

            // var exportButton = new Button();
            // var ebl = new Label("Export Avatar");
            // exportButton.Add(ebl);
            // exportButton.clicked += UpdatePath;

            // _ins.Add(exportButton);

            // Return the finished inspector UI
            return _ins;
        }

        // private void UpdatePath()
        // {
        //     serializedObject.Update();
        //     exportPath.stringValue = EditorUtility.SaveFilePanel("Select .fst", exportPath.stringValue, "Avatar", "fst");
        //     serializedObject.ApplyModifiedProperties();
        // }
    }
}
