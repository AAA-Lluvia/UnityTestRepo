using UnityEngine;

namespace CameraControl
{
    public interface IRotationInputProvider
    {
        /// <summary>
        /// raw.x = yawRaw, raw.y = pitchRaw
        /// 这里约定统一输出“deg/frame”语义（Mouse天生如此；QE在Provider内部乘dt实现）
        /// </summary>
        Vector2 GetRawRotation();
    }
}
