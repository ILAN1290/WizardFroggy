using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomGenerator))]
public class RoomGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Визуальные заглушки", EditorStyles.boldLabel);
        SerializedProperty fallback = serializedObject.FindProperty("useOnGUIIfNoSprite");
        EditorGUILayout.PropertyField(fallback,
            new GUIContent("Использовать OnGUI если нет спрайта"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Спрайты (можно назначить позже)", EditorStyles.boldLabel);
        DrawPropertiesExcluding(serializedObject, "m_Script", "useOnGUIIfNoSprite");

        serializedObject.ApplyModifiedProperties();
    }
}
