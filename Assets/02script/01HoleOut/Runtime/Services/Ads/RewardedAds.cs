using System;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Điểm gắn quảng cáo thưởng. Chưa tích hợp SDK: mặc định coi như xem xong ngay.
    /// Khi gắn SDK chỉ cần thay <see cref="RewardedHandler"/>.
    /// </summary>
    public static class RewardedAds
    {
        /// <summary>Hiện quảng cáo thưởng rồi gọi lại với true nếu người chơi xem hết.</summary>
        public static Action<string, Action<bool>> RewardedHandler = (placement, done) => done(true);

        public static void ShowRewarded(string placement, Action<bool> done) => RewardedHandler(placement, done);
    }
}
