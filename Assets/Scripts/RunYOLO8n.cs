using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.IntegerTime;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Lays = Unity.Sentis.Layers;
using Unity.Collections;


public class RunYOLO8n : MonoBehaviour
{
    // Link the classes.txt here:
    public TextAsset labelsAsset;
    // Create a Raw Image in the scene and link it here:
    public RawImage displayImage;
    // Link to a bounding box texture here:
    public Sprite boxTexture;
    // Link to the font for the labels:
    public Font font;

    const BackendType backend = BackendType.GPUCompute;

    private Transform displayLocation;
    private Model model;
    private IWorker engine;
    private string[] labels;
    private RenderTexture targetRT;
    private string currentLabel = "";

    [SerializeField]
    private GameObject nutritionLabelPrefab;


    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;

    //The number of classes in the model
    private const int numClasses = 5;
    
    List<GameObject> boxPool = new List<GameObject>();

    [SerializeField, Range(0, 1)] float iouThreshold = 0.5f;
    [SerializeField, Range(0, 1)] float scoreThreshold = 0.5f;
    int maxOutputBoxes = 32;

    //For using tensor operators:
    Ops ops;

    //private WebCamTexture webcamTexture;
    [SerializeField]
    private ModelAsset modelAsset;

    private WebCamTexture webcamTexture;
    [SerializeField] private AspectRatioFitter fit;

    public Material shaderMaterial;
    private FoodFacts foodFacts;
    private int frame = 0;

    private ARCameraManager cameraManager;
    private XRCpuImage cpuImage;
    public Vector2Int DesireResolution = new Vector2Int(1170,2532);
    private Texture2D outputTexture = null;

    [SerializeField] private PlaceEagleManager eagleManager;
    [SerializeField] private GameObject eagle;

    [SerializeField] private GameObject xrOrigin;
    [SerializeField] private GameObject arSession;
    [SerializeField] private GameObject sceneCamera;

    private List<string> allergyList;
    private string[] ingredients;

    private bool placedEagle = false;

