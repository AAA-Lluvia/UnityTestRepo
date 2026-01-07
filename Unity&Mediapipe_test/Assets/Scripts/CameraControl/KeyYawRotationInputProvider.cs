using UnityEngine;
using UnityEngine.InputSystem;  // 引入新输入系统命名空间

namespace CameraControl
{
    public class KeyYawRotationInputProvider : MonoBehaviour, IRotationInputProvider
    {
        [Header("Q/E yaw control")]
        [Tooltip("deg per second（在本Provider内部乘dt，输出deg/frame）")]
        public float yawSpeedDegPerSec = 90f;

        public KeyCode keyLeft = KeyCode.Q;
        public KeyCode keyRight = KeyCode.E;

        public Vector2 GetRawRotation()
        {
            // 使用新输入系统检查 Q/E 键的状态
            float dir = 0f;
            if (Keyboard.current[Key.Q].isPressed) dir -= 1f;  // 新输入系统读取按键
            if (Keyboard.current[Key.E].isPressed) dir += 1f;  // 新输入系统读取按键

            // 计算 yaw 增量（deg/frame）
            float yawDeltaDeg = dir * yawSpeedDegPerSec * Time.deltaTime;

            return new Vector2(yawDeltaDeg, 0f);
        }
    }
}
