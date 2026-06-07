using System.Collections;

namespace LoadingSystem.Core
{
    public interface ILoadingOperation
    {
        float Progress { get; }
        bool IsDone { get; }
        IEnumerator Execute();
    }
}