using Sven.Command;

namespace Sc4ve.Voice
{
    public class RecognitionResult
    {
        public const string AlternativesKey = "alternatives";
        public const string ResultKey = "result";
        public const string PartialKey = "partial";

        public Sentence[] Phrases;
        public bool Partial;

        public RecognitionResult(string json)
        {
            JSONObject resultJson = JSONNode.Parse(json).AsObject;

            if (resultJson.HasKey(AlternativesKey))
            {
                var alternatives = resultJson[AlternativesKey].AsArray;
                Phrases = new Sentence[alternatives.Count];

                for (int i = 0; i < Phrases.Length; i++)
                {
                    Phrases[i] = new Sentence(alternatives[i].AsObject);
                }

            }
            else if (resultJson.HasKey(ResultKey))
            {
                Phrases = new Sentence[] { new(resultJson.AsObject) };
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