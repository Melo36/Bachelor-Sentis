using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Random = UnityEngine.Random;

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
    [SerializeField]
    private GameObject detailedView;
    [SerializeField]
    private Button closeButton;

    public Button closeComparisonButton;

    [SerializeField] private RunYOLO8n yolov8;

    [Header("Detail Overview Settings")] 
    public TextMeshProUGUI productName;

    public RawImage productImage;
    public PieChart pieChart;
    public TextMeshProUGUI calorieText;
    public TextMeshProUGUI fettText;
    public TextMeshProUGUI kohlenText;
    public TextMeshProUGUI eiweisText;

    public Texture[] textureArray = new Texture[5];
    public Texture[] nutriscoreTextures = new Texture[5];
    public RawImage nutriscoreImage;
    private Dictionary<string, Texture> nutriDict;

    private DemoController demoController;
    private Animator animator;
    private int itemCount = 0;

    private GameObject textRoot;

    public GameObject[] checkmarkTexts;
    public GameObject deficiencyWhiteBox;
    public GameObject bottomNutriScore;

    private SetupComparison comparison;
    public GameObject comparisonPage;

    private bool comparisonInitialized = false;
    
    void Start()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        arPlaneManager = GetComponent<ARPlaneManager>();
        eagle.GetComponentInChildren<BillboardText>().mainCamera = arCamera;
        closeButton.onClick.AddListener(closeDetailedView);
        closeComparisonButton.onClick.AddListener(closeCopmarisonView);

        nutriDict = new Dictionary<string, Texture>();
        nutriDict.Add("a", nutriscoreTextures[0]);
        nutriDict.Add("b", nutriscoreTextures[1]);
        nutriDict.Add("c", nutriscoreTextures[2]);
        nutriDict.Add("d", nutriscoreTextures[3]);
        nutriDict.Add("e", nutriscoreTextures[4]);

        comparison = gameObject.GetComponent<SetupComparison>();
    }

    private void closeDetailedView()
    {
        detailedView.SetActive(false);
        if (comparisonInitialized)
        {
            comparisonPage.SetActive(true);
        }
    }

    private void closeCopmarisonView()
    {
        comparisonPage.SetActive(false);
    }

    private void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                Ray ray = arCamera.ScreenPointToRay(touch.position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    Collider collider = hit.collider;
                    if (collider != null && collider.CompareTag("Eagle"))
                    {
                        // The user clicked on the eagle GameObject
                        Debug.Log("Eagle Clicked!");
                        
                        Dictionary<string, DetailedNutrition> detailedNutritionDict = yolov8.detailedNutritionDict;
                        
                        if (itemCount > 0 && itemCount == detailedNutritionDict.Count)
                        {
                            float random = Random.Range(0.0f, 1.0f);
                            textRoot.SetActive(false);
                            if (random < 0.5f)
                            {
                                animator.SetTrigger("Leave");
                                StartCoroutine(waitFor(3f));
                            }
                            else
                            {
                                animator.SetTrigger("Hit");
                                StartCoroutine(waitFor(2f));
                            }
                            return;
                        }

                        itemCount = detailedNutritionDict.Count;
                        
                        DetailedNutrition healthiestChoice = null;
                        DetailedNutrition worstChoice = null;
                        string minNutriGrade = "e";
                        string maxNutriGrade = "a";
                        foreach (KeyValuePair<string, DetailedNutrition> choice in detailedNutritionDict)
                        {
                            if (choice.Value.allergies.Count > 0)
                            {
                                continue;
                            }
                            if (string.Compare(choice.Value.nutriscore, minNutriGrade) < 0)
                            {
                                minNutriGrade = choice.Value.nutriscore;
                                healthiestChoice = choice.Value;
                            }
                            else if(string.Compare(choice.Value.nutriscore, maxNutriGrade) > 0)
                            {
                                maxNutriGrade = choice.Value.nutriscore;
                                worstChoice = choice.Value;
                            }
                        }
                        
                        if (healthiestChoice == null)
                        {
                            healthiestChoice = detailedNutritionDict.First().Value;
                        }

                        if (healthiestChoice != null)
                        {
                            productName.text = healthiestChoice.productName;
                            pieChart.setValues(healthiestChoice.nutritionValues);
                            fettText.text = "Fett                                  " + healthiestChoice.nutritionValues[0];
                            kohlenText.text = "Kohlenhydrate                 " + healthiestChoice.nutritionValues[1];
                            eiweisText.text = "Eiweiß                              " + healthiestChoice.nutritionValues[2];
                            calorieText.text = (int)healthiestChoice.nutritionValues[0] * 9 + (int)healthiestChoice.nutritionValues[1] * 4 + (int)healthiestChoice.nutritionValues[2] * 4 + " kcal";
                            nutriscoreImage.texture = nutriDict[healthiestChoice.nutriscore];
                            nutriscoreImage.transform.localScale = new Vector3(2.5f, 2.5f);
                            switch (healthiestChoice.productName)
                            {
                                case "Ferrero Küsschen":
                                    productImage.texture = textureArray[0];
                                    break;
                                case "Butterkeks 30% weniger Zucker":
                                    productImage.texture = textureArray[1];
                                    break;
                                case "Leibniz Kakao Keks":
                                    productImage.texture = textureArray[2];
                                    break;
                                case "Lindt":
                                    productImage.texture = textureArray[3];
                                    break;
                                case "Milka Lait Alpin":
                                    productImage.texture = textureArray[4];
                                    break;
                            }
                            productImage.SetNativeSize();

                            if (healthiestChoice.defficiencies.Count > 0)
                            {
                                deficiencyWhiteBox.SetActive(true);
                                bottomNutriScore.SetActive(true);
                                List<string> def = healthiestChoice.defficiencies;
                                for (int i = 0; i < Math.Min(def.Count, 3); i++)
                                {
                                    checkmarkTexts[i].SetActive(true);
                                    checkmarkTexts[i].GetComponentInChildren<TextMeshProUGUI>().text = def[i];
                                }
                            }

                            if (worstChoice != null)
                            {
                                comparisonInitialized = true;
                                comparison.setupComparisonPage(healthiestChoice, worstChoice);
                            }
                        }
                        detailedView.SetActive(true);
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
            
            animator = instantiatedEagle.GetComponent<Animator>();
            textRoot = instantiatedEagle.transform.Find("TextRoot").gameObject;
            
            foreach (var plane in arPlaneManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
            arPlaneManager.enabled = false;
            return true;
        }
        return false;
    }
    
    private IEnumerator waitFor(float length)
    {
        yield return new WaitForSeconds(length); 
        textRoot.SetActive(true);
    }
}
