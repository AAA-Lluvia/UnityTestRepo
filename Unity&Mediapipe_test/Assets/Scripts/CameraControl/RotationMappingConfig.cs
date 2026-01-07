using UnityEngine;

namespace CameraControl
{
    [CreateAssetMenu(menuName = "Camera Control/Rotation Mapping Config", fileName = "RotationMappingConfig")]
    public class RotationMappingConfig : ScriptableObject
    {
        [Header("Axis sign (device -> unified convention)")]
        [Tooltip("统一约定：+yaw = 向右转头")]
        public float yawSign = 1f;

        [Tooltip("统一约定：+pitch = 向上抬头（若你希望向下为正，则设为-1）")]
        public float pitchSign = 1f;

        [Header("Unit scaling")]
        [Tooltip("将raw缩放为“角度增量(度)”比例。当前我们统一deg/frame，通常保持1。")]
        public float unitScale = 1f;

        [Header("Integration (disabled in this scheme)")]
        [Tooltip("本方案要求为 false：Mouse/QE都输出deg/frame，映射层只做sign/scale。")]
        public bool multiplyByDeltaTime = false;

        [Header("Spike clamp (optional)")]
        [Tooltip("限制每帧最大角度变化（度）。0表示不限制。")]
        public float maxDeltaPerFrameDegrees = 0f;

        public Vector2 MapToDeltaDegrees(Vector2 raw, float dt)
        {
            float yawDelta = raw.x * yawSign * unitScale;
            float pitchDelta = raw.y * pitchSign * unitScale;

            // 本方案应保持 false（写在这里是为了未来IMU可复用）
            if (multiplyByDeltaTime)
            {
                yawDelta *= dt;
                pitchDelta *= dt;
            }

            if (maxDeltaPerFrameDegrees > 0f)
            {
                yawDelta = Mathf.Clamp(yawDelta, -maxDeltaPerFrameDegrees, maxDeltaPerFrameDegrees);
                pitchDelta = Mathf.Clamp(pitchDelta, -maxDeltaPerFrameDegrees, maxDeltaPerFrameDegrees);
            }

            return new Vector2(yawDelta, pitchDelta);
        }
    }
}
