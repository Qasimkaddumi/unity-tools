namespace Kaddumi.UnityTools.Ads.Core
{
    public struct AdErrorDomain
    {
        public int Code;
        public string Message;

        public AdErrorDomain(int code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}
