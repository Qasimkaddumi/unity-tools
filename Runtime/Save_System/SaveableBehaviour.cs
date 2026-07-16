using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Save
{
    /// <summary>
    /// Ergonomic base class for gameplay objects that want to be saved. Implement
    /// <see cref="Capture"/>/<see cref="Restore"/> with a plain <typeparamref name="TState"/>
    /// struct/class and this handles the JSON conversion (via <see cref="JsonUtility"/>) and
    /// automatic (un)registration with the <see cref="SaveManager"/>.
    ///
    /// <para>Give each instance a unique <see cref="SaveKey"/>. For a single-instance system
    /// a constant string is fine; for objects there may be several of, expose a serialized id.</para>
    ///
    /// <example><code>
    /// public class PlayerStats : SaveableBehaviour&lt;PlayerStats.State&gt;
    /// {
    ///     public int health;
    ///     public override string SaveKey =&gt; "player.stats";
    ///
    ///     [System.Serializable] public class State { public int health; }
    ///
    ///     protected override State Capture() =&gt; new State { health = health };
    ///     protected override void Restore(State s) =&gt; health = s.health;
    /// }
    /// </code></example>
    /// </summary>
    /// <typeparam name="TState">Serializable state type (public fields, no properties/dictionaries).</typeparam>
    public abstract class SaveableBehaviour<TState> : MonoBehaviour, ISaveable
        where TState : class, new()
    {
        private bool _registered;

        /// <summary>Stable unique key for this object's slice of the save.</summary>
        public abstract string SaveKey { get; }

        /// <summary>Build the serializable snapshot of this object's state.</summary>
        protected abstract TState Capture();

        /// <summary>Apply a previously captured snapshot back onto this object.</summary>
        protected abstract void Restore(TState state);

        string ISaveable.CaptureState() => JsonUtility.ToJson(Capture());

        void ISaveable.RestoreState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            var parsed = JsonUtility.FromJson<TState>(state);
            if (parsed != null) Restore(parsed);
        }

        protected virtual void OnEnable() => TryRegister();

        // Start covers the case where this component's OnEnable ran before the
        // SaveManager's Awake set its Instance (scene load order isn't guaranteed).
        protected virtual void Start() => TryRegister();

        protected virtual void OnDisable()
        {
            if (_registered)
            {
                SaveManager.Instance?.Unregister(this);
                _registered = false;
            }
        }

        private void TryRegister()
        {
            if (_registered || SaveManager.Instance == null) return;
            SaveManager.Instance.Register(this);
            _registered = true;
        }
    }
}
