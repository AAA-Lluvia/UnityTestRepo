using UnityEngine;
using UnityEngine.InputSystem;  // 引入新输入系统命名空间

namespace CameraControl
{
    public class PriorityRotationInputProvider : MonoBehaviour, IRotationInputProvider
    {
        [Header("Providers")]
        public MonoBehaviour primaryProviderBehaviour;   // Mouse
        public MonoBehaviour secondaryProviderBehaviour; // Q/E

        [Header("Priority rule")]
        [Tooltip("当primary输入幅度超过该阈值，本帧忽略secondary（确保鼠标优先）。")]
        public float primaryMagnitudeThreshold = 0.0001f;

        private IRotationInputProvider Primary => primaryProviderBehaviour as IRotationInputProvider;
        private IRotationInputProvider Secondary => secondaryProviderBehaviour as IRotationInputProvider;

        public Vector2 GetRawRotation()
        {
            Vector2 p = Primary != null ? Primary.GetRawRotation() : Vector2.zero;

            // 鼠标输入优先
            if (p.sqrMagnitude > primaryMagnitudeThreshold * primaryMagnitudeThreshold)
                return p;

            // 如果鼠标没有输入，使用 Q/E 输入
            Vector2 s = Secondary != null ? Secondary.GetRawRotation() : Vector2.zero;
            return s;
        }
    }
}
