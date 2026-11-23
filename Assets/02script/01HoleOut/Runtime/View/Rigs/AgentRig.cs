using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Tham chiếu trên prefab nhân vật: khối ngồi, thân có xương, animator và bụi chạy.
    /// View gọi các hàm trạng thái; thời điểm do mô phỏng quyết định.
    /// </summary>
    public class AgentRig : MonoBehaviour
    {
        public Renderer seated;
        public SkinnedMeshRenderer body;
        public Animator animator;
        public ParticleSystem dust;
        [Tooltip("Bóng tròn mềm dưới chân, nằm phẳng trên mặt khay.")]
        public Renderer shadow;
        [Tooltip("Độ lệch bóng theo trục thế giới (đơn vị prefab). Cùng hướng xuống-phải với bóng khối trên bàn và khay chờ.")]
        public Vector3 shadowOffset = new Vector3(0.08f, 0, -0.14f);
        [Tooltip("Hướng mesh bóng để nằm phẳng, mặt hướng lên.")]
        public Vector3 shadowRotation = new Vector3(270, 0, 0);
        [Header("Hình khối cho shader ô tranh")]
        [Tooltip("Thân nhân vật dùng chung shader với ô tranh nhưng là mesh có xương: shader nhận kích thước theo đơn vị thế giới. " +
                 "Đây là nửa bề rộng/chiều cao thân theo đơn vị prefab, nhân với tỉ lệ hiện tại, để vệt sáng mép trên đầu và bo cạnh rơi đúng chỗ.")]
        public float bodyHalfSize = 0.39f;
        public float bodyHeight = 0.9f;
        float _appliedScale = -1;

        static MaterialPropertyBlock _shapeBlock;
        static readonly int HalfSizeId = Shader.PropertyToID("_HalfSize");
        static readonly int HeightId = Shader.PropertyToID("_Height");

        static readonly int RunHash = Animator.StringToHash("Run");
        static readonly int RunStartHash = Animator.StringToHash("Run Start");
        static readonly int JumpHash = Animator.StringToHash("Jump");

        public enum Pose { Standing, Running, Jumping }
        public Pose Current { get; private set; }

        public void Spawn(Material bodyMaterial)
        {
            gameObject.SetActive(true);
            if (seated) seated.gameObject.SetActive(false);
            if (body)
            {
                // Prefab gốc để tắt nhánh thân; bật lại toàn bộ đường cha tới gốc.
                for (var t = body.transform; t != null && t != transform; t = t.parent) t.gameObject.SetActive(true);
                var mats = body.sharedMaterials;
                if (mats.Length > 0 && bodyMaterial) { mats[0] = bodyMaterial; body.sharedMaterials = mats; }
                ApplyBodyShape();
                body.enabled = true;
            }
            if (animator)
            {
                animator.enabled = true;
                animator.speed = 1;
                animator.Rebind();
                animator.Update(0);
            }
            if (dust) { dust.gameObject.SetActive(false); }
            if (shadow) shadow.gameObject.SetActive(true);
            Current = Pose.Standing;
        }

        void ApplyBodyShape()
        {
            _appliedScale = -1;
            UpdateBodyShape();
        }

        /// <summary>Đặt lại kích thước cho shader khi tỉ lệ nhân vật đổi (khởi tạo màn, nhảy vào hố).</summary>
        void UpdateBodyShape()
        {
            if (!body) return;
            float s = transform.lossyScale.x;
            if (Mathf.Abs(s - _appliedScale) < 1e-4f) return;
            _appliedScale = s;
            var mat = body.sharedMaterial;
            if (!mat || !mat.HasProperty(HalfSizeId)) { body.SetPropertyBlock(null); return; }
            _shapeBlock ??= new MaterialPropertyBlock();
            _shapeBlock.Clear();
            _shapeBlock.SetFloat(HalfSizeId, bodyHalfSize * s);
            _shapeBlock.SetFloat(HeightId, bodyHeight * s);
            body.SetPropertyBlock(_shapeBlock);
        }

        void LateUpdate()
        {
            UpdateBodyShape();
            // Bóng không xoay theo hướng chạy: luôn phẳng và lệch cùng một hướng trên màn hình.
            bool anchored = _shadowAnchored;
            _shadowAnchored = false;
            if (!shadow || !shadow.gameObject.activeSelf) return;
            float s = transform.lossyScale.x;
            var ground = anchored ? _shadowAnchor : transform.position;
            shadow.transform.SetPositionAndRotation(ground + (shadowOffset + Vector3.up * 0.005f) * s,
                Quaternion.Euler(shadowRotation));
        }

        Vector3 _shadowAnchor;
        bool _shadowAnchored;

        /// <summary>Giữ bóng ở điểm trên sàn cho frame này khi gốc nhân vật bị nhấc lên (lăn, lộn).</summary>
        public void AnchorShadow(Vector3 groundPoint)
        {
            _shadowAnchor = groundPoint;
            _shadowAnchored = true;
        }

        public void Run()
        {
            if (Current == Pose.Running) return;
            Current = Pose.Running;
            if (animator) { animator.SetBool(RunHash, true); animator.SetTrigger(RunStartHash); }
            if (dust) { dust.gameObject.SetActive(true); dust.Play(); }
        }

        /// <summary>Phanh ở mép hố hoặc đứng chờ: thôi chạy, tắt bụi.</summary>
        public void Stop()
        {
            if (Current == Pose.Standing) return;
            Current = Pose.Standing;
            if (animator) { animator.SetBool(RunHash, false); animator.speed = 1; }
            if (dust) dust.gameObject.SetActive(false);
        }

        /// <summary>Đứng ở mép nhưng chân vẫn cuống quýt đạp (con cuối chới với trước khi nhảy).</summary>
        public void Scramble()
        {
            if (Current != Pose.Running) Run();
            if (animator) animator.speed = 1.8f;
            if (dust) dust.gameObject.SetActive(false);
        }

        /// <summary>Vấp ngã giữa đường: chân chậm lại, không tung bụi cho tới khi bật dậy.</summary>
        public void Stumble(bool down)
        {
            if (Current != Pose.Running) return;
            if (animator) animator.speed = down ? 0.35f : 1;
            if (dust) dust.gameObject.SetActive(!down);
        }

        /// <summary>Bắt đầu clip nhảy (đoạn nhún trước khi rời đất); <paramref name="speed"/> căn sự kiện cất cánh với lịch bay.</summary>
        public void Jump(float speed)
        {
            if (Current == Pose.Jumping) return;
            Current = Pose.Jumping;
            if (animator) { animator.SetBool(RunHash, false); animator.SetTrigger(JumpHash); animator.speed = speed; }
            if (dust) dust.gameObject.SetActive(false);
        }

        /// <summary>Rời mép hố: bỏ bóng thay vì để bóng lơ lửng theo chân.</summary>
        public void Launch()
        {
            if (shadow) shadow.gameObject.SetActive(false);
        }

        public void Despawn()
        {
            if (animator) animator.speed = 1;
            gameObject.SetActive(false);
        }
    }
}
