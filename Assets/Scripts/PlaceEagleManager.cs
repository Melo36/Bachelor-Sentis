using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlaceEagleManager : MonoBehaviour
{
    [SerializeField]
    private GameObject eagle;

    private ARRaycastManager arRaycastManager;
    private ARPlaneManager arPlaneManager;
    private XROrigin xrOrigin;

    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
    private bool eagleExists;
    
    void Start()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        arPlaneManager = GetComponent<ARPlaneManager>();
        xrOrigin = GetComponent<XROrigin>();
    }

    void Update()
    {
        if (!eagleExists)
        {
            //arRaycastManager.Raycast()
        }
    }

    public void placeEagle(float centerX, float centerY)
    {
        bool collision = arRaycastManager.Raycast(new Vector2(centerX + 100f, centerY), raycastHits, TrackableType.PlaneWithinPolygon);
        if (collision)
        {
            GameObject instantiatedEagle = Instantiate(eagle);
            instantiatedEagle.transform.position = raycastHits[0].pose.position;

            foreach (var plane in arPlaneManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
            
            arPlaneManager.enabled = false;

        }
    }
}
