using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AllergyList : MonoBehaviour
{
    public static AllergyList _instance;
    public List<string> allergyList;
    public List<string> deficiencyList;

    public static AllergyList Instance
    {
        get {
            if (_instance == null) {
                
            }
            return _instance;
        }
    }
    

    private void Awake()
    {
        _instance = this;
        allergyList = new List<string>();
        deficiencyList = new List<string>();
    }
}
