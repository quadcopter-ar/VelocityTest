using System.Net;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using System;
using System.Threading;
using System.Net.Sockets;
using System.Text;
using UnityEngine.UI;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    // Start is called before the first frame update
    public bool debugMode = false;

    public string remoteIP;
    public GameObject droneObject;

    private Thread clientRecieveThread;
    private TcpClient socketConnection;

    private Vector3 target_position = new Vector3(0, 0, 0);
    private Vector3 target_orientation = new Vector3(0, 0, 0);


    void Start() {
        ConnectToTcpServer();
    }

    void Update() {
        droneObject.transform.position = target_position;
        droneObject.transform.eulerAngles = target_orientation;
    }


    private void ConnectToTcpServer() {
        try {
            clientRecieveThread = new Thread(new ThreadStart(ListenForData));
            clientRecieveThread.IsBackground = true;
            clientRecieveThread.Start();
        }
        catch(Exception e) {
            Debug.Log("Error: Connection Error occured on TCP link.");
        }
    }

    private void ListenForData()
    {
        try{
            socketConnection = new TcpClient(remoteIP, 13579);
            
            if(socketConnection.Connected)
                Debug.Log("Error: TCP Server Connected.");
            while(true){
                using(NetworkStream stream = socketConnection.GetStream()){
                    Byte[] bytes = new byte[1024];

                    while(true) {
                        try{
                            stream.Read(bytes, 0, bytes.Length);
                            string json_str = Encoding.UTF8.GetString(bytes);
                            PoseJSON p = JsonUtility.FromJson<PoseJSON>(json_str);
                            target_position.x = p.x;
                            target_position.y = p.y;
                            target_position.z = -p.z;
                            target_orientation.x = -p.pitch * 180f / Mathf.PI;
                            target_orientation.y = p.yaw * 180f / Mathf.PI;
                            target_orientation.z = p.roll * 180f / Mathf.PI;
                        }
                        catch (Exception e) {
                            stream.Flush();
                        }
                    }
                }
            }
        }
        catch (SocketException socketException) {
            Debug.Log("Socket exception: " + socketException);
        }
    }

    void OnDestroy() {
        clientRecieveThread.Abort();
        Debug.Log("OnDestroy1");
    }
}
