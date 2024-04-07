using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Lays = Unity.Sentis.Layers;


/*
 *  YOLOv8n Inference Script
 *  ========================
 *
 * Place this script on the Main Camera.
 *
 * Place the yolov8n.sentis file and a *.mp4 video file in the Assets/StreamingAssets folder
 * Create a RawImage in your scene and set it as the displayImage field
 * Drag the classes.txt into the labelsAsset field
 * Add a reference to a sprite image for the bounding box and a font for the text
 *
 */


public class RunYOLO8n : MonoBehaviour
{
    const string modelName = "sweets.sentis";
    // Change this to the name of the video you put in StreamingAssets folder:
    const string videoName = "IMG_1742.mp4";
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

    private WebCamTexture webcamTexture;
    [SerializeField]
    private ModelAsset modelAsset;

    [SerializeField] private AspectRatioFitter fit;

    public Material shaderMaterial;
    private FoodFacts foodFacts;

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

        SetupInput();
    }

    void SetupInput()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No webcam detected.");
            return;
        }

        // Start capturing from the first webcam found
        webcamTexture = new WebCamTexture(devices[0].name, Screen.width, Screen.height);
        webcamTexture.Play();
    }

    void LoadModel()
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

    public void ExecuteML()
    {
        ClearAnnotations();

        if (webcamTexture)
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
                float aspect = (float)webcamTexture.height / (float)webcamTexture.width;
                fit.aspectRatio = aspect;
                shaderMaterial.mainTexture = webcamTexture;
                Graphics.Blit(webcamTexture, targetRT, shaderMaterial);
                Graphics.Blit(targetRT, targetRT, new Vector2(1f/aspect, 1f), new Vector2(0,0));
            #endif
            displayImage.texture = targetRT;
        }
        else return;
        
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

        //Draw the bounding boxes
        for (int n = 0; n < output.shape[1]; n++)
        {
            var box = new BoundingBox
            {
                centerX = output[0, n, 0] * scaleX - displayWidth / 2,
                centerY = output[0, n, 1] * scaleY - displayHeight / 2,
                width = output[0, n, 2] * scaleX,
                height = output[0, n, 3] * scaleY,
                label = labels[labelIDs[0, 0,n]],
            };
            DrawBox(box, n);
        }
    }

    public void DrawBox(BoundingBox box , int id)
    {
        //Create the bounding box graphic or get from pool
        GameObject panel;
        bool newBbox = false;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.magenta, Color.black);
            newBbox = true;
        }
        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);
        
        //Set label text
        var childrenText = panel.GetComponentsInChildren<Text>();
        bool changed = false;
        for (int i = 0;i<childrenText.Length;i++)
        {
            var child = childrenText[i];
            if (child.name == "ObjectLabel" && child.text != box.label) {
                child.text = box.label;
                changed = true;
            } else if (child.name == "NutritionLabel" && (changed || newBbox))
            {
                string facts;
                StartCoroutine(foodFacts.GetRequest(box.label, result => {
                    // Handle the result here
                    facts = result;
                    child.text = facts;
                }));
            }
        }
    }

    public GameObject CreateNewBox(Color boxColor, Color textColor)
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

        //Create the label

        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        Text txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = textColor;
        txt.fontSize = 20;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);
        
        // Create NutritionLabel

        var nutritionLabel = new GameObject("NutritionLabel");
        nutritionLabel.AddComponent<CanvasRenderer>();
        nutritionLabel.transform.SetParent(panel.transform, false);
        Text nutritionTxt = nutritionLabel.AddComponent<Text>();
        nutritionTxt.font = font;
        nutritionTxt.fontSize = 250;
        nutritionTxt.color = Color.green;
        nutritionTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        
        RectTransform rt3 = nutritionLabel.GetComponent<RectTransform>();

        // Calculate label width and offset from the bounding box
        float labelWidth = 150f; // Adjust this value based on your label size
        float offsetFromBox = 2000f; // Adjust this value for spacing

        // Get the RectTransform of the bounding box (panel)
        RectTransform boxRT = panel.GetComponent<RectTransform>();

        // Calculate the position to the left of the bounding box
        float labelXPosition = boxRT.offsetMin.x - offsetFromBox - labelWidth;

        // Set the position and size of the NutritionLabel
        rt3.anchorMin = new Vector2(0, 0); // Left edge of the panel
        rt3.anchorMax = new Vector2(0, 1); // Left edge of the panel
        rt3.pivot = new Vector2(1, 0.5f); // Anchored to the left edge

        // Set the position and size of the label
        rt3.offsetMin = new Vector2(labelXPosition, 0);
        rt3.offsetMax = new Vector2(labelXPosition + labelWidth, boxRT.offsetMax.y); // Match height with bounding box

        boxPool.Add(panel);
        return panel;
    }

    private void createNutritionLabel(GameObject panel)
    {
        var nutritionLabel = Instantiate(nutritionLabelPrefab);
        nutritionLabel.transform.SetParent(panel.transform, false);
        Canvas canvas = nutritionLabel.GetComponentInChildren<Canvas>();
        canvas.transform.localScale = new Vector3(2,2f,1);
        RectTransform rt2 = nutritionLabel.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);
        
    }

    public void ClearAnnotations()
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