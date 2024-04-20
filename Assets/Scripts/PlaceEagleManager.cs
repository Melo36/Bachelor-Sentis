using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
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
    [SerializeField]
    private GameObject detailedView;
    [SerializeField]
    private Button closeButton;

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
    
    
    void Start()
    {
        arRaycastManager = GetComponent<ARRaycastManager>();
        arPlaneManager = GetComponent<ARPlaneManager>();
        eagle.GetComponentInChildren<BillboardText>().mainCamera = arCamera;
        closeButton.onClick.AddListener(closeDetailedView);

        nutriDict = new Dictionary<string, Texture>();
        nutriDict.Add("a", nutriscoreTextures[0]);
        nutriDict.Add("b", nutriscoreTextures[1]);
        nutriDict.Add("c", nutriscoreTextures[2]);
        nutriDict.Add("d", nutriscoreTextures[3]);
        nutriDict.Add("e", nutriscoreTextures[4]);
    }

    private void closeDetailedView()
    {
        detailedView.SetActive(false);
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
                        Dictionary<string, DetailedNutrition> detailedNutritionDict = yolov8.detailedNutritionDict;
                        
                        DetailedNutrition healthiestChoice = null;
                        string minNutriGrade = "e";
                        foreach (KeyValuePair<string, DetailedNutrition> choice in detailedNutritionDict)
                        {
                            if (choice.Value.allergies.Count > 0)
                            {
                                continue;
                            }
                            Debug.Log(choice.Value.productName + " " + choice.Value.nutriscore);
                            if (string.Compare(choice.Value.nutriscore, minNutriGrade) < 0)
                            {
                                minNutriGrade = choice.Value.nutriscore;
                                healthiestChoice = choice.Value;
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
                            fettText.text = "Fett: " + healthiestChoice.nutritionValues[0] + " g";
                            kohlenText.text = "Kohlenhydrate: " + healthiestChoice.nutritionValues[1] + " g";
                            eiweisText.text = "Eiweiß: " + healthiestChoice.nutritionValues[2] + " g";
                            calorieText.text = (int)healthiestChoice.nutritionValues[0] * 9 + (int)healthiestChoice.nutritionValues[1] * 4 + (int)healthiestChoice.nutritionValues[2] * 4 + " kcal";
                            nutriscoreImage.texture = nutriDict[healthiestChoice.nutriscore];
                            nutriscoreImage.transform.localScale = new Vector3(2, 2);
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
