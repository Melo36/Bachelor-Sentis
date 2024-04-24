using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Unity.VisualScripting;


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
    [SerializeField]
    private GameObject allergyLabelPrefab;

    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;

    //The number of classes in the model
    private const int numClasses = 5;
    
    List<GameObject> boxPool = new List<GameObject>();

    [SerializeField, Range(0, 1)] float iouThreshold = 0.5f;
    [SerializeField, Range(0, 1)] float scoreThreshold = 0.5f;
    int maxOutputBoxes = 4;

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
    private List<string> deficiencyList;
    private string ingredientsArray;

    private bool placedEagle = false;
    private bool hasIngredients = false;
    private int movingAverageWindow = 10;
    private List<BoundingBox> movingAverageBBoxes = new List<BoundingBox>();

    public Dictionary<string, DetailedNutrition> detailedNutritionDict = new Dictionary<string, DetailedNutrition>();

    private Dictionary<string, int> labelBoxPoolID = new Dictionary<string, int>();
    private int dictIndex = -1;

    private Transform currentAllergyLabel;

    private Dictionary<string, string[]> deficiencyDict;

    //bounding box data
    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public float area;
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
        deficiencyList = AllergyList.Instance.deficiencyList;

        setupDeficiencyDict();
        
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
        frame++;

        int foundBoxes = output.shape[1];

        if (foundBoxes > 0)
        {
            BoundingBox[] bboxArray = new BoundingBox[foundBoxes];

            // Draw the bounding boxes
            // Save all bboxes in an array
            for (int n = 0; n < foundBoxes; n++)
            {
                var box = new BoundingBox
                {
                    centerX = output[0, n, 0] * scaleX - displayWidth / 2,
                    centerY = output[0, n, 1] * scaleY - displayHeight / 2,
                    width = output[0, n, 2] * scaleX,
                    height = output[0, n, 3] * scaleY,
                    label = labels[labelIDs[0, 0, n]],
                };
                box.area = box.width * box.height;
                bboxArray[n] = box;
            }

            // choose the bbox with the largest area
            float maxArea = -1f;
            int bboxIndex = -1;
            for (int i = 0; i < bboxArray.Length; i++)
            {
                if (bboxArray[i].area > maxArea)
                {
                    maxArea = bboxArray[i].area;
                    bboxIndex = i;
                }
            }
        
            // add bbox with largest area to list
            movingAverageBBoxes.Add(bboxArray[bboxIndex]);
        }
        
        if (frame % movingAverageWindow == 0)
        {
            // if not enough boxes were found dont display it
            if (movingAverageBBoxes.Count < movingAverageWindow / 2)
            {
                movingAverageBBoxes.Clear();
                ClearAnnotations();
                return;
            }
            // Calculate average center coordinates
            // Calculate total sums
            float totalCx = movingAverageBBoxes.Sum(box => box.centerX);
            float totalCy = movingAverageBBoxes.Sum(box => box.centerY);
            float totalWidth = movingAverageBBoxes.Sum(box => box.width);
            float totalHeight = movingAverageBBoxes.Sum(box => box.height);

            // Calculate averages
            float averageCx = totalCx / movingAverageBBoxes.Count;
            float averageCy = totalCy / movingAverageBBoxes.Count;
            float averageWidth = totalWidth / movingAverageBBoxes.Count;
            float averageHeight = totalHeight / movingAverageBBoxes.Count;
            
            // Dictionary to store label counts
            Dictionary<string, int> labelCounts = new Dictionary<string, int>();

            // Count occurrences of each label
            foreach (var bbox in movingAverageBBoxes)
            {
                if (!labelCounts.ContainsKey(bbox.label))
                {
                    labelCounts[bbox.label] = 0;
                }
                labelCounts[bbox.label]++;
            }
            
            // Find label with the maximum count
            string mostFrequentLabel = labelCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
            
            var box = new BoundingBox
            {
                centerX = averageCx,
                centerY = averageCy,
                width = averageWidth,
                height = averageHeight,
                label = mostFrequentLabel
            };
            ClearAnnotations();
            if (!labelBoxPoolID.ContainsKey(box.label))
            {
                dictIndex++;
                labelBoxPoolID.Add(box.label, dictIndex);
            }
            DrawBox(box, labelBoxPoolID[box.label]);
            movingAverageBBoxes.Clear();
        }
    }

    private void DrawBox(BoundingBox box, int id)
    {
        // Create the bounding box graphic or get from pool
        float[] values;
        GameObject panel;
        bool newBbox = false;
        
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            ingredientsArray = null;
            hasIngredients = false;
            panel = CreateNewBox(Color.magenta, box.label);
            newBbox = true;
        }

        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);
        currentLabel = box.label;
        
        if (!placedEagle || currentLabel != box.label || newBbox)
        {
            placedEagle = eagleManager.placeEagle(box.centerX, box.centerY);
        }

        if (currentLabel != box.label || !hasIngredients || newBbox)
        {
            StartCoroutine(foodFacts.GetRequest(box.label, result =>
            {
                values = result;
                ingredientsArray = foodFacts.getIngredients();
                
                // Handle the result here
                PieChart pieChart = panel.GetComponentInChildren<PieChart>();
                string productName = foodFacts.getProductName();
                if (!detailedNutritionDict.ContainsKey(productName))
                {
                    Debug.Log("Setting value to dict " + productName);
                    detailedNutritionDict.Add(productName, new DetailedNutrition(productName, values, foodFacts.getNutriGrade()));
                }
                pieChart.setValues(values);
                var textChildren = panel.GetComponentsInChildren<TextMeshProUGUI>();
                string[] type = { "Fett: ", "Kohlenhydrate: ", "Eiweiß: " };
                for (int i = 0; i < 3; i++)
                {
                    textChildren[i + 1].text = type[i] + values[i] + "g";
                }
                
                List<string> foundAllergies = new List<string>();
                
                textChildren[0].text = (int)values[0] * 9 + (int)values[1] * 4 + (int)values[2] * 4 + " kcal";
                hasIngredients = true;
                currentLabel = box.label;
                
                if (ingredientsArray == null || allergyList == null)
                {
                    Debug.Log("ingredients or allergyList null");
                    currentAllergyLabel.gameObject.SetActive(false);
                    return;
                }
        
                for (int i = 0; i < allergyList.Count; i++)
                {
                    if (ingredientsArray.ContainsInsensitive(allergyList[i]))
                    {
                        foundAllergies.Add(allergyList[i]);
                        Debug.Log("Found allergy " + allergyList[i]);
                    }
                }
                
                if (foundAllergies.Count > 0)
                {
                    currentAllergyLabel.gameObject.SetActive(true);
                    for (int i = 0; i < Math.Min(foundAllergies.Count, 3); i++)
                    {
                        detailedNutritionDict[productName].allergies.Add(foundAllergies[i]);
                        GameObject child = currentAllergyLabel.transform.GetChild(i + 1).gameObject;
                        child.GetComponentInChildren<TextMeshProUGUI>().text = foundAllergies[i];
                        child.SetActive(true);
                    }
                }
                else
                {
                    currentAllergyLabel.gameObject.SetActive(false);
                }

                if (foundAllergies.Count == 0)
                {
                    List<string> foundDeficiencies = new List<string>();
                    for (int i = 0; i < deficiencyList.Count; i++)
                    {
                        string[] richIngredients = deficiencyDict[deficiencyList[i]];
                        for (int j = 0; j < richIngredients.Length; j++)
                        {
                            if (ingredientsArray.ContainsInsensitive(richIngredients[j]))
                            {
                                foundDeficiencies.Add(deficiencyList[i]);
                            }
                        }
                    }

                    if (foundDeficiencies.Count > 0)
                    {
                        currentAllergyLabel.gameObject.SetActive(true);
                        for (int i = 0; i < Math.Min(foundDeficiencies.Count, 3); i++)
                        {
                            detailedNutritionDict[productName].defficiencies.Add(foundDeficiencies[i]);
                            GameObject child = currentAllergyLabel.transform.GetChild(i + 4).gameObject;
                            child.GetComponentInChildren<TextMeshProUGUI>().text = foundDeficiencies[i];
                            child.SetActive(true);
                        }
                    }
                }
            }));
        }
    }

    private GameObject CreateNewBox(Color boxColor, string label)
    {
        //Create the box and set image
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = boxColor;
        img.sprite = boxTexture;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        createNutritionLabel(panel, label);

        boxPool.Add(panel);
        return panel;
    }

    private void createNutritionLabel(GameObject panel, string label)
    {
        var nutritionLabel = Instantiate(nutritionLabelPrefab, panel.transform);
        currentAllergyLabel = nutritionLabel.transform.GetChild(1);
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
    }
    
    private void setupDeficiencyDict()
    {
        deficiencyDict = new Dictionary<string, string[]>()
        {
            { "Eisen", new [] 
            {
                "Sesam", "Pistazie", "Cashew"
            }},
            {"Jod", new []
            {
                "Fisch", "Milch", "Käse", "Brokkoli", "Spinat"
            }},
            { "Vitamin D", new []
            {
                "Fisch", "Champignon", "Pfifferling", "Steinpilz"
            }},
            { "Vitamin B12", new []
            {
                "Fisch", "Emmentaler", "Quark", "Milch"
            }},
            { "Magnesium", new []
                {
                    "Kürbiskern", "Sonnenblumenöl", "Cashew", "Erdnüsse", "Banane"
                }
            },
            { "Omega 3-Fettsäuren", new []
                {
                    "Lein", "Walnuss", "Fisch", "Soja", "Olivenöl", 
                }
            }
        };
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