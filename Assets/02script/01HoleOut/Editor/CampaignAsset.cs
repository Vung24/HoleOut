using HoleOut.Runtime;
using UnityEditor;

namespace HoleOut.EditorTools
{
    /// <summary>Đường dẫn và tạo asset chiến dịch mặc định cho công cụ Editor.</summary>
    public static class CampaignAsset
    {
        public const string Path = "Assets/07data/HoleOutCampaign.asset";

        public static CampaignSO LoadOrCreate()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSO>(Path);
            if (campaign != null) return campaign;
            campaign = UnityEngine.ScriptableObject.CreateInstance<CampaignSO>();
            AssetDatabase.CreateAsset(campaign, Path);
            return campaign;
        }
    }
}
