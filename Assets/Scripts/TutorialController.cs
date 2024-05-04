using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialController : MonoBehaviour
{
    public GameObject tutorialPanel;

    public GameObject eagle;
    public TextMeshProUGUI eagleText;
    private Animator animator;

    private TextMeshProUGUI buttonText;
    private Button button;
    public GameObject textRoot;
    private int position = 0;
    // Start is called before the first frame update
    void Start()
    {
        buttonText = GetComponentInChildren<TextMeshProUGUI>();
        button = GetComponent<Button>();
        
        button.onClick.AddListener(pressedContinue);
        animator = eagle.GetComponent<Animator>();

        Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        if (scene.name == "AllergySelectionScene" || scene.name == "DeficitSelectionScene")
        {
            position = 2;
        }
    }

    private void pressedContinue()
    {
        if (position == 0)
        {
            eagleText.text = "Klicken Sie auf <b>\"Produkt erkennen\"</b>, um die Kamera starten";
            position++;
        } else if (position == 1)
        {
            eagle.transform.localPosition = new Vector3(0, -2300, eagle.transform.localPosition.z);
            eagleText.text = "Klicken Sie auf <b>\"Allergien/Defizite eintragen\"</b>, um Allergien oder Defizite zu ergänzen";
            buttonText.text = "Fertig";
            position++;
        }
        else if (position == 2)
        {
            animator.SetTrigger("Leave");
            textRoot.SetActive(false);
            StartCoroutine(waitFor(2));
        }
    }
    
    private IEnumerator waitFor(float length)
    {
        yield return new WaitForSeconds(length); 
        tutorialPanel.SetActive(false);
    }
}
