﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RockUp : MonoBehaviour
{
    public static List<RockUp> Rocks;

    public float Force = 100;

    float lastYPos;
    bool falling = false;
    bool punched = false;

    private static bool allUp;
    private static bool waitAllDown = false;

    private void Awake()
    {
        if(Rocks == null)
        {
            Rocks = new List<RockUp>();
        }

        Rocks.Add(this);
    }

    // Start is called before the first frame update
    void Start()
    {
        Impulse();
        lastYPos = transform.position.y;
    }

    private void Update()
    {
        if (lastYPos > transform.position.y && !falling/* && !punched*/)
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
        if(allUp)
        {
            yield return new WaitWhile(() => waitAllDown);
        }
        else
        {
            yield return new WaitForSeconds(1);
        }
        GetComponentInChildren<Collider>().isTrigger = false;
        GetComponent<Rigidbody>().useGravity = true;
    }

    public void Punch(Vector3 direction, float force)
    {
        //punched = true;
        GetComponentInChildren<Collider>().isTrigger = false;
        GetComponent<Rigidbody>().useGravity = true;
        GetComponent<Rigidbody>().AddForce(direction * force, ForceMode.Impulse);
    }

    public static void AllRocksUp()
    {
        allUp = true;
        waitAllDown = true;
        foreach (RockUp rock in Rocks)
        {
            rock.Impulse();
        }
    }

    public static void AllRocksDown()
    {
        waitAllDown = false;
        allUp = false;
    }
}
