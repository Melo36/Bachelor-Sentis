using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DealDamageComponent : MonoBehaviour {

    public GameObject hitFX;
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            animator.SetTrigger("Leave");
        }
    }

    void DealDamage() {
        transform.GetComponent<DemoController>().DealDamage(this);
    }
	

}
