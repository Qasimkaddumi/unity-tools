using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kaddumi.UnityTools.InterfaceSerialization.Editor
{
    /// <summary>
    /// Custom Inspector GUI for InterfaceReference&lt;T&gt;.
    /// Renders a native-looking object field whose picker is filtered to only the
    /// UnityEngine.Object types that actually implement the target interface, and
    /// validates drag-and-drop (extracting a matching component from a dropped GameObject).
    /// Note: must live in an 'Editor' folder / editor-only assembly.
    /// </summary>
    [CustomPropertyDrawer(typeof(InterfaceReference<>))]
    public class InterfaceReferenceDrawer : PropertyDrawer
    {
        private const string TargetFieldName = "targetObject";
        private const float PickerButtonWidth = 19f;

        private static readonly int PickerControlHint = "InterfaceReferencePicker".GetHashCode();

        // Cached implementer discovery keyed by interface type (TypeCache scan done once per type).
        private static readonly Dictionary<Type, InterfaceImplementers> ImplementerCache = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty targetObjectProperty = property.FindPropertyRelative(TargetFieldName);
            if (targetObjectProperty == null)
            {
                EditorGUI.LabelField(position, label.text, $"Missing '{TargetFieldName}' field");
                return;
            }

            Type interfaceType = ExtractInterfaceType();
            InterfaceImplementers implementers = ResolveImplementers(interfaceType);

            EditorGUI.BeginProperty(position, label, property);

            position.height = EditorGUIUtility.singleLineHeight;
            Rect fieldRect = EditorGUI.PrefixLabel(
                position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Control ID must be requested every pass, in a stable order, so the
            // ExecuteCommand pass matches the id we opened the picker with.
            int pickerControlId = GUIUtility.GetControlID(PickerControlHint, FocusType.Keyboard, fieldRect);

            Object currentObject = targetObjectProperty.objectReferenceValue;
            Object updatedObject = currentObject;

            bool allowSceneObjects = !EditorUtility.IsPersistent(property.serializedObject.targetObject);

            HandlePickerCommands(pickerControlId, interfaceType, currentObject, ref updatedObject);
            HandleDragAndDrop(fieldRect, interfaceType, ref updatedObject);
            HandleClicks(fieldRect, pickerControlId, currentObject, implementers, allowSceneObjects);

            DrawObjectField(fieldRect, currentObject, interfaceType);

            if (updatedObject != currentObject)
            {
                targetObjectProperty.objectReferenceValue = updatedObject;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        // --- Rendering -------------------------------------------------------

        private static void DrawObjectField(Rect fieldRect, Object currentObject, Type interfaceType)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            GUIContent content = currentObject != null
                ? EditorGUIUtility.ObjectContent(currentObject, currentObject.GetType())
                : new GUIContent($"None ({interfaceType.Name})");

            bool hover = fieldRect.Contains(Event.current.mousePosition);
            EditorStyles.objectField.Draw(fieldRect, content, hover, false, false, false);

            // Native-looking picker button on the right edge.
            GUIStyle pickerStyle = GUI.skin.FindStyle("ObjectFieldButton") ?? EditorStyles.miniButton;
            Rect pickerButtonRect = new Rect(
                fieldRect.xMax - PickerButtonWidth, fieldRect.y + 1f,
                PickerButtonWidth - 2f, fieldRect.height - 2f);
            pickerStyle.Draw(pickerButtonRect, GUIContent.none, hover, false, false, false);
        }

        // --- Interaction -----------------------------------------------------

        private void HandleClicks(Rect fieldRect, int pickerControlId, Object currentObject,
            InterfaceImplementers implementers, bool allowSceneObjects)
        {
            Event evt = Event.current;
            if (evt.type != EventType.MouseDown || !fieldRect.Contains(evt.mousePosition))
            {
                return;
            }

            Rect pickerButtonRect = new Rect(
                fieldRect.xMax - PickerButtonWidth, fieldRect.y, PickerButtonWidth, fieldRect.height);

            if (pickerButtonRect.Contains(evt.mousePosition))
            {
                EditorGUIUtility.ShowObjectPicker<Object>(
                    currentObject, allowSceneObjects, implementers.SearchFilter, pickerControlId);
            }
            else if (currentObject != null)
            {
                // Click the value area: ping (single) or open the asset (double).
                if (evt.clickCount == 2)
                {
                    AssetDatabase.OpenAsset(currentObject);
                }
                else
                {
                    EditorGUIUtility.PingObject(currentObject);
                }
            }

            evt.Use();
        }

        private void HandlePickerCommands(int pickerControlId, Type interfaceType,
            Object currentObject, ref Object updatedObject)
        {
            Event evt = Event.current;
            if (evt.type != EventType.ExecuteCommand ||
                EditorGUIUtility.GetObjectPickerControlID() != pickerControlId)
            {
                return;
            }

            if (evt.commandName != "ObjectSelectorUpdated" &&
                evt.commandName != "ObjectSelectorClosed")
            {
                return;
            }

            Object picked = EditorGUIUtility.GetObjectPickerObject();
            // A cleared picker (None) is a valid null assignment; only validate non-null picks.
            updatedObject = picked == null ? null : ExtractValidImplementer(picked, interfaceType);
            evt.Use();
        }

        private void HandleDragAndDrop(Rect fieldRect, Type interfaceType, ref Object updatedObject)
        {
            Event evt = Event.current;
            if (!fieldRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            Object candidate = ResolveFirstValidDrag(interfaceType);
            DragAndDrop.visualMode = candidate != null
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;

            if (evt.type == EventType.DragPerform && candidate != null)
            {
                DragAndDrop.AcceptDrag();
                updatedObject = candidate;
            }

            evt.Use();
        }

        private Object ResolveFirstValidDrag(Type interfaceType)
        {
            foreach (Object dragged in DragAndDrop.objectReferences)
            {
                Object valid = ExtractValidImplementer(dragged, interfaceType);
                if (valid != null)
                {
                    return valid;
                }
            }

            return null;
        }

        // --- Type resolution -------------------------------------------------

        /// <summary>
        /// Uses reflection to determine the 'TInterface' generic argument from the field definition,
        /// unwrapping array / List&lt;&gt; element types where the wrapper is used in a collection.
        /// </summary>
        private Type ExtractInterfaceType()
        {
            Type currentType = fieldInfo.FieldType;

            if (currentType.IsArray)
            {
                currentType = currentType.GetElementType();
            }
            else if (currentType.IsGenericType &&
                     currentType.GetGenericTypeDefinition() == typeof(List<>))
            {
                currentType = currentType.GetGenericArguments()[0];
            }

            if (currentType != null && currentType.IsGenericType)
            {
                return currentType.GetGenericArguments()[0];
            }

            return typeof(object);
        }

        /// <summary>
        /// Validates and extracts the Object that implements the interface. If a GameObject is
        /// supplied, searches its components for a compatible implementer.
        /// </summary>
        private Object ExtractValidImplementer(Object candidate, Type interfaceType)
        {
            if (candidate == null)
            {
                return null;
            }

            if (interfaceType.IsInstanceOfType(candidate))
            {
                return candidate;
            }

            if (candidate is GameObject gameObject)
            {
                Component component = gameObject.GetComponent(interfaceType);
                if (component != null)
                {
                    return component;
                }
            }

            Debug.LogWarning(
                $"'{candidate.name}' does not implement interface '{interfaceType.Name}' and was ignored.");
            return null;
        }

        private static InterfaceImplementers ResolveImplementers(Type interfaceType)
        {
            if (ImplementerCache.TryGetValue(interfaceType, out InterfaceImplementers cached))
            {
                return cached;
            }

            InterfaceImplementers result = InterfaceImplementers.Build(interfaceType);
            ImplementerCache[interfaceType] = result;
            return result;
        }

        /// <summary>
        /// Precomputed set of concrete UnityEngine.Object types implementing an interface,
        /// plus the object-picker search filter (t:Type ...) that restricts the picker to them.
        /// </summary>
        private readonly struct InterfaceImplementers
        {
            public readonly string SearchFilter;

            private InterfaceImplementers(string searchFilter)
            {
                SearchFilter = searchFilter;
            }

            public static InterfaceImplementers Build(Type interfaceType)
            {
                var builder = new StringBuilder();
                foreach (Type type in TypeCache.GetTypesDerivedFrom<Object>())
                {
                    if (type.IsAbstract || type.IsGenericTypeDefinition ||
                        !interfaceType.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append("t:").Append(type.Name);
                }

                return new InterfaceImplementers(builder.ToString());
            }
        }
    }
}
