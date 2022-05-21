using System.Globalization;
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
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
[Serializable]
public class Postion
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public struct Orientation
{
    public float w;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class PoseJSON
{
    public float x;
    public float y;
    public float z;
    public float pitch;
    public float roll;
    public float yaw;
}

public class TargetJSON
{
    public int status;
    public String x;
    public String y;
    public String z;
    public String yaw;
}
public class BasicMovement : MonoBehaviour
{
    // Start is called before the first frame update
    public float deadZoneAmount = 0.5f;
    public float speedxz = 1;
    public float speedy = 0.5f;

    public bool debugMode = false;
    private XRNode leftControllerNode = XRNode.LeftHand;
    private List<UnityEngine.XR.InputDevice> leftInputDevices = new List<UnityEngine.XR.InputDevice>();

    private UnityEngine.XR.InputDevice leftController;
    private XRNode rightControllerNode = XRNode.RightHand;
    private List<UnityEngine.XR.InputDevice> rightInputDevices = new List<UnityEngine.XR.InputDevice>();
    private UnityEngine.XR.InputDevice rightController;

    private Gamepad gamepad;

    public string remoteIP = "localhost";
    public bool isSimulation;
    public GameObject droneObject;
    //public Button takeoffLandButton;

    private Thread clientReceiveThread;
    private TcpClient socketConnection;

    private int status = 0;
    private Vector3 current_orientation = new Vector3(0, 0, 0);
    private Vector3 target_velocity = new Vector3(0, 0, 0);
    private Vector3 target_orientation = new Vector3(0, 0, 0);
    private Vector3 previous_velocity = new Vector3(0, 0, 0);
    private Vector3 previous_orientation = new Vector3(0, 0, 0);

    private int prev_status = 0;

    private static ManualResetEvent connectDone =
        new ManualResetEvent(false);
    private static ManualResetEvent sendDone =
        new ManualResetEvent(false);
    private static ManualResetEvent receiveDone =
        new ManualResetEvent(false);

    private bool change = false;

    private float timePressed = 0f;

    private bool bChange = false;
    private float timePressedB = 0f;






    void Start()
    {
        //Lets get all the devices we can find.
        if (!debugMode)
        {
            GetDevices();
        }
        else
        {
            gamepad = Gamepad.current;
            if (gamepad == null)
            {
                Debug.LogError("No gamepad connected");
                return;
            }
        }

        ConnectToTcpServer();
    }

    void Update()
    {
        if (!debugMode)
        {
            if (leftController == null)
            {
                GetControllerDevices(leftControllerNode, ref leftController, ref leftInputDevices);
            }

            if (rightController == null)
            {
                GetControllerDevices(rightControllerNode, ref rightController, ref rightInputDevices);
            }


            Vector2 leftTouchCoords;
            Vector2 rightTouchCoords;
            bool aButton;
            bool bButton;

            leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftTouchCoords);
            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out rightTouchCoords);
            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out aButton);
            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bButton);

            setStatus(aButton);
            setStatusDebug(bButton);
            setTargetVelocity(leftTouchCoords, rightTouchCoords);
        }
        else
        {
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 rightStick = gamepad.rightStick.ReadValue();

            setTargetVelocity(leftStick, rightStick);
        }

        target_velocity.x = Mathf.Clamp(target_velocity.x, -1.5f, 1.5f);
        target_velocity.z = Mathf.Clamp(target_velocity.z, -1.5f, 1.5f);
        target_velocity.y = Mathf.Clamp(target_velocity.y, -1.5f, 1.5f);
        target_orientation.y = Mathf.Clamp(target_orientation.y, -10f, 10f);
    }


    void setTargetVelocity(Vector2 LStick, Vector2 RStick)
    {

        target_velocity = new Vector3(0, 0, 0);

        Vector3 dir = new Vector3(0, 0, 0);

        if (LStick != Vector2.zero)
        {

            if (LStick.x < -deadZoneAmount)
            {
                //MoveLeft(leftTouchCoords.x);
                dir.x += LStick.x * speedxz;
            }
            else if (LStick.x > deadZoneAmount)
            {
                //MoveRight(leftTouchCoords.x);
                dir.x += LStick.x * speedxz;
            }

            if (LStick.y < -deadZoneAmount)
            {
                //MoveBackward(leftTouchCoords.y);
                dir.z += LStick.y * speedxz;
            }
            else if (LStick.y > deadZoneAmount)
            {
                //MoveForward(leftTouchCoords.y);
                dir.z += LStick.y * speedxz;
            }
        }

        if (RStick != Vector2.zero)
        {
            if (RStick.x < -deadZoneAmount)
            {
                //RotateLeft(rightTouchCoords.x);
                target_orientation.y += 10f;
            }
            else if (RStick.x > deadZoneAmount)
            {
                //RotateRight(rightTouchCoords.x);
                target_orientation.y -= 10f;
            }

            if (RStick.y < -deadZoneAmount)
            {
                //MoveDown(rightTouchCoords.y);
                dir.y -= RStick.y * speedy;
            }
            else if (RStick.y > deadZoneAmount)
            {
                //MoveUp(rightTouchCoords.y);
                dir.y -= RStick.y * speedy;
            }
        }

        target_velocity.Set(dir.x, dir.y, dir.z);
        // Debug.Log("Target Velocity ---> " + target_velocity);
    }

    void setStatus(bool aButton)
    {

        if(aButton){
            timePressed = Time.time;
            change = true;
        }
        else{
            if(Time.time - timePressed > 0.1 && change){
                Debug.Log("A Button Pressed");
                status = (status == 1)? 0:1;
                change = false;
            }
        }
    }

    void setStatusDebug(bool bButton){
        if(bButton){
            timePressedB = Time.time;
            bChange = true;
        }
        else{
            if(Time.time - timePressedB > 0.1 && bChange){
                Debug.Log("B Button pressed");
                bChange = false;
            }
        }
    }

    void GetDevices()
    {
        //Gets the Right Controller Devices
        GetControllerDevices(leftControllerNode, ref leftController, ref leftInputDevices);

        //Gets the Right Controller Devices
        GetControllerDevices(rightControllerNode, ref rightController, ref rightInputDevices);


        Debug.Log(string.Format("Device name '{0}' with characteristics '{1}'", leftController.name, leftController.characteristics));

        Debug.Log(string.Format("Device name '{0}' with characteristics '{1}'", rightController.name, rightController.characteristics));

    }

    void GetControllerDevices(XRNode controllerNode, ref UnityEngine.XR.InputDevice controller, ref List<UnityEngine.XR.InputDevice> inputDevices)
    {
        Debug.Log("Get devices is called");
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(controllerNode, inputDevices);

        if (inputDevices.Count == 1)
        {
            controller = inputDevices[0];
            Debug.Log(string.Format("Device name '{0}' with characteristics '{1}'", controller.name, controller.characteristics));
        }

        if (inputDevices.Count > 1)
        {
            Debug.LogAssertion("More than one device found with the same input characteristics");
        }
    }

    private void ConnectToTcpServer()
    {
        try
        {
            clientReceiveThread = new Thread(new ThreadStart(ListenForData));
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.Log("On client connect exception " + e);
        }
    }



    // private void sendDataToServer()
    // {
    //     IPHostEntry ipHostInfo = Dns.GetHostEntry(remoteIP);
    //     IPAddress ipAddress = ipHostInfo.AddressList[0];
    //     IPEndPoint remoteEP = new IPEndPoint(ipAddress, 13580);

    //     // Create a TCP/IP socket.  
    //     Socket client = new Socket(ipAddress.AddressFamily,
    //         SocketType.Stream, ProtocolType.Tcp);

    //     // Connect to the remote endpoint.  
    //     client.BeginConnect(remoteEP,
    //         new AsyncCallback(ConnectCallback), client);
    //     connectDone.WaitOne();

    //     // Send test data to the remote device.
    //     while (true)
    //     {
    //         if (Vector3.Distance(target_velocity, previous_velocity) != 0 || Vector3.Distance(target_orientation, previous_orientation) != 0 || status != prev_status)
    //         {
    //             previous_velocity.Set(target_velocity.x, target_velocity.y, target_velocity.z);
    //             previous_orientation.Set(target_orientation.x, target_orientation.y, target_orientation.z);
    //             prev_status = status;
    //             TargetJSON t = new TargetJSON();
    //             t.status = status;
    //             t.x = target_velocity.x;
    //             t.y = target_velocity.y;
    //             t.z = -target_velocity.z;
    //             Debug.Log("SENDING");
    //             Send(client, "{\"status\":\"0\", \"x\":\"" + target_velocity.x +"\", \"y\":\"" + target_velocity.y +"\", \"z\":\"" + target_velocity.z +"\", \"yaw\":\"" + target_orientation.y +"\"}");
    //             sendDone.WaitOne();
    //         }
            
    //     }

    // }

    // private static void Send(Socket client, String data)
    // {
    //     // Convert the string data to byte data using ASCII encoding.  
    //     byte[] byteData = Encoding.ASCII.GetBytes(data);

    //     // Begin sending the data to the remote device.  
    //     client.BeginSend(byteData, 0, byteData.Length, 0,
    //         new AsyncCallback(SendCallback), client);
    // }

    // private static void SendCallback(IAsyncResult ar)
    // {
    //     try
    //     {
    //         // Retrieve the socket from the state object.  
    //         Socket client = (Socket)ar.AsyncState;

    //         // Complete sending the data to the remote device.  
    //         int bytesSent = client.EndSend(ar);
    //         Console.WriteLine("Sent {0} bytes to server.", bytesSent);

    //         // Signal that all bytes have been sent.  
    //         sendDone.Set();
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine(e.ToString());
    //     }
    // }


    // private static void ConnectCallback(IAsyncResult ar)
    // {
    //     try
    //     {
    //         // Retrieve the socket from the state object.  
    //         Socket client = (Socket)ar.AsyncState;

    //         // Complete the connection.  
    //         client.EndConnect(ar);

    //         Console.WriteLine("Socket connected to {0}",
    //             client.RemoteEndPoint.ToString());

    //         // Signal that the connection has been made.  
    //         connectDone.Set();
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine(e.ToString());
    //     }
    // }

    private void ListenForData()
    {
        try
        {
            socketConnection = new TcpClient(remoteIP, 13580);
            socketConnection.NoDelay = true;

            if (socketConnection.Connected)
                Debug.Log("TCP Server connected.");
            while (true)
            {
                
                using (NetworkStream stream = socketConnection.GetStream())
                {
                    // Read incomming stream into byte arrary.
                    while (true)
                    {
                        
                        // Debug.Log("Distance Velocity: " + Vector3.Distance(target_velocity, previous_velocity) + " Distance Orientation: ");
                        // yield return new WaitForSeconds(5);
                        // target_velocity.x= Mathf.Round(target_velocity.x * 100f) * 0.01f;
                        // target_velocity.y = Mathf.Round(target_velocity.y * 100f) * 0.01f;
                        // target_velocity.z = - Mathf.Round(target_velocity.z* 100f) * 0.01f;
                        // target_orientation.y = Mathf.Round((target_orientation.y / 180f) * Mathf.PI * 100f) * 0.01f;
                        if (Vector3.Distance(target_velocity, previous_velocity) != 0 || Vector3.Distance(target_orientation, previous_orientation) != 0 || status != prev_status)
                        {
                            // Debug.Log("Sending Data");
                            previous_velocity.Set(target_velocity.x, target_velocity.y, target_velocity.z);
                            previous_orientation.Set(target_orientation.x, target_orientation.y, target_orientation.z);
                            prev_status = status;
                            try
                            {
                                // Debug.Log(target_velocity.x + " " + target_velocity.y + " " + target_velocity.z);
                                TargetJSON t = new TargetJSON();
                                t.status = status;
                                // t.x =  Mathf.RoundToInt(target_velocity.x * 100f) * 0.01f;
                                if (target_velocity.x.ToString().Length >= 4){
                                    t.x = target_velocity.x.ToString().Substring(0,4);
                                }else{
                                    t.x = target_velocity.x.ToString();
                                }
                                if (target_velocity.y.ToString().Length >= 4){
                                    t.y = target_velocity.y.ToString().Substring(0,4);
                                }else{
                                    t.y = target_velocity.y.ToString();
                                }
                                if (target_velocity.z.ToString().Length >= 4){
                                    t.z = target_velocity.z.ToString().Substring(0,4);
                                }else{
                                    t.z = target_velocity.z.ToString();
                                }
                                if(((target_orientation.y / 180f) * Mathf.PI).ToString().Length >= 4) {
                                    t.yaw = ((target_orientation.y / 180f) * Mathf.PI).ToString().Substring(0,4);
                                } else {
                                    t.yaw = ((target_orientation.y / 180f) * Mathf.PI).ToString();
                                }
                                
                                // t.yaw = Mathf.RoundToInt((target_orientation.y / 180f) * Mathf.PI * 100f) * 0.01f;
                                //t.yaw = - (float) Math.Round((double) target_orientation.y, 2);
                                Debug.Log(JsonUtility.ToJson(t));
                                // Debug.Log(JsonUtility.ToJson(t));

                                Byte[] send_msg = Encoding.UTF8.GetBytes(JsonUtility.ToJson(t));
                                // Byte[] send_msg = Encoding.UTF8.GetBytes("RECV");
                                stream.Write(send_msg, 0, send_msg.Length);

                                // Buffer to store the response bytes.
                                // Byte[] data = new Byte[256];

                                // // String to store the response ASCII representation.
                                // String responseData = String.Empty;

                                // // Read the first batch of the TcpServer response bytes.

                                // Int32 bytes = stream.Read(data, 0, data.Length);
                                // responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);

                                // Debug.Log("Received: "+ responseData);

                                // Thread.Sleep(10);

                                // Array.Clear(bytes, 0, bytes.Length);
                                //stream.Read(bytes, 0, bytes.Length);
                                //string json_str = Encoding.UTF8.GetString(bytes);
                                // print(json_str);

                                //need to create separate socket that gets updated data from location and update
                                /*PoseJSON p = JsonUtility.FromJson<PoseJSON>(json_str);
                                current_position.x = p.x;
                                current_position.y = p.y;
                                current_position.z = -p.z;
                                current_orientation.x = -p.pitch * 180f / Mathf.PI;
                                current_orientation.y = p.yaw * 180f / Mathf.PI;
                                current_orientation.z = p.roll * 180f / Mathf.PI;*/
                            }
                            catch (Exception e)
                            {
                                stream.Flush();
                                Debug.Log(e);
                            }

                        }
                        // Thread.Sleep(10);
                    }
                }
            }
        }

        catch (SocketException socketException)
        {
            Debug.Log("Socket exception: " + socketException);
        }
    }

    void OnDestroy()
    {
        clientReceiveThread.Abort();
        Debug.Log("OnDestroy1");
    }
}
