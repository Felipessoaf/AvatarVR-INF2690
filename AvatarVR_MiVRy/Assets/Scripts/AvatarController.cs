/*
 * Advaced Gesture Recognition - Unity Plug-In
 * 
 * Copyright (c) 2019 MARUI-PlugIn (inc.)
 * This software is free to use for non-commercial purposes.
 * You may use this software in part or in full for any project
 * that does not pursue financial gain, including free software 
 * and projectes completed for evaluation or educational purposes only.
 * Any use for commercial purposes is prohibited.
 * You may not sell or rent any software that includes
 * this software in part or in full, either in it's original form
 * or in altered form.
 * If you wish to use this software in a commercial application,
 * please contact us at support@marui-plugin.com to obtain
 * a commercial license.
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS 
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
 * THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
 * PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, 
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR 
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY 
 * OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using UnityEngine.XR;


public class AvatarController : MonoBehaviour
{
    // Convenience ID's for the "left" and "right" sub-gestures.
    public const int Side_Left = 0;
    public const int Side_Right = 1;

    // The file from which to load gestures on startup (left hand).
    // For example: "Assets/GestureRecognition/Sample_TwoHanded_Gestures.dat"
    [SerializeField] private string LoadGesturesFile;
    
    // Tolerance for averaged controller motion to detect if the controller is still moving.
    [SerializeField] private double ControllerMotionDistanceThreshold;

    // Time the controller must be held still for averaged controller motion to detect if the controller is still moving.
    [SerializeField] private double ControllerMotionTimeThreshold;

    [Space]
    public GameObject RockUp;

    // Averaged controller motion (distance).
    private double controller_motion_distance_left = 0;
    private double controller_motion_distance_right = 0;

    // Timestamp when controller motion was last detected (if pressing the trigger button).
    private System.DateTime controller_motion_time_left = System.DateTime.Now;
    private System.DateTime controller_motion_time_right = System.DateTime.Now;

    // Short-term storage of controller positions to calculate motion.
    private Vector3 controller_motion_last_left = new Vector3(0, 0, 0);
    private Vector3 controller_motion_last_right = new Vector3(0, 0, 0);

    // Whether the user is currently pressing the contoller trigger.
    private bool trigger_pressed_left = false;
    private bool trigger_pressed_right = false;

    // Wether a gesture was already started
    private bool gesture_started = false;

    // The gesture recognition object for bimanual gestures.
    private GestureCombinations gc = new GestureCombinations(2);

    // The text field to display instructions.
    private Text HUDText;

    // ID of the gesture currently being recorded,
    // or: -1 if not currently recording a new gesture,
    // or: -2 if the AI is currently trying to learn to identify gestures
    // or: -3 if the AI has recently finished learning to identify gestures
    private int recording_gesture = -1; 

    // Temporary storage for objects to display the gesture stroke.
    List<string> stroke = new List<string>(); 

    // Temporary counter variable when creating objects for the stroke display:
    int stroke_index = 0;

    // Handle to this object/script instance, so that callbacks from the plug-in arrive at the correct instance.
    GCHandle me;

    // Initialization:
    void Start ()
    {
        // Set the welcome message.
        HUDText = GameObject.Find("HUDText").GetComponent<Text>();
        HUDText.text = "You are the Avatar!\n"
                      + "Press triggers of both controllers to draw a gesture,\n"
                      + "and hold the end position for " + ControllerMotionTimeThreshold + "s.\n"
                      + "Available gestures:\n"
                      + "1 - right hook\n";

        me = GCHandle.Alloc(this);

        // Global setting:
        // Ignore head tilt and roll rotation to approximate torso position.
        gc.ignoreHeadRotationUpDown = true;
        gc.ignoreHeadRotationTilt = true;

        // Load the default set of gestures.
#if UNITY_EDITOR
        string gesture_file_path = "Assets/AvatarGestures";
#else
        string gesture_file_path = Application.streamingAssetsPath;
#endif
        if (LoadGesturesFile == null)
        {
            LoadGesturesFile = "gestures.dat";
        }
        if (gc.loadFromFile(gesture_file_path + "/" + LoadGesturesFile) == false)
        {
            HUDText.text = "Failed to load sample gesture database file";
            return;
        }

        if (ControllerMotionDistanceThreshold == 0)
        {
            ControllerMotionDistanceThreshold = 1;
        }
        if (ControllerMotionTimeThreshold == 0)
        {
            ControllerMotionTimeThreshold = 0.01;
        }
    }
    
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            Gancho();
        }

        float trigger_left = Input.GetAxis("LeftControllerTrigger");
        float trigger_right = Input.GetAxis("RightControllerTrigger");
        
        // If the user presses either controller's trigger, we start a new gesture.
        if (trigger_pressed_left == false && trigger_left > 0.8)
        { 
            // Controller trigger pressed.
            trigger_pressed_left = true;
            GameObject hmd = GameObject.Find("TrackingSpace");
            Vector3 hmd_p = hmd.transform.localPosition;
            Quaternion hmd_q = hmd.transform.localRotation;
            gc.startStroke(Side_Left, hmd_p, hmd_q, recording_gesture);
            gesture_started = true;
        }

        if (trigger_pressed_right == false && trigger_right > 0.8)
        {
            // Controller trigger pressed.
            trigger_pressed_right = true;
            GameObject hmd = GameObject.Find("TrackingSpace");
            Vector3 hmd_p = hmd.transform.localPosition;
            Quaternion hmd_q = hmd.transform.localRotation;
            gc.startStroke(Side_Right, hmd_p, hmd_q, recording_gesture);
            gesture_started = true;
        }

        if (gesture_started == false)
        {
            // nothing to do.
            return;
        }

        // If we arrive here, the user is currently dragging with one of the controllers.
        if (trigger_pressed_left == true)
        {
            if (trigger_left < 0.3 && controller_motion_distance_left < ControllerMotionDistanceThreshold && System.DateTime.Now.Subtract(controller_motion_time_left).Seconds > ControllerMotionTimeThreshold)
            {
                // User let go of a trigger and held controller still
                gc.endStroke(Side_Left);
                trigger_pressed_left = false;
            } else
            {
                // User still dragging or still moving after trigger pressed
                GameObject left_hand = GameObject.Find("LeftHandAnchor");
                gc.contdStroke(Side_Left, left_hand.transform.position, left_hand.transform.rotation);

                // Show the stroke by instatiating new objects
                addToStrokeTrail(left_hand.transform.position);

                float contoller_motion = (left_hand.transform.position - controller_motion_last_left).magnitude;
                controller_motion_last_left = left_hand.transform.position;
                controller_motion_distance_left = (controller_motion_distance_left + contoller_motion) * 0.5f; // averaging

                if (controller_motion_distance_left > ControllerMotionDistanceThreshold)
                {
                    controller_motion_time_left = System.DateTime.Now;
                }
            }
        }

        if (trigger_pressed_right == true)
        {
            if (trigger_right < 0.3 && controller_motion_distance_right < ControllerMotionDistanceThreshold && System.DateTime.Now.Subtract(controller_motion_time_right).Seconds > ControllerMotionTimeThreshold)
            {
                // User let go of a trigger and held controller still
                gc.endStroke(Side_Right);
                trigger_pressed_right = false;
            }
            else
            {
                // User still dragging or still moving after trigger pressed
                GameObject right_hand = GameObject.Find("RightHandAnchor");
                gc.contdStroke(Side_Right, right_hand.transform.position, right_hand.transform.rotation);

                // Show the stroke by instatiating new objects
                addToStrokeTrail(right_hand.transform.position);

                float contoller_motion = (right_hand.transform.position - controller_motion_last_right).magnitude;
                controller_motion_last_right = right_hand.transform.position;
                controller_motion_distance_right = (controller_motion_distance_right + contoller_motion) * 0.5f; // averaging

                if (controller_motion_distance_right > ControllerMotionDistanceThreshold)
                {
                    controller_motion_time_right = System.DateTime.Now;
                }
            }
        }

        if (trigger_pressed_left || trigger_pressed_right)
        {
            // User still dragging with either hand - nothing left to do
            return;
        }
        // else: if we arrive here, the user let go of both triggers, ending the gesture.
        gesture_started = false;

        // Delete the objectes that we used to display the gesture.
        foreach (string star in stroke)
        {
            Destroy(GameObject.Find(star));
            stroke_index = 0;
        }
        
        int multigesture_id = gc.identifyGestureCombination() - 4;

        // if we arrive here, we have identified a new gesture.
        // Perform the action associated with that gesture.
        if (multigesture_id < 0)
        {
            // Error trying to identify any gesture
            HUDText.text = "Failed to identify gesture.";
        }
        else if (multigesture_id == (int)GestureRecorder.AvatarGestures.Gancho)
        {
            Gancho();
        }
        else if (multigesture_id == (int)GestureRecorder.AvatarGestures.Soco)
        {
            HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.Soco + " gesture!";
        }
        else if (multigesture_id == (int)GestureRecorder.AvatarGestures.InfinitoDown)
        {
            HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.InfinitoDown + " gesture!";
        }
        else if (multigesture_id == (int)GestureRecorder.AvatarGestures.SocoXDown)
        {
            HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.SocoXDown + " gesture!";
        }
        else if (multigesture_id == (int)GestureRecorder.AvatarGestures.Cavalo)
        {
            HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.Cavalo + " gesture!";
        }
        else if (multigesture_id == (int)GestureRecorder.AvatarGestures.SemiCirculoUp)
        {
            HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.SemiCirculoUp + " gesture!";
        }
        else if (multigesture_id == (int)GestureRecorder.AvatarGestures.Concentracao)
        {
            HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.Concentracao + " gesture!";
        }
        else
        {
            // Other ID: one of the user-registered gestures:
            HUDText.text = " identified custom registered gesture " + (multigesture_id - 3);
        }
    }


    // Helper function to add a new star to the stroke trail.
    public void addToStrokeTrail(Vector3 p)
    {
        GameObject star_instance = Instantiate(GameObject.Find("star"));
        GameObject star = new GameObject("stroke_" + stroke_index++);
        star_instance.name = star.name + "_instance";
        star_instance.transform.SetParent(star.transform, false);
        System.Random random = new System.Random();
        star.transform.localPosition = new Vector3((float)random.NextDouble() / 80, (float)random.NextDouble() / 80, (float)random.NextDouble() / 80) + p;
        star.transform.localRotation = new Quaternion((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f).normalized;
        //star.transform.localRotation.Normalize();
        float star_scale = (float)random.NextDouble() + 0.3f;
        star.transform.localScale = new Vector3(star_scale, star_scale, star_scale);
        stroke.Add(star.name);
    }

    public void Gancho()
    {
        HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.Gancho + " gesture!";
        Instantiate(RockUp, transform.position + transform.forward*5 - transform.up, RockUp.transform.rotation);
    }

    public void Soco()
    {
        HUDText.text = "Identified a " + GestureRecorder.AvatarGestures.Soco + " gesture!";
    }
}
