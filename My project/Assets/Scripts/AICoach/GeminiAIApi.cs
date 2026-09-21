using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Text;
using System.Collections;
using System;

public class GeminiAIApi : MonoBehaviour
{
    private const string API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    [Serializable]
    private class GeminiRequest
    {
        public Content[] contents;
    }

    [Serializable]
    private class Content
    {
        public Part[] parts;
    }

    [Serializable]
    private class Part
    {
        public string text;
    }

    [Serializable]
    private class GeminiResponse
    {
        public Candidate[] candidates;
    }

    [Serializable]
    private class Candidate
    {
        public Content content;
    }

    public void AnalyzeSwing(string apiKey, float wristAngle, float kneeBend, float swingSpeed, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(SendRequest(apiKey, wristAngle, kneeBend, swingSpeed, onSuccess, onError));
    }

    // Workaround for Unity Curl error 35: Cert verify failed
    private class BypassCertificate : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true; // Always accept
        }
    }

    private IEnumerator SendRequest(string apiKey, float wristAngle, float kneeBend, float swingSpeed, Action<string> onSuccess, Action<string> onError)
    {
        string prompt = $"You are an expert Pickleball coach. I just performed a swing. My wrist angle was {wristAngle:F1} degrees, my knee bend was {kneeBend:F1} degrees, and my swing speed was {swingSpeed:F1} mph. Provide a brief (2-3 sentences), encouraging feedback on how to improve my technique based on these metrics.";

        GeminiRequest requestObj = new GeminiRequest
        {
            contents = new Content[]
            {
                new Content
                {
                    parts = new Part[]
                    {
                        new Part { text = prompt }
                    }
                }
            }
        };

        string jsonBody = JsonUtility.ToJson(requestObj);
        string url = $"{API_URL}?key={apiKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.certificateHandler = new BypassCertificate();
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error + "\n" + request.downloadHandler.text);
            }
            else
            {
                try
                {
                    GeminiResponse responseObj = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                    if (responseObj.candidates != null && responseObj.candidates.Length > 0)
                    {
                        string text = responseObj.candidates[0].content.parts[0].text;
                        onSuccess?.Invoke(text);
                    }
                    else
                    {
                        onError?.Invoke("Unexpected response format.");
                    }
                }
                catch (Exception e)
                {
                    onError?.Invoke("Error parsing response: " + e.Message);
                }
            }
        }
    }
}
