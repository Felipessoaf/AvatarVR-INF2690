using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportAnimEvent : MonoBehaviour
{
    public void Teleport()
    {
        GetComponentInParent<Sample_TwoHanded1>().Teleport();
    }
}
