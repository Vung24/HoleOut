using System;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Nhận animation event <c>OnJumpStart</c> của clip nhảy trên prefab nhân vật.</summary>
    public class JumpEvent : MonoBehaviour
    {
        public Action onJumpStart;

        public void OnJumpStart()
        {
            onJumpStart?.Invoke();
        }
    }
}
