using System;
using System.Text;

namespace Kaddumi.UnityTools.Save.Core
{
    /// <summary>
    /// What happened in one store during a save/load/delete. A single operation now spans
    /// several stores, so each one reports separately and the aggregate
    /// <see cref="SaveReport"/> collects them.
    /// </summary>
    public struct SaveStoreOutcome
    {
        public string StoreId;

        /// <summary>Whether this store's failure is allowed to fail the whole operation.</summary>
        public bool Required;

        public bool Success;

        public SaveError Error;

        public static SaveStoreOutcome Ok(SaveStore store) => new SaveStoreOutcome
        {
            StoreId = store.Id,
            Required = store.Required,
            Success = true
        };

        public static SaveStoreOutcome Fail(SaveStore store, SaveError error) => new SaveStoreOutcome
        {
            StoreId = store.Id,
            Required = store.Required,
            Success = false,
            Error = error
        };

        public static SaveStoreOutcome Fail(SaveStore store, SaveErrorType type, string message, int code = 0) =>
            Fail(store, new SaveError(type, message, code));

        public static SaveStoreOutcome From(SaveStore store, SaveResult result) =>
            result.Success ? Ok(store) : Fail(store, result.Error);

        public override string ToString() =>
            Success ? $"{StoreId}: ok" : $"{StoreId}: {Error}";
    }

    /// <summary>
    /// Result of a service-level save/load/delete, which may have touched several stores.
    /// <see cref="Success"/> means every <em>required</em> store succeeded — an optional
    /// store (see <see cref="SaveStore.Required"/>) that failed shows up in
    /// <see cref="Stores"/> without sinking the operation, so an offline cloud push doesn't
    /// make a perfectly good local save look broken.
    /// </summary>
    public class SaveReport
    {
        private static readonly SaveStoreOutcome[] NoStores = Array.Empty<SaveStoreOutcome>();

        /// <summary>True when every required store succeeded.</summary>
        public bool Success { get; private set; }

        /// <summary>
        /// The failure worth surfacing: the first required store's error, or — when only
        /// optional stores failed — the first of those. Default when nothing failed.
        /// </summary>
        public SaveError Error { get; private set; }

        /// <summary>Per-store outcomes, in the order the stores were written.</summary>
        public SaveStoreOutcome[] Stores { get; private set; } = NoStores;

        /// <summary>True when at least one store failed, required or not.</summary>
        public bool HasFailures
        {
            get
            {
                for (int i = 0; i < Stores.Length; i++)
                {
                    if (!Stores[i].Success) return true;
                }
                return false;
            }
        }

        /// <summary>True when the named store took part and succeeded.</summary>
        public bool SucceededFor(string storeId) =>
            TryGetOutcome(storeId, out var outcome) && outcome.Success;

        public bool TryGetOutcome(string storeId, out SaveStoreOutcome outcome)
        {
            for (int i = 0; i < Stores.Length; i++)
            {
                if (string.Equals(Stores[i].StoreId, storeId, StringComparison.OrdinalIgnoreCase))
                {
                    outcome = Stores[i];
                    return true;
                }
            }
            outcome = default;
            return false;
        }

        /// <summary>Report for an operation that never reached a store, e.g. an invalid slot.</summary>
        public static SaveReport Fail(SaveError error) => new SaveReport
        {
            Success = false,
            Error = error,
            Stores = NoStores
        };

        public static SaveReport Fail(SaveErrorType type, string message, int code = 0) =>
            Fail(new SaveError(type, message, code));

        /// <summary>Report for an operation that legitimately had no store to touch.</summary>
        public static SaveReport Ok() => new SaveReport { Success = true, Stores = NoStores };

        /// <summary>Folds per-store outcomes into an aggregate verdict.</summary>
        public static SaveReport From(SaveStoreOutcome[] outcomes)
        {
            outcomes = outcomes ?? NoStores;

            var report = new SaveReport { Success = true, Stores = outcomes };

            bool haveOptionalError = false;
            for (int i = 0; i < outcomes.Length; i++)
            {
                if (outcomes[i].Success) continue;

                if (outcomes[i].Required)
                {
                    // First required failure wins: it's the one the caller must handle.
                    report.Success = false;
                    report.Error = outcomes[i].Error;
                    return report;
                }

                if (!haveOptionalError)
                {
                    haveOptionalError = true;
                    report.Error = outcomes[i].Error;
                }
            }
            return report;
        }

        public override string ToString()
        {
            var builder = new StringBuilder(Success ? "success" : $"failed ({Error})");
            if (Stores.Length > 0)
            {
                builder.Append(" [");
                for (int i = 0; i < Stores.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    builder.Append(Stores[i]);
                }
                builder.Append(']');
            }
            return builder.ToString();
        }
    }
}
