using System;
using UnityEngine;

namespace Kaddumi.UnityTools.InterfaceSerialization
{
    /// <summary>
    /// A generic wrapper that allows Unity to serialize interfaces.
    /// High-level modules should depend on the generic TInterface (Dependency Inversion).
    /// </summary>
    /// <typeparam name="TInterface">The interface type to serialize.</typeparam>
    [Serializable]
    public class InterfaceReference<TInterface> where TInterface : class
    {
        [SerializeField]
        [Tooltip("The Unity Object that implements the interface.")]
        private UnityEngine.Object targetObject;

        /// <summary>
        /// Default constructor required for Unity serialization.
        /// </summary>
        public InterfaceReference()
        {
        }

        /// <summary>
        /// Initializes a new instance with a specific target.
        /// </summary>
        /// <param name="target">The interface implementation to assign.</param>
        public InterfaceReference(TInterface target)
        {
            Value = target;
        }

        /// <summary>
        /// Gets or sets the interface implementation.
        /// </summary>
        public TInterface Value
        {
            get => targetObject as TInterface;
            set => AssignTargetObject(value);
        }

        /// <summary>
        /// Exposes the raw Unity Object if needed for Unity-specific lifecycle checks (e.g., destroyed state).
        /// </summary>
        public UnityEngine.Object TargetObject => targetObject;

        private void AssignTargetObject(TInterface newTarget)
        {
            if (newTarget == null)
            {
                targetObject = null;
                return;
            }

            if (newTarget is UnityEngine.Object unityObject)
            {
                targetObject = unityObject;
            }
            else
            {
                throw new ArgumentException($"Assigned value must be a UnityEngine.Object that implements {typeof(TInterface).Name}.");
            }
        }
    }
}