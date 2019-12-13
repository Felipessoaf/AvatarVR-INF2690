using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RockUp : MonoBehaviour
{
    public float Force = 100;

    float lastYPos;
    bool falling = false;
    bool punched = false;

    // Start is called before the first frame update
    void Start()
    {
        Impulse();
        lastYPos = transform.position.y;
    }

    private void Update()
    {
        if (lastYPos > transform.position.y && !falling && !punched)
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
        GetComponentInChildren<Collider>().isTrigger = false;
        GetComponent<Rigidbody>().useGravity = true;
    }

    public void Punch(Vector3 direction, float force)
    {
        punched = true;
        GetComponentInChildren<Collider>().isTrigger = false;
        GetComponent<Rigidbody>().useGravity = true;
        GetComponent<Rigidbody>().AddForce(direction * force, ForceMode.Impulse);
    }
}
