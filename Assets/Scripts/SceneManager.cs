using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour
{
    public void changeScene(string scene)
    {
        Debug.Log("Changes scene");
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
    }
}
