using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// 对应 Python JSON 中每只手的数据
/// </summary>
[Serializable]
public class OneHandStatus
{
    public string move;       // "Up" / "Down" / "Left" / "Right" / "Still"
    public string gesture;    // "Fist" / "Pinch" / "OpenPalm" / "Other" / "None"
    public string rotation;   // "CCW" / "CW" / "Still"
    public float cie_x;
    public float cie_y;
    public int[] rgb;         // [r,g,b] 0-255
    public bool locked;       // 由 JSON 的 "lock" 映射而来
}

/// <summary>
/// 整个 JSON：{"Left": {...}, "Right": {...}}
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

    [Header("最新收到的状态")]
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
                // Python 中字段叫 "lock"，C# 中字段是 locked
                json = json.Replace("\"lock\":", "\"locked\":");

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

    void OnApplicationQuit()
    {
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }
}
