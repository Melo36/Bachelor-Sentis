using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FoodFacts : MonoBehaviour
{
    
    private Dictionary<string, string> dict;

    [System.Serializable]
    public class Nutriments
    {
        public float proteins_100g;
        public float carbohydrates_100g;
        public float fat_100g;
    }
    
    [System.Serializable]
    public class NutriscoreData
    {
        public float proteins;
        public float carbohydrates;
        public float fat;
    }

    [System.Serializable]
    public class Product
    {
        public Nutriments nutriments;
        public NutriscoreData nutriscore_data;
        public string product_name;
        public string ingredients_text;
    }
    
    [System.Serializable]
    public class ApiResponse
    {
        public string code;
        public Product product;
        public int status;
        public string status_verbose;
    }

    private string ingredientsText;

    
    // Start is called before the first frame update
    void Start()
    {
        dict = new Dictionary<string, string>()
        {
            {"ferrero_klassik", "8000500303627"},
            {"leibniz_butter", "4017100122910"},
            {"leibniz_kakao", "4017100110818"},
            {"lindt_lindor", "4000539113147"},
            {"milka_vollmilch", "3045140105502"}
        };
        //callRequest("haferflocken_ja");
    }

    public IEnumerator GetRequest(string productName, System.Action<float[]> callback)
    {
        string barcode = dict[productName];
        string foodFactLink = "https://world.openfoodfacts.net/api/v2/product/" + barcode 
                                                                         + "?fields=product_name,nutriscore_data,nutriments,nutrition_grades,ingredients_text";
        Debug.Log(foodFactLink);
        using (UnityWebRequest webRequest = UnityWebRequest.Get(foodFactLink))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            string[] pages = foodFactLink.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(pages[page] + ": Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(pages[page] + ": HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);
                    string facts = webRequest.downloadHandler.text;
                    ApiResponse response = JsonUtility.FromJson<ApiResponse>(facts);
                    
                    if (response != null && response.product != null)
                    {
                        Nutriments productNutriments = new Nutriments();
                        productNutriments.proteins_100g = response.product.nutriments.proteins_100g;
                        productNutriments.carbohydrates_100g = response.product.nutriments.carbohydrates_100g;
                        productNutriments.fat_100g = response.product.nutriments.fat_100g;
                        ingredientsText = response.product.ingredients_text;
                        float[] result = getNutriments(productNutriments);
                        callback?.Invoke(result);
                    }
                    else
                    {
                        Debug.LogError("Failed to parse JSON response");
                    }
                    break;
            }
        }
    }
    
    public float[] getNutriments(Nutriments productNutriments)
    {
        float[] values = new float[3];
        values[0] = productNutriments.fat_100g;
        values[1] = productNutriments.carbohydrates_100g;
        values[2] = productNutriments.proteins_100g;
        return values;
    }

    public string getIngredients()
    {
        return ingredientsText;
    }
}
