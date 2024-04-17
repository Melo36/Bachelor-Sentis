using UnityEngine;

public class BillboardText : MonoBehaviour
{
    [SerializeField]
    public Camera mainCamera;

    private void Start()
    {
        // Find the main camera in the scene
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        // Ensure the main camera is valid
        if (mainCamera != null)
        {
            // Face the camera's direction while maintaining the text's up direction
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                mainCamera.transform.rotation * Vector3.up);
        }
    }
}