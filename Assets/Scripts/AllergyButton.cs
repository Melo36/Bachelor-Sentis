using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class AllergyButton : MonoBehaviour
{
    private Button button;
    private Image image;
    private TextMeshProUGUI text;

    public Sprite notSelectedImage;
    public Sprite selectedImage;

    private bool isSelected = false;
    
    void Start()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        text = GetComponentInChildren<TextMeshProUGUI>();
        button.onClick.AddListener(AddAllergy);
    }

    private void AddAllergy()
    {
        if (button.image.sprite == notSelectedImage)
        {
            isSelected = true;
            button.image.sprite = selectedImage;
        }
        else
        {
            isSelected = false;
            button.image.sprite = notSelectedImage;
        }
        if (!isSelected)
        {
            if (gameObject.CompareTag("Allergy"))
            {
                Debug.Log("Entferne");
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
