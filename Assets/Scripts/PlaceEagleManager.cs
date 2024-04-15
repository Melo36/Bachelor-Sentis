using System;
using System.Collections;
using System.Collections.Generic;
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
    
    void Start()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        arPlaneManager = GetComponent<ARPlaneManager>();
    }

    private void Update()
    {
        /*if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            bool collision = arRaycastManager.Raycast(touch.position, raycastHits, TrackableType.PlaneWithinPolygon);
            if (collision && raycastHits.Count > 0 && touch.phase == TouchPhase.Began)
            {
                Quaternion rotation = Quaternion.Euler(0, 180, 0);
                GameObject instantiatedEagle = Instantiate(eagle, raycastHits[0].pose.position, rotation);
            }
            foreach (var plane in arPlaneManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
            arPlaneManager.enabled = false;
        }*/
    }

    public bool placeEagle(float centerX, float centerY)
    {
        bool collision = arRaycastManager.Raycast(new Vector2(centerX, -centerY), raycastHits, TrackableType.PlaneWithinPolygon);
        if (collision && raycastHits.Count > 0)
        {
            Vector3 eaglePosition = raycastHits[0].pose.position;
            Vector3 direction = arCamera.transform.position - eaglePosition;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            GameObject instantiatedEagle = Instantiate(eagle, eaglePosition, rotation);
            
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
