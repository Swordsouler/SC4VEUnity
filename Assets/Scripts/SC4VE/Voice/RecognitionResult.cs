using Sven.Context;
using System;
using System.Collections.Generic;

namespace Sc4ve.Voice
{
    public class RecognitionResult
    {
        public const string AlternativesKey = "alternatives";
        public const string ResultKey = "result";
        public const string PartialKey = "partial";
        public const string ConfidenceKey = "confidence";
        public const string TextKey = "text";
        public const string WordsKey = "result";

        public Sentence[] Phrases;
        public bool Partial;

        public RecognitionResult(string json, DateTime recognizerInitializedAt)
        {
            JSONObject resultJson = JSONNode.Parse(json).AsObject;

            if (resultJson.HasKey(AlternativesKey))
            {
                var alternatives = resultJson[AlternativesKey].AsArray;
                Phrases = new Sentence[alternatives.Count];

                for (int i = 0; i < Phrases.Length; i++)
                {
                    Phrases[i] = ParseSentence(alternatives[i].AsObject, recognizerInitializedAt);
                }

            }
            else if (resultJson.HasKey(ResultKey))
            {
                Phrases = new Sentence[] { ParseSentence(resultJson.AsObject, recognizerInitializedAt) };
            }
            else if (resultJson.HasKey(PartialKey))
            {
                Partial = true;
                Phrases = new Sentence[] { new(resultJson[PartialKey]) };
            }
            else
            {
                Phrases = new[] { new Sentence() { } };
            }
        }

        /// <summary>
        /// Construit une Sentence (modèle SVEN) depuis le JSON au format Vosk :
        /// clés "text"/"confidence", et "result" contenant les mots ("word"/"start"/"end").
        /// </summary>
        private static Sentence ParseSentence(JSONObject json, DateTime recognizerInitializedAt)
        {
            float confidence = json.HasKey(ConfidenceKey) ? json[ConfidenceKey].AsFloat : 0f;
            string text = json.HasKey(TextKey) ? json[TextKey].Value.Trim() : "";
            List<Word> words = new();
            if (json.HasKey(WordsKey))
            {
                var wordsJson = json[WordsKey].AsArray;
                for (int i = 0; i < wordsJson.Count; i++)
                {
                    var wordJson = wordsJson[i].AsObject;
                    if (wordJson.HasKey("word") && wordJson.HasKey("start") && wordJson.HasKey("end"))
                    {
                        string wordText = wordJson["word"].Value.Trim();
                        DateTime startedAt = recognizerInitializedAt.AddSeconds(wordJson["start"].AsFloat);
                        DateTime endedAt = recognizerInitializedAt.AddSeconds(wordJson["end"].AsFloat);
                        words.Add(new Word(wordText, startedAt, endedAt));
                    }
                }
            }
            return new Sentence(text, words, confidence);
        }

        // to string
        public override string ToString()
        {
            string result = "RecognitionResult: \n";
            foreach (var phrase in Phrases)
            {
                result += $"- {phrase}\n";
            }
            return result;
        }
    }
}
