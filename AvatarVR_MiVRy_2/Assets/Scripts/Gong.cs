using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gong : MonoBehaviour
{
    public AudioSource GongAudio;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Rock"))
        {
            GongAudio.Play();
        }
    }
}
