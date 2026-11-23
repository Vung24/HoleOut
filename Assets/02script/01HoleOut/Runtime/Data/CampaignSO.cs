using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Thứ tự màn của chiến dịch; dùng chung cho màn chơi, Home (album) và công cụ Editor.</summary>
    [CreateAssetMenu(menuName = "HoleOut/Campaign", fileName = "HoleOutCampaign")]
    public class CampaignSO : ScriptableObject
    {
        public List<LevelSO> levels = new List<LevelSO>();
    }
}
