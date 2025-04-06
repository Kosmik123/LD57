using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[CustomPropertyDrawer(typeof(NavMeshAgentTypeAttribute))]
public class NavMeshAgentTypePropertyDrawer : PropertyDrawerBase
{
    protected override void OnGUI_Internal(Rect rect, SerializedProperty property, GUIContent label)
    {
        var index = -1;
        var count = NavMesh.GetSettingsCount();
        var agentTypeNames = new string[count];
        for (var i = 0; i < count; i++)
        {
            var id = NavMesh.GetSettingsByIndex(i).agentTypeID;
            var name = NavMesh.GetSettingsNameFromID(id);
            agentTypeNames[i] = name;
            if (id == property.intValue)
                index = i;
        }

        bool validAgentType = index != -1;
        if (!validAgentType)
        {
            EditorGUILayout.HelpBox("Agent Type invalid.", MessageType.Warning);
        }

        EditorGUI.BeginProperty(rect, label, property);
        EditorGUI.BeginChangeCheck();
        index = EditorGUI.Popup(rect, label.text, index, agentTypeNames);
        if (EditorGUI.EndChangeCheck())
        {
            if (index >= 0 && index < count)
            {
                var id = NavMesh.GetSettingsByIndex(index).agentTypeID;
                property.intValue = id;
            }
        }

        EditorGUI.EndProperty();
    }
}

