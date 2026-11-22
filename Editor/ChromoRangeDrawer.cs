using UnityEditor;
using UnityEngine;

namespace Chromolithograph.Editor
{
    /// <summary>A style setting: a tick box to override, then the value's slider (greyed out while unticked).</summary>
    [CustomPropertyDrawer(typeof(ChromoRangeAttribute))]
    public class ChromoRangeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var range = (ChromoRangeAttribute)attribute;
            SerializedProperty on = property.FindPropertyRelative("on");
            SerializedProperty value = property.FindPropertyRelative("value");
            EditorGUI.BeginProperty(position, label, property);
            Rect toggle = new Rect(position.x, position.y, 18f, position.height);
            Rect rest = new Rect(position.x + 20f, position.y, position.width - 20f, position.height);
            on.boolValue = EditorGUI.Toggle(toggle, on.boolValue);
            using (new EditorGUI.DisabledScope(!on.boolValue))
                value.floatValue = EditorGUI.Slider(rest, label, value.floatValue, range.min, range.max);
            EditorGUI.EndProperty();
        }
    }
}
