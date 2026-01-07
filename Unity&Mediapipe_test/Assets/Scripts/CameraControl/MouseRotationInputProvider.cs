using UnityEngine;
using UnityEngine.InputSystem;  // 引入新输入系统命名空间

namespace CameraControl
{
    public class MouseRotationInputProvider : MonoBehaviour, IRotationInputProvider
    {
        [Header("Mouse sensitivity (deg/frame-ish)")]
        public float sensitivityX = 1.5f;
        public float sensitivityY = 1.5f;

        public string mouseXAxis = "Mouse X";  // 这将被废弃
        public string mouseYAxis = "Mouse Y";  // 这将被废弃

        public Vector2 GetRawRotation()
        {
            // 使用新的 InputSystem 获取鼠标的移动量
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();  // 读取鼠标的相对移动

            // 使用鼠标的 X 和 Y 移动量计算角度
            float yawRaw = mouseDelta.x * sensitivityX;
            float pitchRaw = -mouseDelta.y * sensitivityY;  // 反转 Y 轴的移动

            return new Vector2(yawRaw, pitchRaw);
        }
    }
}
