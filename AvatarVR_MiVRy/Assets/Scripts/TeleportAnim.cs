using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportAnim : MonoBehaviour
{
    public void Teleport()
    {
        GetComponentInParent<AvatarController>().Teleport();
    }
}
