using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
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
        if (button.colors.selectedColor.CompareRGB(image.color))
        {
            image.color = button.colors.normalColor;
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0.5f);
            if (gameObject.CompareTag("Allergy"))
            {
                AllergyList.Instance.allergyList.Remove(text.text);
            }
            else
            {
                AllergyList.Instance.deficiencyList.Remove(text.text);
            }
        }
        // Button was not pressed before
        else
        {
            image.color = button.colors.selectedColor;
            image.color = new Color(image.color.r, image.color.g, image.color.b, 255);
            if (gameObject.CompareTag("Allergy"))
            {
                Debug.Log("Füge hinzu");
                AllergyList.Instance.allergyList.Add(text.text);
            }
            else
            {
                AllergyList.Instance.deficiencyList.Add(text.text);
            }
        }
    }
    
}
