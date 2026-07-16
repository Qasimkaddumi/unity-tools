using Kaddumi.UnityTools.LoadingSystem.Core;
using System;
using System.Collections;
using UnityEngine;

namespace Kaddumi.UnityTools.LoadingSystem.Operations
{
    public class ActionLoadingOperation : ILoadingOperation
    {
        private readonly Action actionToExecute;
        private readonly float simulatedWaitTime;
        private const float MaxDeltaTimePerFrame = 0.1f;
        public float Progress { get; private set; }
        public bool IsDone { get; private set; }

        public ActionLoadingOperation(Action actionToExecute, float simulatedWaitTime = 0f)
        {
            this.actionToExecute = actionToExecute;
            this.simulatedWaitTime = simulatedWaitTime;
            Progress = 0f;
            IsDone = false;
        }

        public IEnumerator Execute()
        {
            Progress = 0f;

            if (simulatedWaitTime > 0f)
            {
                float elapsed = 0f;
                while (elapsed < simulatedWaitTime)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxDeltaTimePerFrame);
                    Progress = Mathf.Clamp01(elapsed / simulatedWaitTime);
                    yield return null;
                }
            }

            actionToExecute?.Invoke();

            Progress = 1f;
            IsDone = true;
        }
    }
}