using UnityEngine;
using TMPro;
using System.Linq;

public class VoskResultTMP : MonoBehaviour
{
    public VoskSpeechToText VoskSpeechToText;
    public TextMeshProUGUI ResultText;

    void Awake()
    {
        VoskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;
    }

    private void OnTranscriptionResult(string obj)
    {
        var result = new RecognitionResult(obj);
        for (int i = 0; i < result.Phrases.Length; i++)
        {
            if (result.Phrases[i].Text == "") continue;
            if (i > 0)
            {
                ResultText.text += " |";
            }

            ResultText.text += result.Phrases[i].Text;
        }
        if (!result.Phrases.All(p => p.Text == ""))
            ResultText.text += "\n";
    }
}