using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DealDamageComponent : MonoBehaviour {

    public GameObject hitFX;
    
    void DealDamage() {
        transform.GetComponent<DemoController>().DealDamage(this);
    }
	

}
