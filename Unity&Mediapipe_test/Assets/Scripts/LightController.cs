using UnityEngine;

public class LightController : MonoBehaviour
{
    public Light targetLight;

    public float moveStep = 0.3f;    // 平移步长
    public float heightStep = 0.2f;  // 上下步长
    public float rotateStep = 10f;   // 旋转角度（度）

    // 根据右手的 move / rotation 更新光源
    public void UpdateLightFromStatus(OneHandStatus status)
    {
        if (targetLight == null || status == null) return;

        // 位移
        Vector3 pos = targetLight.transform.position;
        switch (status.move)
        {
            case "Up":
                pos += new Vector3(0f, heightStep, 0f);
                break;
            case "Down":
                pos -= new Vector3(0f, heightStep, 0f);
                break;
            case "Left":
                pos += new Vector3(-moveStep, 0f, 0f);
                break;
            case "Right":
                pos += new Vector3(moveStep, 0f, 0f);
                break;
        }
        targetLight.transform.position = pos;

        // 旋转（绕 Y 轴）
        if (status.rotation == "CW")
        {
            targetLight.transform.Rotate(Vector3.up, rotateStep, Space.World);
        }
        else if (status.rotation == "CCW")
        {
            targetLight.transform.Rotate(Vector3.up, -rotateStep, Space.World);
        }
    }
}
