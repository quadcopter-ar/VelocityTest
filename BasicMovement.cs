using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System;
using System.Threading;
using System.Text;
using System.Net.Sockets;
using System.Linq;

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
    public float x;
    public float y;
    public float z;
    public float yaw;
}

public class BasicMovement : MonoBehaviour
{
    float deadZoneAmount = 0.5f;
    public float speedxz = 1;
    public float speedy = 0.5;

    public bool debugMode = false;
    private XRNode leftControllerNode = XRNode.LeftHand;
    private List<UnityEngine.XR.InputDevice> leftInputDevices = new List<UnityEngine.XR.InputDevice>();

    private UnityEngine.XR.InputDevice leftController;
    private XRNode rightControllerNode = XRNode.RightHand;
    private List<UnityEngine.XR.InputDevice> rightInputDevices = new List<UnityEngine.XR.InputDevice>();
    private UnityEngine.XR.InputDevice rightController;

    private Gamepad gamepad;

    public string remoteIP;
    public bool isSimulation;
    public GameObject droneObject;
    public Button takeoffLandButton;

    private Thread clientReceiveThread;
    private TcpClient socketConnection;

    private int status = 0;

    private Vector3 current_position = new Vector3(0, 0, 0);
    private Vector3 current_orientation = new Vector3(0, 0, 0);
    private Vector3 target_displacement = new Vector3(0, 0, 0);
    private Vector3 target_position = new Vector3(0, 1, 0);
    private Vector3 target_velocity = new Vector3(0, 0, 0);
    private Vector3 target_orientation = new Vector3(0, 0, 0);






    void Start() {
        //Lets get all the devices we can find.
        if(!debugMode){
          GetDevices();
        }else{
          gamepad = Gamepad.current;
          if(gamepad == null){
            Debug.LogError("No gamepad connected");
            return;
          }
        }

        ConnectToTcpServer();
    }

    void Update() {
        if(!debugMode){
            if (leftController == null) {
                GetControllerDevices(leftControllerNode, ref leftController, ref leftInputDevices);
            }

            if (rightController == null) {
                GetControllerDevices(rightControllerNode, ref rightController, ref rightInputDevices);
            }


            Vector2 leftTouchCoords;
            Vector2 rightTouchCoords;

            leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftTouchCoords);
            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out rightTouchCoords);

            setTargetVelocity(leftTouchCoords, rightTouchCoords);
        }else{
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 rightStick = gamepad.rightStick.ReadValue();

            setTargetVelocity(leftStick, rightStick);
        }

        target_velocity.x = Mathf.Clamp(target_velocity.x, -1.5f, 1.5f);
        target_velocity.z = Mathf.Clamp(target_velocity.z, -0.5f, 0.5f);
        target_velocity.y = Mathf.Clamp(target_velocity.y, 0.5f, 1.5f);
        target_orientation.y = 0f;
    }


    void setTargetVelocity(Vector2 LStick, Vector2 RStick) {
        
        target_postion = new Vector3(current_position.x, current_position.y, current_position.z);
        target_velocity = new Vector3(0, 0, 0);

        target_orientation = new Vector3(current_orientation.x, current_orientation.y, current_orientation.z);

        Vector3 dir = new Vector3(0, 0, 0);

        if (LStick != Vector2.zero)
        {

            if(LStick.x < -deadZoneAmount){
              //MoveLeft(leftTouchCoords.x);
              dir -= LStick.x * speedxz;
            }else if(LStick.x > deadZoneAmount){
              //MoveRight(leftTouchCoords.x);
              dir += LStick.x * speedxz;
            }

            if (LStick.y < -deadZoneAmount) {
                //MoveBackward(leftTouchCoords.y);
                dir -= LStick.y * speedxz;
            } else if (LStick.y > deadZoneAmount) {
                //MoveForward(leftTouchCoords.y);
                dir += LStick.y * speedxz;
            }
        }

        if (RStick != Vector2.zero)
        {
            if(RStick.x < -deadZoneAmount){
              //RotateLeft(rightTouchCoords.x);
              target_orientation.y += 10f;
            }else if(RStick.x > deadZoneAmount){
              //RotateRight(rightTouchCoords.x);
              target_orientation.y -= 10f;
            }

            if (RStick.y < -deadZoneAmount) {
              //MoveDown(rightTouchCoords.y);
              dir -= RStick.y * speedy;
            } else if (RStick.y > deadZoneAmount) {
              //MoveUp(rightTouchCoords.y);
              dir += rightTouchCoords.y * speedy;
            }
        }
    }



/*
    void MoveForward(float input){
      MoveObject(Vector3.forward * input * Time.deltaTime * speed);
    }
    void MoveBackward(float input){
      MoveObject(Vector3.back * input * Time.deltaTime * -speed);
    }

    void MoveLeft(float input){
      MoveObject(Vector3.left* input * Time.deltaTime * -speed);
    }

    void MoveRight(float input){
      MoveObject(Vector3.right * input * Time.deltaTime * speed);
    }

    void MoveUp(float input){
      MoveObject(Vector3.up * input * Time.deltaTime * speed);
    }

    void MoveDown(float input){
      MoveObject(Vector3.down * input * Time.deltaTime * -speed);
    }

    void RotateLeft(float input){
      RotateObject(Vector3.up * input * Time.deltaTime * speed*10);
    }

    void RotateRight(float input){
      transform.Rotate(Vector3.down * input * Time.deltaTime * -speed * 10);
    }
    void MoveObject(Vector3 tranlation){
      transform.Translate(tranlation);
    }

    void RotateObject(Vector3 rotation){
      transform.Rotate(rotation);
    }
*/
    void GetDevices() {
        //Gets the Right Controller Devices
        GetControllerDevices(leftControllerNode, ref leftController, ref leftInputDevices);

        //Gets the Right Controller Devices
        GetControllerDevices(rightControllerNode, ref rightController, ref rightInputDevices);


        Debug.Log(string.Format("Device name '{0}' with characteristics '{1}'", leftController.name, leftController.characteristics));

        Debug.Log(string.Format("Device name '{0}' with characteristics '{1}'", rightController.name, rightController.characteristics));

    }

    void GetControllerDevices(XRNode controllerNode, ref UnityEngine.XR.InputDevice controller,ref List<UnityEngine.XR.InputDevice> inputDevices) {
        Debug.Log("Get devices is called");
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(controllerNode, inputDevices);

        if (inputDevices.Count == 1){
            controller = inputDevices[0];
            Debug.Log(string.Format("Device name '{0}' with characteristics '{1}'", controller.name, controller.characteristics));
        }

        if (inputDevices.Count > 1) {
            Debug.LogAssertion("More than one device found with the same input characteristics");
        }
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

