using System.Collections;

namespace Kaddumi.UnityTools.LoadingSystem.Core
{
    public interface ILoadingOperation
    {
        float Progress { get; }
        bool IsDone { get; }
        IEnumerator Execute();
    }
}