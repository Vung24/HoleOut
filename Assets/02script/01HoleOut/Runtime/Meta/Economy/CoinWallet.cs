using System;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Nơi cộng/trừ coin của người chơi.</summary>
    public interface IWallet
    {
        int Balance { get; }
        void Add(int amount);
        bool TrySpend(int amount);
    }

    /// <summary>Ví coin lưu bền bằng PlayerPrefs; phát <see cref="Changed"/> khi số dư đổi.</summary>
    public sealed class CoinWallet : IWallet
    {
        const string Key = "HoleOut.Coins";
        public static readonly CoinWallet Shared = new CoinWallet();
        public static event Action<int> Changed;

        public int Balance => Mathf.Max(0, PlayerPrefs.GetInt(Key, 0));

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Set(Balance + amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Balance < amount) return false;
            Set(Balance - amount);
            return true;
        }

        static void Set(int value)
        {
            PlayerPrefs.SetInt(Key, Mathf.Max(0, value));
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }
}
