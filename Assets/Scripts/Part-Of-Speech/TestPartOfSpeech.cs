using UnityEngine;
using System.Collections.Generic;

public class TestPartOfSpeech : MonoBehaviour
{
    [Tooltip("Location of the model file")]
    public string ModelPath = "P:/fr-pos-perceptron.bin";
    public string text = "Votre texte ici";

    private void Start()
    {
    }

    private List<string> GetPartOfSpeech(string text)
    {
        return null;
    }
}