using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// 对应 JSON 中 Left / Right 每只手的数据
/// </summary>
[Serializable]
public class OneHandStatus
{
    public string move;       // "Up" / "Down" / "Left" / "Right" / "Still"
    public string gesture;    // "OpenPalm" / "Pinch" / "Fist" / "None"
    public string rotation;   // "CCW" / "CW" / "Still"
    public float cie_x;       // 可能是 0 或未设置
    public float cie_y;
    public int[] rgb;         // [r,g,b] 0-255
    public bool lockColor;    // 由 JSON 中的 "lock" 映射而来
}

/// <summary>
/// 整个 hand_status：有 Left / Right 两只手
/// </summary>
[Serializable]
public class HandStatusMessage
{
    public OneHandStatus Left;
    public OneHandStatus Right;
}

public class UdpHandReceiver : MonoBehaviour
{
    [Header("UDP 设置")]
    public int listenPort = 5005;

    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;

    [Header("最新收到的 HandStatus")]
    public HandStatusMessage latestStatus = new HandStatusMessage();

    void Start()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            udpClient.BeginReceive(ReceiveCallback, null);
            Debug.Log("UDP 监听已启动，端口 = " + listenPort);
        }
        catch (Exception e)
        {
            Debug.LogError("UDP 监听启动失败: " + e);
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        if (udpClient == null) return;

        try
        {
            byte[] bytes = udpClient.EndReceive(ar, ref remoteEndPoint);
            string json = Encoding.UTF8.GetString(bytes);

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("收到空 JSON");
            }
            else
            {
                // 把 Python 的 "lock" 映射成 C# 里的 "lockColor"
                json = json.Replace("\"lock\":", "\"lockColor\":");

                HandStatusMessage msg = JsonUtility.FromJson<HandStatusMessage>(json);

                if (msg == null)
                {
                    Debug.LogWarning("JsonUtility.FromJson 返回 null，JSON = " + json);
                }
                else
                {
                    if (msg.Left == null) msg.Left = new OneHandStatus();
                    if (msg.Right == null) msg.Right = new OneHandStatus();
                    latestStatus = msg;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("UDP 接收或 JSON 解析出错: " + e);
        }
        finally
        {
            if (udpClient != null)
                udpClient.BeginReceive(ReceiveCallback, null);
        }
    }

    void Update()
    {
        // 简单打印一下，确认数据在更新（可以之后关掉）
        if (latestStatus != null && latestStatus.Left != null)
        {
            Debug.Log(
                $"[Left] gesture={latestStatus.Left.gesture}, " +
                $"move={latestStatus.Left.move}, " +
                $"CIE=({latestStatus.Left.cie_x:F3},{latestStatus.Left.cie_y:F3}), " +
                $"RGB=({(latestStatus.Left.rgb != null && latestStatus.Left.rgb.Length >= 3 ? $"{latestStatus.Left.rgb[0]},{latestStatus.Left.rgb[1]},{latestStatus.Left.rgb[2]}" : "null")})"
            );
        }
    }

    void OnApplicationQuit()
    {
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }
}
