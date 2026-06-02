using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HauntedReelsView))]
public class HauntedReelsViewEditor : Editor
{
    // campos herdados que não fazem sentido nesta view
    private static readonly HashSet<string> _hidden = new HashSet<string>
    {
        "_betIncreaseButton",
        "_betDecreaseButton",
        "_betPerLineText",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // m_Script (desabilitado, padrão Unity)
        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        GUI.enabled = true;

        var prop = serializedObject.GetIterator();
        prop.NextVisible(true); // avança além de m_Script

        while (prop.NextVisible(false))
        {
            if (_hidden.Contains(prop.name)) continue;
            EditorGUILayout.PropertyField(prop, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
