﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// This class takes the users input and controls the cameras interactions.
/// The users can rotate the camera or move its position to a new target location.
/// </summary>
public class CameraController : MonoBehaviour
{

    /// <summary>
    /// This is the public info for the cameras rotation.
    /// </summary>
    public Transform camYaw;
    public Transform camPitch;
    private bool invertLookY = true;

    /// <summary>
    /// lerpSpeed is the multiplier for deltaTime. 
    /// </summary>
    private float lerpSpeed;

    /// <summary>
    /// This is the public sensitivity info. It should be updated in the inspector.
    /// </summary>
    private float MouseSensitivityX = 4;
    private float MouseSensitivityY = 4;
    private float MouseSensitivityScroll = 2;
    private float MobileSensitivity = 12;
    private float MobileSensitivityScroll = 60;

    /// <summary>
    /// This is our max and min zoom values. It should be updated in the inspector.
    /// </summary>
    private float maxZoom = 20;
    private float minZoom = 1;

    /// <summary>
    /// This is the timer for the auto orbit feature. If there is not input for this amout of time the camera system will begin to spin.
    /// </summary>
    private float autoOrbitTimer;
    private float orbitSpeed;
    private float currentOrbitTimer = 0;
    private bool autoOrbit = false;

    /// <summary>
    /// This is the private info such as the moues starting position, the pitch, yaw, and scroll amounts.
    /// </summary>
    float camPitchAmount;
    float yaw;
    float scroll;

    /// <summary>
    /// This is the desired yaw, pitch, and zoom that is set when a user updated the POI.
    /// </summary>
    private float desiredYaw;
    private float desiredPitch;
    private float desiredZoom;

    /// <summary>
    /// allowCameraUpdate will tell the script if we should run or stop the Lerps and Slerps.
    /// </summary>
    private bool allowCameraUpdate = false;

    /// <summary>
    /// This is the previous distance between the users fingers on mobile
    /// </summary>
    private float prevDistance = 0;

    private Manager manager;

    /// <summary>
    /// In this start function we set our max and min zoom to the negative version of the public version.
    /// </summary>
    void Start()
    {

        manager = GameObject.FindGameObjectWithTag("Manager").GetComponent<Manager>();
        if (manager == null) print("The Manager object is missing or broken!");
        else
        {
            invertLookY = manager.invertInput;
            lerpSpeed = manager.cameraRotateSpeed;
            MouseSensitivityX = manager.mouseSensitivityX;
            MouseSensitivityY = manager.mouseSensitivityY;
            MouseSensitivityScroll = manager.scrollSensitivity;
            MobileSensitivity = manager.mobileSensitivity;
            MobileSensitivityScroll = manager.mobileSensitivityZoom;
            maxZoom = manager.maxZoom;
            minZoom = manager.minZoom;
            autoOrbitTimer = manager.autoOrbitTimout;
            orbitSpeed = manager.autoOrbitSpeed;
        }


        maxZoom *= -1;
        minZoom *= -1;
    }

    /// <summary>
    /// In this update funciton we call the MouseInput function every update and the UpdateCamera funciton if we are currently allowing camera updates.
    /// </summary>
    void Update()
    {
        MouseInput();
        MobileInput();
        HandleZoom();
        MobileZoom();
        if (allowCameraUpdate) { UpdateCamera(); }
        autoOrbitCountdown();
        if (autoOrbit) { orbitCamera(); }
    }

    /// <summary>
    /// In this MouseInput method we start by settinog our scroll float to the axis of our mouses scrollwheel.
    /// Then we set the localposition to the localPosition.z + our scroll offset * our mouse scroll sensitivity if the user gave us scroll input.
    /// We have to use a local position so we don't mess with the parent object.
    /// 
    /// Next we check to see if we are outside our upper or lower bounds for our zoom.
    /// 
    /// Then we get our left mouse click input, if the user has clicked it means they might be trying to rotate the camera.
    /// We set our PrevY and PrevZ to the camPitches previous euler angels for Y and Z, we will use these to reset if the user tries to exceed the max pitch.
    /// 
    /// Next we get our yaw input from our Mouse X and our pitch input from the Mouse Y values. If the mouse is inverted we invert it here.
    /// Then we clap the pitch amount.
    /// 
    /// To rotate we call our camYaw and camPitch objects and tell them to rotate. Then we make sure that the min and max pitch values are within our preset limits.
    /// We also limiti the camPitches Z angle to 0, if it ever becomes somthing other than 0 we reset it back to 0, if we don't do this the camera will break and the
    /// user would be upside down. They wern't acutally upside down the camera just rolled enough to appear upsided down.
    /// </summary>
    void MouseInput()
    {
        if (Input.GetButton("Fire1"))
        {
            yaw = Input.GetAxis("Mouse X");
            camPitchAmount = Input.GetAxis("Mouse Y") * MouseSensitivityY * (invertLookY ? -1 : 1);

            HandleOrbit(yaw, camPitchAmount);
        }

        LimitOrbit();
    }

