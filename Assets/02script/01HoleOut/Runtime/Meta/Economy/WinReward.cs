namespace HoleOut.Runtime
{
    /// <summary>
    /// Thưởng thắng của một màn, tách khỏi UI: chốt số coin khi thắng, chỉ cho nhận một lần (thường hoặc nhân đôi).
    /// </summary>
    public sealed class WinReward
    {
        readonly IWallet _wallet;
        int _coins, _multiplier = 2;
        bool _ready, _claimed;

        public WinReward(IWallet wallet) => _wallet = wallet;

        public int Coins => _ready ? _coins : 0;
        public int DoubledCoins => Coins * _multiplier;
        public bool CanClaim => _ready && !_claimed && _coins > 0;

        /// <summary>Gọi khi vào màn mới: xóa thưởng của màn trước.</summary>
        public void Reset()
        {
            _ready = _claimed = false;
            _coins = 0;
        }

        /// <summary>Chốt thưởng khi thắng; gọi lại trong cùng màn không đổi số đã chốt.</summary>
        public void Prepare(int coins, int multiplier)
        {
            if (_ready) return;
            _coins = System.Math.Max(0, coins);
            _multiplier = System.Math.Max(1, multiplier);
            _ready = true;
        }

        /// <summary>Nhận thưởng; trả số coin đã cộng (0 nếu không nhận được).</summary>
        public int Claim(bool doubled)
        {
            if (!CanClaim) return 0;
            _claimed = true;
            int amount = doubled ? DoubledCoins : _coins;
            _wallet.Add(amount);
            return amount;
        }
    }
}
