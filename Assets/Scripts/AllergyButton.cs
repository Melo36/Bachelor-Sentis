using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class AllergyButton : MonoBehaviour
{
    private Button button;
    private Image image;
    private TextMeshProUGUI text;
    void Start()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        text = GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(AddAllergy);
    }

    private void AddAllergy()
    {
        // Button was pressed already
        if (image.color == button.colors.selectedColor)
        {
            image.color = button.colors.normalColor;
            if (gameObject.CompareTag("Allergy"))
            {
                AllergyList.allergyList.Remove(text.text);
            }
            else
            {
                AllergyList.defficiencyList.Remove(text.text);
            }
        }
        // Button was not pressed before
        else
        {
            image.color = button.colors.selectedColor;
            if (gameObject.CompareTag("Allergy"))
            {
                AllergyList.allergyList.Add(text.text);
            }
            else
            {
                AllergyList.defficiencyList.Add(text.text);
            }
        }

        
        
    }
    
}
