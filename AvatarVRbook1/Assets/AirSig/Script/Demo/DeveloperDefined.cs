using System.Collections.Generic;
using UnityEngine;

using AirSig;

public class DeveloperDefined : BasedGestureHandle {

    // Gesture index to use for training and verifying custom gesture. Valid range is between 1 and 1000
    // Beware that setting to 100 will overwrite your player signature.
    readonly int PLAYER_GESTURE_ONE = 101;
    readonly int PLAYER_GESTURE_TWO = 102;

    public GameObject Cube1;
    public GameObject Cube2;

    // Callback for receiving signature/gesture progression or identification results
    //AirSigManager.OnDeveloperDefinedMatch developerDefined;
    AirSigManager.OnPlayerGestureMatch playerGestureMatch;

    // Use this for initialization
    void Awake()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // Update the display text
        textMode.text = string.Format("Mode: {0}", AirSigManager.Mode.IdentifyPlayerGesture.ToString());
        textResult.text = defaultResultText = "Pressing trigger and write symbol in the air\nReleasing trigger when finish";
        textResult.alignment = TextAnchor.UpperCenter;
        instruction.SetActive(false);
        ToggleGestureImage("");

        //// Configure AirSig by specifying target 
        //developerDefined = new AirSigManager.OnDeveloperDefinedMatch(HandleOnDeveloperDefinedMatch);
        //airsigManager.onDeveloperDefinedMatch += developerDefined;
        //airsigManager.SetMode(AirSigManager.Mode.DeveloperDefined);
        //airsigManager.SetDeveloperDefinedTarget(new List<string> { "HEART", "C", "DOWN" });
        //airsigManager.SetClassifier("SampleGestureProfile", "");

        playerGestureMatch = new AirSigManager.OnPlayerGestureMatch(HandleOnPlayerGestureMatch);
        airsigManager.onPlayerGestureMatch += playerGestureMatch;
        airsigManager.SetMode(AirSigManager.Mode.IdentifyPlayerGesture);
        airsigManager.SetTarget(new List<int> { PLAYER_GESTURE_ONE, PLAYER_GESTURE_TWO });

        checkDbExist();
    }

    void OnDestroy()
    {
        // Unregistering callback
        //airsigManager.onDeveloperDefinedMatch -= developerDefined;
        airsigManager.onPlayerGestureMatch -= playerGestureMatch;
    }

    void Update()
    {
        UpdateUIandHandleControl();
    }

    // Handling developer defined gesture match callback - This is invoked when the Mode is set to Mode.DeveloperDefined and a gesture is recorded.
    // gestureId - a serial number
    // gesture - gesture matched or null if no match. Only guesture in SetDeveloperDefinedTarget range will be verified against
    // score - the confidence level of this identification. Above 1 is generally considered a match
    //void HandleOnDeveloperDefinedMatch(long gestureId, string gesture, float score) {
    //    textToUpdate = string.Format("<color=cyan>Gesture Match: {0} Score: {1}</color>", gesture.Trim(), score);
    //}

    // Handling custom gesture match callback - This is inovked when the Mode is set to Mode.IdentifyPlayerGesture and a gesture
    // is recorded.
    // gestureId - a serial number
    // match - the index that match or -1 if no match. The match index must be one in the SetTarget()
    void HandleOnPlayerGestureMatch(long gestureId, int match)
    {
        if (gestureId == 0)
        {

        }
        else
        {
            string result = "<color=red>Cannot find closest custom gesture</color>";
            if (PLAYER_GESTURE_ONE == match)
            {
                result = string.Format("<color=#FF00FF>Closest Custom Gesture Gesture #1</color>");
            }
            else if (PLAYER_GESTURE_TWO == match)
            {
                result = string.Format("<color=yellow>Closest Custom Gesture Gesture #2</color>");
            }

            // Check whether this gesture match any custom gesture in the database
            float[] data = airsigManager.GetFromCache(gestureId);
            bool isExisted = airsigManager.IsPlayerGestureExisted(data);
            result += isExisted ? string.Format("\n<color=green>There is a similar gesture in DB!</color>") :
                string.Format("\n<color=red>There is no similar gesture in DB!</color>");

            textToUpdate = result;

            if (isExisted)
            {
                if (PLAYER_GESTURE_ONE == match)
                {
                    Instantiate(Cube1);
                }
                else if (PLAYER_GESTURE_TWO == match)
                {
                    result = string.Format("<color=yellow>Closest Custom Gesture Gesture #2</color>");
                    Instantiate(Cube2);
                }
            }
        }
    }
}