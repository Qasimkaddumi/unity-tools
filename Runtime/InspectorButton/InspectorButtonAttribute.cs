using System;

namespace Kaddumi.UnityTools.InspectorButton
{
    /// <summary>
    /// Add this attribute to any parameterless method in a MonoBehaviour to create a button in the Unity Inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class InspectorButtonAttribute : Attribute
    {
        public string ButtonText { get; private set; }

        public InspectorButtonAttribute(string buttonText = null)
        {
            ButtonText = buttonText;
        }
    }
}
