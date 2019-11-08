using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RockUp : MonoBehaviour
{
    public float Force = 100;

    float lastYPos;
    bool falling = false;

    // Start is called before the first frame update
    void Start()
    {
        Impulse();
        lastYPos = transform.position.y;
    }

    private void Update()
    {
        if (lastYPos > transform.position.y && !falling)
        {
            StartCoroutine(WaitToFall());
        }

        lastYPos = transform.position.y;
    }

    void Impulse()
    {
        falling = false;
        GetComponent<Rigidbody>().AddForce(Vector3.up * Force, ForceMode.Impulse);
        //GetComponent<Rigidbody>().AddExplosionForce(Force, transform.Find("Explosion Point").position, 1);
    }

    IEnumerator WaitToFall()
    {
        falling = true;
        GetComponent<Rigidbody>().useGravity = false;
        yield return new WaitForSeconds(1);
        GetComponent<Rigidbody>().useGravity = true;
    }
}
