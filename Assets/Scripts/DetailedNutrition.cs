using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetailedNutrition
{
    public string productName;
    public float[] nutritionValues;
    public List<string> allergies = new List<string>();
    public string nutriscore;

    public DetailedNutrition(string productName, float[] nutritionValues, string nutriscore)
    {
        this.productName = productName;
        this.nutritionValues = nutritionValues;
        this.nutriscore = nutriscore;
    }
}
