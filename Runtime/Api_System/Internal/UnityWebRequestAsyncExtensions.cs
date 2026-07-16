using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Kaddumi.UnityTools.Api.Internal
{
    /// <summary>
    /// Bridges <see cref="UnityWebRequest"/> to <c>async</c>/<c>await</c> so
    /// <see cref="Core.ApiClient"/> can be written as plain awaited code. The completion
    /// callback fires on Unity's main thread, so awaiting from a MonoBehaviour resumes
    /// there too. Honouring a <see cref="CancellationToken"/> aborts the in-flight
    /// request. Internal to the assembly — systems use <c>IApiClient</c>, not this.
    /// </summary>
    internal static class UnityWebRequestAsyncExtensions
    {
        public static Task<UnityWebRequest> SendAsync(this UnityWebRequest request, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();

            if (cancellationToken.IsCancellationRequested)
            {
                request.Abort();
                tcs.TrySetCanceled(cancellationToken);
                return tcs.Task;
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    if (!request.isDone) request.Abort();
                });
            }

            void Complete()
            {
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested) tcs.TrySetCanceled(cancellationToken);
                else tcs.TrySetResult(request);
            }

            if (operation.isDone) Complete();
            else operation.completed += _ => Complete();

            return tcs.Task;
        }
    }
}
