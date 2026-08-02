using System.Linq;
using System.Reflection;
using Kaddumi.UnityTools.InspectorButton;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.InspectorButton.Editor
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects] // Allows the buttons to work even when you select multiple objects!
    public class InspectorButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector fields first
            base.OnInspectorGUI();

            var mono = target as MonoBehaviour;
            if (mono == null) return;

            // Find all methods with the InspectorButtonAttribute
            var methods = mono.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttributes(typeof(InspectorButtonAttribute), true).Length > 0);

            if (methods.Any())
            {
                EditorGUILayout.Space(); // Add a little spacing before drawing the buttons

                foreach (var method in methods)
                {
                    var attr = (InspectorButtonAttribute)method.GetCustomAttributes(typeof(InspectorButtonAttribute), true)[0];

                    // Use the provided text, otherwise fallback to a nicely formatted method name
                    string buttonText = string.IsNullOrEmpty(attr.ButtonText)
                        ? ObjectNames.NicifyVariableName(method.Name)
                        : attr.ButtonText;

                    if (GUILayout.Button(buttonText))
                    {
                        // Safety check: Make sure the method has no parameters
                        if (method.GetParameters().Length > 0)
                        {
                            Debug.LogWarning($"[InspectorButton] Method '{method.Name}' has parameters and cannot be invoked via the simple button.");
                            continue;
                        }

                        // Invoke the method for all currently selected objects
                        foreach (var t in targets)
                        {
                            method.Invoke(t, null);
                        }
                    }
                }
            }
        }
    }
}
