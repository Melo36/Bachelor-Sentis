using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SetupComparison : MonoBehaviour
{
    public RawImage firstImage;
    public RawImage secondImage;

    public TextMeshProUGUI[] kfk = new TextMeshProUGUI[3];
    public Image[] kfkPercentages = new Image[3];

    public Image firstProtein;
    public Image secondProtein;

    public TextMeshProUGUI firstProteinText;
    public TextMeshProUGUI secondProteinText;
    
    public Texture[] textureArray = new Texture[5];

    public void setupComparisonPage(DetailedNutrition healthyChoice, DetailedNutrition worstChoice)
    {
        float[] healthyValues = healthyChoice.nutritionValues;
        float[] badValues = worstChoice.nutritionValues;
        
        // set values for calories
        float caloriesOne = (int)healthyValues[0] * 9 + (int)healthyValues[1] * 4 + (int)healthyValues[2] * 4;
        float caloriesTwo = (int)badValues[0] * 9 + (int)badValues[1] * 4 + (int)badValues[2] * 4;
        float fillAmountCalories = caloriesOne / caloriesTwo;
        fillAmountCalories = Mathf.Round(fillAmountCalories * 10.0f) * 0.1f;
        kfk[0].text = "Kalorien                            -" + (100f - fillAmountCalories * 100f) + "%";
        kfkPercentages[0].fillAmount = 1f - fillAmountCalories;
        
        // set values for fat
        float fillAmountFat = healthyValues[0] / badValues[0];
        fillAmountFat = Mathf.Round(fillAmountFat * 10.0f) * 0.1f;
        kfk[1].text = "Fett                                   -" + (100f - fillAmountFat * 100f) + "%";
        kfkPercentages[1].fillAmount = 1f - fillAmountFat;
        
        // set values for carbohydrates
        float fillAmountCarbo = healthyValues[1] / badValues[1];
        fillAmountCarbo = Mathf.Round(fillAmountCarbo * 10.0f) * 0.1f;
        kfk[2].text = "Kohlenhydrate                 -" + (100f - fillAmountCarbo * 100f) + "%";
        kfkPercentages[2].fillAmount = 1f - fillAmountCarbo;

        if (fillAmountCarbo >= 1)
        {
            kfk[2].gameObject.SetActive(false);
        }
        
        // set values for protein
        float fillAmountProtein = badValues[2] / healthyValues[2];
        fillAmountProtein = Mathf.Round(fillAmountProtein * 10.0f) * 0.1f;
        secondProtein.fillAmount = fillAmountProtein;
        firstProteinText.text = healthyValues[2] + "g";
        secondProteinText.text = badValues[2] + "g";

        firstImage.texture = getGroceryTexture(healthyChoice.productName);
        secondImage.texture = getGroceryTexture(worstChoice.productName);
        
        firstImage.SetNativeSize();
        secondImage.SetNativeSize();

        if (healthyChoice.productName == "Butterkeks 30% weniger Zucker")
        {
            firstImage.transform.localScale = new Vector3(1.2f, 1.2f, 1);
        }
    }

    private Texture getGroceryTexture(string productName)
    {
        switch (productName)
        {
            case "Ferrero Küsschen":
                return textureArray[0];
                break;
            case "Butterkeks 30% weniger Zucker":
                return textureArray[1];
                break;
            case "Leibniz Kakao Keks":
                return textureArray[2];
                break;
            case "Lindt":
                return textureArray[3];
                break;
            case "Milka Lait Alpin":
                return textureArray[4];
                break;
            default:
                return null;
        }
    }
}
