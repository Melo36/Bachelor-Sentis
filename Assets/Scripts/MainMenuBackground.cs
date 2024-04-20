using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuBackground : MonoBehaviour
{
    // Start is called before the first frame update
    private RawImage background;
    private WebCamTexture webcamTexture;
    private AspectRatioFitter fit;
    private RenderTexture targetRT;
    
    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;
    
    public Material shaderMaterial;
    public Material blurMaterial;

    private int frame = 0;
    void Start()
    {
        background = GetComponent<RawImage>();
        fit = GetComponent<AspectRatioFitter>();
        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        SetupInput();
    }

    // Update is called once per frame
    void Update()
    {
        frame++;
        if (frame % 60 != 0)
        {
            return;
        }
        if (webcamTexture)
        {
            #if UNITY_EDITOR_OSX
                float aspect = (float)webcamTexture.width / (float)webcamTexture.height;
                fit.aspectRatio = aspect;
                float mirror = webcamTexture.videoVerticallyMirrored ? -1f : 1f;
                background.rectTransform.localScale = new Vector3(1f / aspect, mirror / aspect, 1f / aspect);
                    
                int orient = -webcamTexture.videoRotationAngle;
                background.rectTransform.localEulerAngles = new Vector3(0, 0, orient);
                Graphics.Blit(webcamTexture, targetRT, new Vector2(1f, 1f), new Vector2(0,0));
            #elif UNITY_IOS
                float aspect = (float)webcamTexture.width / (float)webcamTexture.height;
                fit.aspectRatio = aspect;
                shaderMaterial.mainTexture = webcamTexture;
                Graphics.Blit(webcamTexture, targetRT, shaderMaterial);
                Graphics.Blit(targetRT, targetRT, new Vector2(1f/aspect, 1f), new Vector2(0,0));
            #endif
            float blurAmount = 500f;
            blurMaterial.SetFloat("_BlurAmount", blurAmount); // Set the blur amount parameter in the shader
            RenderTexture blurredTexture = new RenderTexture(imageWidth, imageHeight, 0);
            Graphics.Blit(targetRT, blurredTexture, blurMaterial);
            background.texture = blurredTexture;
        }
    }
    
    void SetupInput()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No webcam detected.");
        }
        // Start capturing from the first webcam found
        webcamTexture = new WebCamTexture(devices[0].name, Screen.width, Screen.height);
        webcamTexture.Play();
    }
}
