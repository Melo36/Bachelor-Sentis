using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PieChart : MonoBehaviour
{
    public Image[] imagesPieChart;

    public float[] values;
    
    void Start()
    {
        setValues(values);
    }
    
    public void setValues(float[] valuesToSet)
    {
        float totalValues = 0f;
        for (int i = 0; i < imagesPieChart.Length; i++)
        {
            totalValues += findPercentage(valuesToSet, i);
            imagesPieChart[i].fillAmount = totalValues;
        }
    }

    private float findPercentage(float[] valueToSet, int index)
    {
        float totalAmount = 0;
        for (int i=0;i<valueToSet.Length;i++)
        {
            totalAmount += valueToSet[i];
        }

        return valueToSet[index] / totalAmount;
    }
}
