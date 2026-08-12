#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;

public static class DebugViewInspector
{
    [MenuItem("Tools/Toggle Debug or Normal view inspector &w")]
    static void ToggleDebugNormalView()
    {
        EditorWindow targetInspector = EditorWindow.mouseOverWindow;
        if (targetInspector != null && targetInspector.GetType().Name == "InspectorWindow")
        {
            Type type = Assembly.GetAssembly(typeof(UnityEditor.Editor)).GetType("UnityEditor.InspectorWindow");
            FieldInfo field = type.GetField("m_InspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);

            InspectorMode mode = (InspectorMode)field.GetValue(targetInspector);
            mode = (mode == InspectorMode.Normal ? InspectorMode.Debug : InspectorMode.Normal);

            MethodInfo method = type.GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(targetInspector, new object[] { mode });

            targetInspector.Repaint();       //refresh inspector
        }
    }
}
#endif
