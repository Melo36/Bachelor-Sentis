using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlaceEagleManager : MonoBehaviour
{
    [SerializeField]
    private GameObject eagle;

    private ARRaycastManager arRaycastManager;
    private ARPlaneManager arPlaneManager;

    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
    private bool eagleExists;
    [SerializeField] private Camera arCamera;
    private GameObject instantiatedEagle;
    [SerializeField] private Canvas worldSpaceCanvas;
    
    void Start()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        arPlaneManager = GetComponent<ARPlaneManager>();
        eagle.GetComponentInChildren<BillboardText>().mainCamera = arCamera;
    }

    private void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // Check if the touch phase is "Began" (user just touched the screen)
            if (touch.phase == TouchPhase.Began)
            {
                // Perform a raycast from the touch position
                Ray ray = arCamera.ScreenPointToRay(touch.position);
                RaycastHit hit;

                // Check if the raycast hits any GameObject
                if (Physics.Raycast(ray, out hit))
                {
                    // Check if the hit GameObject has a Collider (e.g., it's interactable)
                    Collider collider = hit.collider;
                    if (collider != null && collider.CompareTag("Eagle"))
                    {
                        // The user clicked on the eagle GameObject
                        Debug.Log("Eagle Clicked!");

                        // Perform desired actions (e.g., play sound, show UI, etc.)
                        // You can add your custom interaction logic here
                    }
                }
            }
        }
    }

    public bool placeEagle(float centerX, float centerY)
    {
        bool collision = arRaycastManager.Raycast(new Vector2(centerX, -centerY), raycastHits, TrackableType.PlaneWithinPolygon);
        if (collision && raycastHits.Count > 0)
        {
            if (instantiatedEagle != null)
            {
                Destroy(instantiatedEagle);
            }
            Vector3 eaglePosition = raycastHits[0].pose.position;
            Vector3 direction = arCamera.transform.position - eaglePosition;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            instantiatedEagle = Instantiate(eagle, eaglePosition, rotation);
            
            foreach (var plane in arPlaneManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
            arPlaneManager.enabled = false;
            return true;
        }
        return false;
    }
}