    void HandleZoom()
    {
        scroll = Input.GetAxis("Mouse ScrollWheel");
        //print(scroll);
        if (scroll != 0)
        {
            allowCameraUpdate = false;
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z + scroll * MouseSensitivityScroll);
            LimitZoom();
            cancleAutoOrbit();
        }
    }

    /// <summary>
    /// This funciton will limit the minimum and maximum zoom.
    /// </summary>
    void LimitZoom()
    {
        if (transform.localPosition.z > minZoom)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, minZoom);
        }

        if (transform.localPosition.z < maxZoom)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, maxZoom);
        }
    }

    void MobileInput()
    {
        if(Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {
            yaw = Input.GetTouch(0).deltaPosition.x / MobileSensitivity * -1;
            camPitchAmount = Input.GetTouch(0).deltaPosition.y / MobileSensitivity * MouseSensitivityY * (invertLookY ? -1 : 1) * -1;
            
            HandleOrbit(yaw, camPitchAmount);
        }

        LimitOrbit();
    }


    void MobileZoom()
    {
        if (Input.touchCount == 2 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {
            Vector2 point1 = Input.GetTouch(0).position;
            Vector2 point2 = Input.GetTouch(1).position;

            //Calculate the difference between the positions
            float difference = Vector2.Distance(point1, point2);

            if(prevDistance == 0)
            {
                prevDistance = difference;
                return;
            }else
            {
                float zoomAmount = difference - prevDistance;
                zoomAmount = zoomAmount / MobileSensitivityScroll;
                allowCameraUpdate = false;
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z + zoomAmount);
                LimitZoom();
                cancleAutoOrbit();
            }

            prevDistance = difference;
        }else
        {
            prevDistance = 0;
        }
    }

    void HandleOrbit(float yaw, float pitchAmount)
    {
        allowCameraUpdate = false;

        pitchAmount = Mathf.Clamp(pitchAmount, -10, 80);

        camYaw.Rotate(0, yaw * MouseSensitivityX, 0);
        camPitch.Rotate(pitchAmount, 0, 0);

        cancleAutoOrbit();
    }

    void LimitOrbit()
    {
        //This limits the pitch to a minimum preset angel
        if (camPitch.rotation.eulerAngles.x < 1 || camPitch.rotation.eulerAngles.x > 300)
        {
            camPitch.eulerAngles = new Vector3(1, camPitch.rotation.eulerAngles.y, camPitch.rotation.eulerAngles.z);
        }

        //This limits the pitch to a preset angle
        if (camPitch.rotation.eulerAngles.x > 75)
        {
            camPitch.eulerAngles = new Vector3(75, camPitch.rotation.eulerAngles.y, camPitch.rotation.eulerAngles.z);
        }

        //This will prevent the camera from breaking and going past the max angle pitch angle
        if (camPitch.rotation.eulerAngles.z != 0)
        {
            camPitch.eulerAngles = new Vector3(camPitch.rotation.eulerAngles.x, camPitch.rotation.eulerAngles.y, 0);
        }
    }

    /// <summary>
    /// In this MoveToPosition function we take in a new yaw, pitch, and zoom value. We then set our desired yaw, pitch, and zoom floats. We also set
    /// allowCameraUpdate to true, this will allow the UpdateCamera function to run.
    /// </summary>
    /// <param name="yaw">This value (float) is the desired yaw position.</param>
    /// <param name="pitch">This value (float) is the desired pitch position.</param>
    /// <param name="zoom">This value (float) is the desired zoom position.</param>
    public void MoveToPosition(float yaw, float pitch,float zoom)
    {
        desiredYaw = yaw;
        desiredPitch = pitch;
        desiredZoom = zoom * -1;
        allowCameraUpdate = true;
        cancleAutoOrbit();
    }

    /// <summary>
    /// This function will lerp the camera into the correct position.
    /// </summary>
    void UpdateCamera()
    {
        camYaw.rotation = Quaternion.Slerp(Quaternion.Euler(0, camYaw.rotation.eulerAngles.y, 0), Quaternion.Euler(0, desiredYaw, 0), Time.deltaTime * lerpSpeed);
        camPitch.rotation = Quaternion.Slerp(Quaternion.Euler(camPitch.rotation.eulerAngles.x, 0, 0), Quaternion.Euler(desiredPitch, 0, 0), Time.deltaTime * lerpSpeed);
        camPitch.localEulerAngles = new Vector3(camPitch.localEulerAngles.x, 0, 0);
        transform.localPosition = Vector3.Lerp(transform.localPosition, new Vector3(0, 0, desiredZoom), Time.deltaTime * lerpSpeed);
    }

    /// <summary>
    /// This function is called when you want to cancle the auto orbit. It should be called when the user provides any type of input.
    /// </summary>
    private void cancleAutoOrbit()
    {
        autoOrbit = false;
        currentOrbitTimer = 0;
    }

    /// <summary>
    /// This function is called every update, it will add to the timer and check to see if we should enable the auto orbit feature.
    /// </summary>
    private void autoOrbitCountdown()
    {
        currentOrbitTimer += Time.deltaTime;
        //print(currentOrbitTimer);
        if (currentOrbitTimer >= autoOrbitTimer) autoOrbit = true;
    }

    /// <summary>
    /// This function is called every update when the auto orbit feature is enabled. It spins the camera counter-clockwise based on the orbitSpeed's value.
    /// </summary>
    private void orbitCamera()
    {
        camYaw.rotation = Quaternion.Slerp(Quaternion.Euler(0, camYaw.rotation.eulerAngles.y, 0), Quaternion.Euler(0, camYaw.rotation.eulerAngles.y - 1, 0), Time.deltaTime * orbitSpeed);
        LimitOrbit();
    }
}
