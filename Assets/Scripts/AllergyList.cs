using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AllergyList : MonoBehaviour
{
    public static AllergyList instance { get; private set; }
    public static List<string> allergyList = new List<string>();
    public static List<string> defficiencyList = new List<string>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
        }
        else
        {
            instance = this;
        }
    }
}