    //bounding box data
    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
    }

    
    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;

        ops = WorkerFactory.CreateOps(backend, null);

        //Parse neural net labels
        labels = labelsAsset.text.Split('\n');

        LoadModel();

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);

        //Create image to display video
        displayLocation = displayImage.transform;

        //Create engine to run model
        engine = WorkerFactory.CreateWorker(backend, model);

        foodFacts = GetComponent<FoodFacts>();

        allergyList = AllergyList.Instance.allergyList;
        
        #if UNITY_EDITOR_OSX
        arSession.SetActive(false);
        xrOrigin.SetActive(false);
        sceneCamera.SetActive(true);
        SetupInput();
        #endif
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

    private void OnEnable()
    {
        cameraManager = GetComponent<ARCameraManager>();
        if (cameraManager)
        {
            cameraManager.frameReceived += FrameChanged;
        }
    }

    private void OnDisable()
    {
        if (cameraManager)
        {
            cameraManager.frameReceived -= FrameChanged;
        }
    }
    
    private void FrameChanged(ARCameraFrameEventArgs args){
        if (!cameraManager.TryAcquireLatestCpuImage(out cpuImage))
        {
            Debug.Log("Nicht geklaoptt");
            return;
        }
        using (cpuImage) {
            var width = Mathf.Min(DesireResolution.x, cpuImage.width);
            var height = Mathf.Min((DesireResolution.y * width) / DesireResolution.x, cpuImage.height);

            var conversionParams = new XRCpuImage.ConversionParams {
                inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(width, height),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY,
            };
            
            int size = cpuImage.GetConvertedDataSize(conversionParams);
            
            using var buffer = new NativeArray<byte>(size, Allocator.Temp);
            var slice = new NativeSlice<byte>(buffer);

            cpuImage.Convert(conversionParams, slice);

            if (outputTexture == null) {
                outputTexture = new Texture2D(
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
                    conversionParams.outputFormat,
                    false);
            }

            outputTexture.LoadRawTextureData(buffer);
            outputTexture.Apply();
        }
    }
    

    private void LoadModel()
    {
        //Load model
        model = ModelLoader.Load(modelAsset);

        //The classes are also stored here in JSON format:
        Debug.Log($"Class names: \n{model.Metadata["names"]}");

        //We need to add some layers to choose the best boxes with the NMSLayer
        
        //Set constants
        model.AddConstant(new Lays.Constant("0", new int[] { 0 }));
        model.AddConstant(new Lays.Constant("1", new int[] { 1 }));
        model.AddConstant(new Lays.Constant("4", new int[] { 4 }));


        model.AddConstant(new Lays.Constant("classes_plus_4", new int[] { numClasses + 4 }));
        model.AddConstant(new Lays.Constant("maxOutputBoxes", new int[] { maxOutputBoxes }));
        model.AddConstant(new Lays.Constant("iouThreshold", new float[] { iouThreshold }));
        model.AddConstant(new Lays.Constant("scoreThreshold", new float[] { scoreThreshold }));
       
        //Add layers
        model.AddLayer(new Lays.Slice("boxCoords0", "output0", "0", "4", "1")); 
        model.AddLayer(new Lays.Transpose("boxCoords", "boxCoords0", new int[] { 0, 2, 1 }));
        model.AddLayer(new Lays.Slice("scores0", "output0", "4", "classes_plus_4", "1")); 
        model.AddLayer(new Lays.ReduceMax("scores", new[] { "scores0", "1" }));
        model.AddLayer(new Lays.ArgMax("classIDs", "scores0", 1));

        model.AddLayer(new Lays.NonMaxSuppression("NMS", "boxCoords", "scores",
            "maxOutputBoxes", "iouThreshold", "scoreThreshold",
            centerPointBox: Lays.CenterPointBox.Center
        ));

        model.outputs.Clear();
        model.AddOutput("boxCoords");
        model.AddOutput("scores");
        model.AddOutput("classIDs");
        model.AddOutput("NMS");
    }

    private void Update()
    {
        ExecuteML();
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    private void ExecuteML()
    {
        if (outputTexture || webcamTexture)
        {
            #if UNITY_EDITOR_OSX
                float aspect = (float)webcamTexture.width / (float)webcamTexture.height;
                fit.aspectRatio = aspect;
                float mirror = webcamTexture.videoVerticallyMirrored ? -1f : 1f;
                displayImage.rectTransform.localScale = new Vector3(1f / aspect, mirror / aspect, 1f / aspect);
                
                int orient = -webcamTexture.videoRotationAngle;
                displayImage.rectTransform.localEulerAngles = new Vector3(0, 0, orient);

                Graphics.Blit(webcamTexture, targetRT, new Vector2(1f, 1f), new Vector2(0,0));
            #elif UNITY_IOS
                float aspect = (float)outputTexture.width / (float)outputTexture.height;
                fit.aspectRatio = aspect;
                shaderMaterial.mainTexture = outputTexture;
                Graphics.Blit(outputTexture, targetRT, shaderMaterial);
                Graphics.Blit(targetRT, targetRT, new Vector2(1f/aspect, 1f), new Vector2(0,0));
            #endif
            // displayImage.texture = targetRT;
        }
        else return;

        // TODO: implement moving average
        frame++;
        if (frame % 10 != 0)
        {
            return;
        }
        
        ClearAnnotations();
        
        using var input = TextureConverter.ToTensor(targetRT, imageWidth, imageHeight, 3);
        engine.Execute(input);

        var boxCoords = engine.PeekOutput("boxCoords") as TensorFloat;
        var NMS = engine.PeekOutput("NMS") as TensorInt;
        var classIDs = engine.PeekOutput("classIDs") as TensorInt;

        using var boxIDs = ops.Slice(NMS, new int[] { 2 }, new int[] { 3 }, new int[] { 1 }, new int[] { 1 });
        using var boxIDsFlat = boxIDs.ShallowReshape(new TensorShape(boxIDs.shape.length)) as TensorInt;
        using var output = ops.Gather(boxCoords, boxIDsFlat, 1);
        using var labelIDs = ops.Gather(classIDs, boxIDsFlat, 2);
        
        output.MakeReadable();
        labelIDs.MakeReadable();
        
        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        // Draw the bounding boxes
        for (int n = 0; n < output.shape[1]; n++)
        {
            var box = new BoundingBox
            {
                centerX = output[0, n, 0] * scaleX - displayWidth / 2,
                centerY = output[0, n, 1] * scaleY - displayHeight / 2,
                width = output[0, n, 2] * scaleX,
                height = output[0, n, 3] * scaleY,
                label = labels[labelIDs[0, 0, n]],
            };
            DrawBox(box, n);
        }
    }

    private void DrawBox(BoundingBox box , int id)
    {
        // Create the bounding box graphic or get from pool
        GameObject panel;
        bool newBbox = false;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.magenta);
            newBbox = true;
        }
        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);
        
        if (!placedEagle || newBbox || currentLabel != box.label)
        {
            placedEagle = eagleManager.placeEagle(box.centerX, box.centerY);
            placedEagle = true;
        }
        
        //Set label text
        if (currentLabel != box.label || newBbox)
        {
            StartCoroutine(foodFacts.GetRequest(box.label, result =>
            {
                // Handle the result here
                float[] values = result;
                PieChart pieChart = panel.GetComponentInChildren<PieChart>();
                pieChart.setValues(values);
                var textChildren = panel.GetComponentsInChildren<TextMeshProUGUI>();
                string[] type = { "Fett: ", "Kohlenhydrate: ", "Eiweiß: " };
                for (int i = 0; i < textChildren.Length - 1; i++)
                {
                    textChildren[i + 1].text = type[i] + values[i] + "g";
                }

                ingredients = foodFacts.getIngredients().Split(",");

                textChildren[0].text = (int)values[0] * 9 + (int)values[1] * 4 + (int)values[2] * 4 + " kcal";
                currentLabel = box.label;
            }));
        }
    }

    private GameObject CreateNewBox(Color boxColor)
    {
        //Create the box and set image
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = boxColor;
        img.sprite = boxTexture;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        createNutritionLabel(panel);

        boxPool.Add(panel);
        return panel;
    }

    private void createNutritionLabel(GameObject panel)
    {
        var nutritionLabel = Instantiate(nutritionLabelPrefab, panel.transform);
        Canvas canvas = nutritionLabel.GetComponentInChildren<Canvas>();
        canvas.transform.localScale = new Vector3(1,1,1);
        
        RectTransform rt = nutritionLabel.GetComponent<RectTransform>();
        
        // Set the anchor to align the bottom of the nutritionLabel with the top of the panel
        rt.anchorMin = new Vector2(0f, 1f); // Anchor at the top-left corner of the panel
        rt.anchorMax = new Vector2(1f, 1f); // Anchor at the top-right corner of the panel
        rt.pivot = new Vector2(0.5f, 1f); // Pivot at the bottom-center of the nutritionLabel

        // Calculate the height of the panel and desired height of the nutritionLabel
        float nutritionLabelHeight = 500f;

        // Set the size of the nutritionLabel
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, nutritionLabelHeight);

        // Calculate the anchored position to align the bottom of the nutritionLabel with the top of the panel
        rt.anchoredPosition = new Vector2(0f, nutritionLabelHeight);

        for (int i = 0; i < allergyList.Count; i++)
        {
            for (int j = 0; j < ingredients.Length; j++)
            {
                if (allergyList[i] == ingredients[j])
                {
                    Debug.Log("Found allergy");
                }
            }
        }
    }

    private void ClearAnnotations()
    {
        foreach(var box in boxPool)
        {
            box.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        engine?.Dispose();
        ops?.Dispose();
    }
}