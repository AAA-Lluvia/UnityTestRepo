using UnityEngine;
using UnityEngine.InputSystem;  // 引入新输入系统命名空间

namespace CameraControl
{
    public class CameraRigControllerIntegrated : MonoBehaviour
    {
        [Header("=== Camera Rig References ===")]
        public Transform cameraTransform;   // 实际 Camera（一般为子物体）
        [Tooltip("可选：作为“面向中心点”的参考/未来扩展。当前移动以rig自身yaw为参考。")]
        public Transform target;

        [Header("=== Providers ===")]
        [Tooltip("建议填 PriorityRotationInputProvider（鼠标优先 + QE低优先级）")]
        public MonoBehaviour rotationProviderBehaviour; // IRotationInputProvider
        public RotationMappingConfig mappingConfig;

        [Header("=== Move (WASD) ===")]
        [Tooltip("基础移动速度（单位/秒）")]
        public float moveSpeed = 5f;

        [Tooltip("按住Shift时的速度倍率")]
        public float fastMultiplier = 2.0f;

        [Tooltip("是否锁定只在水平面移动（推荐 true）")]
        public bool moveOnXZPlaneOnly = true;

        [Header("=== Look (Yaw / Pitch) ===")]
        public float minPitch = -60f;
        public float maxPitch = 75f;
        public float smoothTimeLook = 0.05f;

        [Header("=== Distance Control (reserved) ===")]
        public float minDistance = 0.3f;
        public float maxDistance = 3.0f;
        public float smoothTimeDistance = 0.08f;

        [Header("=== Cursor Lock (dev) ===")]
        public bool lockCursorOnStart = true;

        /* ===== Internal State ===== */
        float yawTarget, pitchTarget;
        float yawCurrent, pitchCurrent;
        float yawVel, pitchVel;

        float distanceTarget = 1.0f;
        float distanceCurrent = 1.0f;
        float distanceVel;

        private IRotationInputProvider RotationProvider => rotationProviderBehaviour as IRotationInputProvider;

        void Start()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Vector3 euler = transform.eulerAngles;
            yawTarget = yawCurrent = euler.y;
            pitchTarget = pitchCurrent = NormalizeAngle(euler.x);

            if (cameraTransform != null)
            {
                distanceCurrent = distanceTarget = cameraTransform.localPosition.magnitude;
            }
        }

        void Update()
        {
            /* =========================================================
             * 1) Move (WASD + Shift)
             *    以当前 yaw 朝向为参考坐标系（忽略pitch），平移 Rig.position
             * ========================================================= */
            HandleMove();

            /* =========================================================
             * 2) Look (Mouse high priority, QE low priority)
             * ========================================================= */
            if (RotationProvider != null && mappingConfig != null)
            {
                Vector2 raw = RotationProvider.GetRawRotation();              // deg/frame
                Vector2 deltaDeg = mappingConfig.MapToDeltaDegrees(raw, Time.deltaTime); // 本方案只做sign/scale

                yawTarget += deltaDeg.x;
                pitchTarget += deltaDeg.y;
            }

            pitchTarget = Mathf.Clamp(pitchTarget, minPitch, maxPitch);

            /* =========================================================
             * 3) Distance clamp（输入不在此绑定，保留接口给手势/IMU）
             * ========================================================= */
            distanceTarget = Mathf.Clamp(distanceTarget, minDistance, maxDistance);
        }

        void LateUpdate()
        {
            /* =========================================================
             * 4) Smooth Look
             * ========================================================= */
            yawCurrent = Mathf.SmoothDampAngle(yawCurrent, yawTarget, ref yawVel, smoothTimeLook);
            pitchCurrent = Mathf.SmoothDampAngle(pitchCurrent, pitchTarget, ref pitchVel, smoothTimeLook);

            transform.rotation = Quaternion.Euler(pitchCurrent, yawCurrent, 0f);

            /* =========================================================
             * 5) Smooth Distance (camera local -Z)
             * ========================================================= */
            distanceCurrent = Mathf.SmoothDamp(distanceCurrent, distanceTarget, ref distanceVel, smoothTimeDistance);

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = new Vector3(0f, 0f, -distanceCurrent);
            }
        }

        private void HandleMove()
        {
            // 使用新输入系统读取键盘输入
            float h = 0f;
            float v = 0f;

            // 获取 WASD 键输入
            if (Keyboard.current.wKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed) v -= 1f;
            if (Keyboard.current.dKey.isPressed) h += 1f;
            if (Keyboard.current.aKey.isPressed) h -= 1f;

            if (Mathf.Abs(h) < 0.001f && Mathf.Abs(v) < 0.001f)
                return;

            bool fast = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            float speed = moveSpeed * (fast ? fastMultiplier : 1f);

            // 只用yaw朝向作为移动参考：forward/right 在水平面
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            if (moveOnXZPlaneOnly)
            {
                forward.y = 0f;
                right.y = 0f;
                forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
                right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            }

            Vector3 move = forward * v + right * h;
            move = move.sqrMagnitude > 1e-6f ? move.normalized : Vector3.zero;

            transform.position += move * speed * Time.deltaTime;
        }

        /* =========================================================
         * 🔌 Public APIs (future IMU / hand gestures)
         * ========================================================= */
        public void AddRotationDelta(float yawDeltaDeg, float pitchDeltaDeg)
        {
            yawTarget += yawDeltaDeg;
            pitchTarget = Mathf.Clamp(pitchTarget + pitchDeltaDeg, minPitch, maxPitch);
        }

        public void AddDistanceDelta(float delta)
        {
            distanceTarget = Mathf.Clamp(distanceTarget + delta, minDistance, maxDistance);
        }

        public void SetDistance(float distance)
        {
            distanceTarget = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
