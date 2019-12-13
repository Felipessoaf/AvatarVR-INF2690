﻿/*
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
using UnityEngine.SceneManagement;
using VRTK;


public class Sample_TwoHanded1 : MonoBehaviour
{
    public enum AvatarGestures
    {
        Gancho,
        Soco,
        Cavalo,
        SemiCirculoUp,
        InfinitoDown,
        SocoXDown,
        Concentracao,
        None
    }

    // Convenience ID's for the "left" and "right" sub-gestures.
    public const int Side_Left = 0;
    public const int Side_Right = 1;

    public bool EditMode = false;
    public bool FakeMode = false;
    [Range(0, 1)]
    public float similarityMin = 0.3f;

    // The file from which to load gestures on startup (left hand).
    // For example: "Assets/GestureRecognition/Sample_TwoHanded_Gestures.dat"
    [SerializeField] private string LoadGesturesFile;

    // File where to save recorded gestures.
    // For example: "Assets/GestureRecognition/Sample_TwoHanded_MyGestures.dat"
    [SerializeField] private string SaveGesturesFile;
    
    // Tolerance for averaged controller motion to detect if the controller is still moving.
    [SerializeField] private double ControllerMotionDistanceThreshold;

    // Time the controller must be held still for averaged controller motion to detect if the controller is still moving.
    [SerializeField] private double ControllerMotionTimeThreshold;

    [Space]
    public VRTK_ControllerEvents LeftHandEvents;
    public VRTK_ControllerEvents RightHandEvents;

    [Space]
    public GameObject RockUpPrefab;
    public LayerMask SocoLayer;

    [HideInInspector]
    public VRTK_Pointer teleportPointer;

    bool teleporting = false;

    AvatarGestures currentAvatarGesture = AvatarGestures.Gancho;

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
    private bool fake_trigger_pressed_left = false;
    private bool fake_trigger_pressed_right = false;

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

    // Last reported recognition performance (during training).
    // 0 = 0% correctly recognized, 1 = 100% correctly recognized.
    private double last_performance_report = 0; 

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
        HUDText = GameObject.Find("TextCanvas").transform.Find("HUDText").GetComponent<Text>();


        HUDText.gameObject.SetActive(EditMode);

        HUDText.text = "Welcome to 3D Gesture Recognition Plug-in!\n"
                      + "Press triggers of both controllers to draw a gesture,\n"
                      + "and hold the end position for " + ControllerMotionTimeThreshold + "s.\n"
                      + "Available gestures:\n"
                      + "1 - throw your hands up\n"
                      + "2 - pound your chest (like King Kong)\n"
                      + "3 - shoot an arrow (bow-and-arrow)\n"
                      + "4 - draw a heart shape (with both hands)\n"
                      + "or: press 'A'/'X'/Menu button\nto create new gesture.";

        me = GCHandle.Alloc(this);

        // Global setting:
        // Ignore head tilt and roll rotation to approximate torso position.
        gc.ignoreHeadRotationUpDown = false;
        gc.ignoreHeadRotationTilt = false;

        // Load the default set of gestures.
#if UNITY_EDITOR
        string gesture_file_path = "Assets/Gestures";
#else
        string gesture_file_path = Application.streamingAssetsPath;
#endif
        if (LoadGesturesFile == null)
        {
            LoadGesturesFile = "TwoHanded_Base.dat";
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
    

    // Update:
    void Update()
    {
        if (VRTK_DeviceFinder.HeadsetTransform())
        {
            transform.position = VRTK_DeviceFinder.HeadsetTransform().position;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(0);
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            Gancho();
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            Soco();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            Cavalo();
        }

        // If recording_gesture is -3, that means that the AI has recently finished learning a new gesture.
        if (recording_gesture == -3)
        {
            // Show "finished" message.
            double performance = gc.recognitionScore();
            HUDText.text = "Training finished!\n(Final recognition performance = " + (performance * 100.0) + "%)\nFeel free to use your new gesture.";
            // Set recording_gesture to -1 to indicate normal operation (learning finished).
            recording_gesture = -1;
        }
        // If recording_gesture is -2, that means that the AI is currently learning a new gesture.
        if (recording_gesture == -2)
        {
            // Show "please wait" message
            HUDText.text = "...training...\n(Current recognition performance = " + (last_performance_report * 100.0) + "%)\nPress the 'A'/'X'/Menu button to cancel training.";
            // In this mode, the user may press the "B/Y/menu" button to cancel the learning process.
            if (LeftHandEvents.buttonOnePressed || RightHandEvents.buttonOnePressed)
            {
                // Button pressed: stop the learning process.
                gc.stopTraining();
                recording_gesture = -3;
            }
            return;
		}
        // Else: if we arrive here, we're not in training/learning mode,
        // so the user can draw gestures.

        // If recording_gesture is -1, we're currently not recording a new gesture.
        if (recording_gesture == -1 && EditMode)
        {
            // In this mode, the user can press button A/X to create a new gesture
            if (LeftHandEvents.buttonOnePressed || RightHandEvents.buttonOnePressed)
            {
                int recording_gesture_left = gc.createGesture(Side_Left, currentAvatarGesture.ToString() + "LEFT HAND");
                int recording_gesture_right = gc.createGesture(Side_Right, currentAvatarGesture.ToString() + "RIGHT HAND");
                recording_gesture = gc.createGestureCombination(currentAvatarGesture.ToString());
                gc.setCombinationPartGesture(recording_gesture, Side_Left, recording_gesture_left);
                gc.setCombinationPartGesture(recording_gesture, Side_Right, recording_gesture_right);
                // from now on: recording a new gesture
                HUDText.text = "Learning a new gesture (" + currentAvatarGesture.ToString() + " id:" + (recording_gesture) + "):\nPlease perform the gesture 25 times.\n(0 / 25)";
            }
        }

        
        // If the user presses either controller's trigger, we start a new gesture.
        if (trigger_pressed_left == false && LeftHandEvents.triggerPressed)
        { 
            // Controller trigger pressed.
            trigger_pressed_left = true;
            Transform hmd = VRTK_DeviceFinder.HeadsetTransform(); // alternative: Camera.main.gameObject
            Vector3 hmd_p = hmd.localPosition;
            Quaternion hmd_q = hmd.localRotation;
            gc.startStroke(Side_Left, hmd_p, hmd_q, recording_gesture);
            gesture_started = true;
        }
        if (trigger_pressed_right == false && RightHandEvents.triggerPressed)
        {
            // Controller trigger pressed.
            trigger_pressed_right = true;
            Transform hmd = VRTK_DeviceFinder.HeadsetTransform(); // alternative: Camera.main.gameObject
            Vector3 hmd_p = hmd.localPosition;
            Quaternion hmd_q = hmd.localRotation;
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
            if (!LeftHandEvents.triggerPressed && controller_motion_distance_left < ControllerMotionDistanceThreshold && System.DateTime.Now.Subtract(controller_motion_time_left).Seconds > ControllerMotionTimeThreshold)
            {
                // User let go of a trigger and held controller still
                gc.endStroke(Side_Left);
                trigger_pressed_left = false;

                if (fake_trigger_pressed_right && FakeMode)
                {
                    fake_trigger_pressed_right = false;
                    gc.endStroke(Side_Right);
                }
            }
            else
            {
                // User still dragging or still moving after trigger pressed
                GameObject left_hand = VRTK_DeviceFinder.GetControllerLeftHand();
                gc.contdStroke(Side_Left, left_hand.transform.localPosition, left_hand.transform.rotation);

                if (fake_trigger_pressed_right && !trigger_pressed_right && FakeMode)
                {
                    gc.contdStroke(Side_Right, Vector3.zero, Quaternion.identity);
                }

                if (!trigger_pressed_right && !fake_trigger_pressed_right && FakeMode)
                {
                    Transform hmd = VRTK_DeviceFinder.HeadsetTransform();
                    Vector3 hmd_p = hmd.localPosition;
                    Quaternion hmd_q = hmd.localRotation;
                    gc.startStroke(Side_Right, hmd_p, hmd_q, recording_gesture);
                    fake_trigger_pressed_right = true;
                }

                // Show the stroke by instatiating new objects
                addToStrokeTrail(left_hand.transform.position);

                float contoller_motion = (left_hand.transform.localPosition - controller_motion_last_left).magnitude;
                controller_motion_last_left = left_hand.transform.localPosition;
                controller_motion_distance_left = (controller_motion_distance_left + contoller_motion) * 0.5f; // averaging

                if (controller_motion_distance_left > ControllerMotionDistanceThreshold)
                {
                    controller_motion_time_left = System.DateTime.Now;
                }
            }
        }

        if (trigger_pressed_right == true)
        {
            if (!RightHandEvents.triggerPressed && controller_motion_distance_right < ControllerMotionDistanceThreshold && System.DateTime.Now.Subtract(controller_motion_time_right).Seconds > ControllerMotionTimeThreshold)
            {
                // User let go of a trigger and held controller still
                gc.endStroke(Side_Right);
                trigger_pressed_right = false;

                if (fake_trigger_pressed_left && FakeMode)
                {
                    fake_trigger_pressed_left = false;
                    gc.endStroke(Side_Left);
                }
            }
            else
            {
                // User still dragging or still moving after trigger pressed
                GameObject right_hand = VRTK_DeviceFinder.GetControllerRightHand();
                gc.contdStroke(Side_Right, right_hand.transform.position, right_hand.transform.rotation);

                if (fake_trigger_pressed_left && !trigger_pressed_left && FakeMode)
                {
                    Vector3 newPos = right_hand.transform.position;
                    newPos.x *= -1;

                    gc.contdStroke(Side_Left, newPos, right_hand.transform.rotation);
                }

                if (!trigger_pressed_left && !fake_trigger_pressed_left && FakeMode)
                {
                    Transform hmd = VRTK_DeviceFinder.HeadsetTransform();
                    Vector3 hmd_p = hmd.localPosition;
                    Quaternion hmd_q = hmd.localRotation;
                    gc.startStroke(Side_Left, hmd_p, hmd_q, recording_gesture);
                    fake_trigger_pressed_left = true;
                }

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

        double similarity = 0;
        int multigesture_id = gc.identifyGestureCombination(ref similarity);
        
        // If we are currently recording samples for a custom gesture, check if we have recorded enough samples yet.
        if (recording_gesture >= 0)
        {
            // Currently recording samples for a custom gesture - check how many we have recorded so far.
            int num_samples;
            if (FakeMode)
            {
                num_samples = Mathf.Max(gc.getGestureNumberOfSamples(Side_Left, recording_gesture), gc.getGestureNumberOfSamples(Side_Right, recording_gesture));
            }
            else
            {
                num_samples = gc.getGestureNumberOfSamples(Side_Left, recording_gesture);
            }

            if (num_samples < 25)
            {
                // Not enough samples recorded yet.
                HUDText.text = "Learning a new gesture (" + currentAvatarGesture.ToString() + " id:" + (recording_gesture) + "):\nPlease perform the gesture 25 times.\n(" + num_samples + " / 25)";
            }
            else
            {
                // Enough samples recorded. Start the learning process.
                HUDText.text = "Learning gestures - please wait...\n(press B button to stop the learning process)";
                // Set up the call-backs to receive information about the learning process.
                gc.setTrainingUpdateCallback(trainingUpdateCallback);
                gc.setTrainingUpdateCallbackMetadata((IntPtr)me);
                gc.setTrainingFinishCallback(trainingFinishCallback);
                gc.setTrainingFinishCallbackMetadata((IntPtr)me);
                gc.setMaxTrainingTime(30);

                // Set recording_gesture to -2 to indicate that we're currently in learning mode.
                recording_gesture = -2;

                if (gc.startTraining() == false)
                {
                    Debug.Log("COULD NOT START TRAINING");
                }
            }
            return;
        }

        if (similarity < similarityMin)
        {
            HUDText.text = "Similarity lower than permitted: " + similarity;
            return;
        }

        // else: if we arrive here, we're not recording new samples for custom gestures,
        // but instead have identified a new gesture.
        // Perform the action associated with that gesture.
        if (multigesture_id < 0)
        {
            // Error trying to identify any gesture
            HUDText.text = "Failed to identify gesture." + "\nSimilarity: " + similarity;
        }
        else if (multigesture_id == 0)
        {
            HUDText.text = "Identified a \"throw-your-hands-up\" gesture!" + "\nSimilarity: " + similarity;
        }
        else if (multigesture_id == 1)
        {
            HUDText.text = "Identified a chest-pounding gesture!" + "\nSimilarity: " + similarity;
        }
        else if (multigesture_id == 2)
        {
            HUDText.text = "Identified a bow-and-arrow gesture!" + "\nSimilarity: " + similarity;
        }
        else if (multigesture_id == 3)
        {
            HUDText.text = "Identified a heart-shape gesture!" + "\nSimilarity: " + similarity;
        }
        else
        {
            multigesture_id -=  4;

            if (multigesture_id == (int)AvatarGestures.Gancho)
            {
                Gancho();
            }
            else if (multigesture_id == (int)AvatarGestures.Soco)
            {
                Soco();
            }
            else if (multigesture_id == (int)AvatarGestures.Cavalo)
            {
                Cavalo();
            }
            else if (multigesture_id == (int)AvatarGestures.SemiCirculoUp)
            {
                SemiCirculoUp();
            }
            else if (multigesture_id == (int)AvatarGestures.InfinitoDown)
            {
                InfinitoDown();
            }
            else if (multigesture_id == (int)AvatarGestures.SocoXDown)
            {
                HUDText.text = "Identified a " + AvatarGestures.SocoXDown + " gesture!";
            }
            else if (multigesture_id == (int)AvatarGestures.Concentracao)
            {
                HUDText.text = "Identified a " + AvatarGestures.Concentracao + " gesture!";
            }
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

    // Callback function to be called by the left-hand gesture recognition plug-in during the learning process.
    public static void trainingUpdateCallback(double performance, IntPtr ptr)
    {
        // Get the script/scene object back from metadata.
        GCHandle obj = (GCHandle)ptr;
        Sample_TwoHanded1 me = (obj.Target as Sample_TwoHanded1);
        // Update the performance indicator with the latest estimate.
        me.last_performance_report = performance;
    }
    // Callback function to be called by the gesture recognition plug-in when the learning process was finished.
    public static void trainingFinishCallback(double performance, IntPtr ptr)
    {
        // Get the script/scene object back from metadata.
        GCHandle obj = (GCHandle)ptr;
        Sample_TwoHanded1 me = (obj.Target as Sample_TwoHanded1);
        // Save the data to file.
#if UNITY_EDITOR
        string gesture_file_path = "Assets/Gestures";
#else
        string gesture_file_path = Application.streamingAssetsPath;
#endif
        if (me.SaveGesturesFile == null)
        {
            me.SaveGesturesFile = "TwoHanded_Avatar.dat";
        }
        me.gc.saveToFile(gesture_file_path + "/" + me.SaveGesturesFile);
        // Update the performance indicator with the latest estimate.
        me.last_performance_report = performance;
        // Signal that training was finished.
        me.recording_gesture = -3;

        me.currentAvatarGesture += 1;

        if (me.currentAvatarGesture >= AvatarGestures.None)
        {
            me.currentAvatarGesture = 0;
        }
    }

    public void Gancho()
    {
        HUDText.text = "Identified a " + AvatarGestures.Gancho + " gesture!";
        Instantiate(RockUpPrefab, transform.position + transform.forward * 2 - transform.up, RockUpPrefab.transform.rotation);
    }

    public void Soco()
    {
        HUDText.text = "Identified a " + AvatarGestures.Soco + " gesture!";

        if (teleporting)
        {
            LeftHandEvents.OnTouchpadReleased(LeftHandEvents.SetControllerEvent(ref LeftHandEvents.touchpadPressed, false, 0f));
        }
        else
        {
            RaycastHit hit;
            Debug.DrawRay(transform.GetChild(0).position + Vector3.up * 0.5f, transform.GetChild(0).forward * 5, Color.green, 5, false);
            Debug.DrawRay(transform.GetChild(0).position + Vector3.down * 0.5f, transform.GetChild(0).forward * 5, Color.red, 5, false);
            if (Physics.Raycast(transform.GetChild(0).position + Vector3.up * 0.5f, transform.GetChild(0).forward, out hit, 5, SocoLayer, QueryTriggerInteraction.Collide))
            {
                if (hit.transform.GetComponent<RockUp>())
                {
                    hit.transform.GetComponent<RockUp>().Punch(transform.GetChild(0).forward, 10);
                }
            }
            else if (Physics.Raycast(transform.GetChild(0).position + Vector3.down * 0.5f, transform.GetChild(0).forward, out hit, 5, SocoLayer, QueryTriggerInteraction.Collide))
            {
                if (hit.transform.GetComponent<RockUp>())
                {
                    hit.transform.GetComponent<RockUp>().Punch(transform.GetChild(0).forward, 10);
                }
            }
        }
    }

    public void Cavalo()
    {
        HUDText.text = "Identified a " + AvatarGestures.Cavalo + " gesture!";

        LeftHandEvents.OnTouchpadPressed(LeftHandEvents.SetControllerEvent(ref LeftHandEvents.touchpadPressed, true, 1f));
        teleporting = true;
    }

    public void SemiCirculoUp()
    {
        HUDText.text = "Identified a " + AvatarGestures.SemiCirculoUp + " gesture!";

        RockUp.AllRocksUp();
    }

    public void InfinitoDown()
    {
        HUDText.text = "Identified a " + AvatarGestures.InfinitoDown + " gesture!";
    }

    public void Teleport()
    {
        teleportPointer.FinishTeleport();
    }
}
