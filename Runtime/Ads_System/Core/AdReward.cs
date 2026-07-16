namespace Kaddumi.UnityTools.Ads.Core
{
    public class AdReward
    {
        public string Type;
        public double Amount;

        public AdReward(string type, double amount)
        {
            Type = type;
            Amount = amount;
        }
    }
}
