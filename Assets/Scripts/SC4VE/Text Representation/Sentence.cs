using Sven.GraphManagement;
using Sven.OwlTime;
using System;
using System.Collections.Generic;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sc4ve.Voice
{
    [Serializable]
    public class Sentence : Sven.Context.Event
    {
        public const string ConfidenceKey = "confidence";
        public const string TextKey = "text";
        public const string WordsKey = "result";


        private readonly float _confidence = 0.0f;
        public float Confidence => _confidence;

        private readonly string _text = "";
        public string Text => _text;

        private readonly List<Word> _words = new();
        public List<Word> Words => _words;

        public DateTime StartedAt => _words.Count > 0 ? _words[0].StartedAt : DateTime.MinValue;
        public DateTime EndedAt => _words.Count > 0 ? _words[^1].EndedAt : DateTime.MinValue;

        public Sentence(string text) : base(null)
        {
            _text = text;
            // split text and make a delay of 1 seconds between each word
            _words = new List<Word>();
            string[] wordArray = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            DateTime currentTime = DateTime.Now.AddSeconds(wordArray.Length);
            foreach (string word in wordArray)
            {
                _words.Add(new Word(word, currentTime, currentTime.AddSeconds(1)));
                currentTime = currentTime.AddSeconds(1);
            }
            _words.Sort((x, y) => x.StartedAt.CompareTo(y.StartedAt));
        }
        public Sentence(string text, List<Word> words) : base(null)
        {
            _text = text;
            _words = words ?? new List<Word>();
            _words.Sort((x, y) => x.StartedAt.CompareTo(y.StartedAt));
        }

        public Sentence(JSONObject json) : base(null)
        {
            if (json.HasKey(ConfidenceKey)) _confidence = json[ConfidenceKey].AsFloat;
            if (json.HasKey(TextKey)) _text = json[TextKey].Value.Trim();
            _words = new List<Word>();
            if (json.HasKey(WordsKey))
            {
                var wordsJson = json[WordsKey].AsArray;
                // get max of ended at to calculate offset
                float maxEndedAt = 0.0f;
                for (int i = 0; i < wordsJson.Count; i++)
                {
                    var wordJson = wordsJson[i].AsObject;
                    if (wordJson.HasKey("end"))
                    {
                        float endedAt = wordJson["end"].AsFloat;
                        if (endedAt > maxEndedAt) maxEndedAt = endedAt;
                    }
                }
                Debug.Log($"Max ended at: {maxEndedAt}");

                for (int i = 0; i < wordsJson.Count; i++)
                {
                    var wordJson = wordsJson[i].AsObject;
                    if (wordJson.HasKey("word") && wordJson.HasKey("start") && wordJson.HasKey("end"))
                    {
                        string wordText = wordJson["word"].Value.Trim();
                        DateTime startedAt = DateTime.Now.AddSeconds(wordJson["start"].AsFloat - maxEndedAt);
                        DateTime endedAt = DateTime.Now.AddSeconds(wordJson["end"].AsFloat - maxEndedAt);
                        _words.Add(new Word(wordText, startedAt, endedAt));
                    }
                }
                _words.Sort((x, y) => x.StartedAt.CompareTo(y.StartedAt));
            }
        }

        public Sentence() : base(null) { }

        public override string ToString()
        {
            return $"{_text} ({_confidence}) [{string.Join(", ", _words)}]";
        }

        public new IUriNode Semanticize()
        {
            IUriNode eventNode = UriNode;
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("rdf:type"), GraphManager.CreateUriNode($"sc4ve:{GetType().Name}")));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sven:hasTemporalExtent"), this.Interval.Semanticize()));
            if (_user != null) GraphManager.Assert(new Triple(GraphManager.CreateUriNode(":" + _user.UUID), GraphManager.CreateUriNode("sven:perform"), eventNode));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sc4ve:confidence"), GraphManager.CreateLiteralNode(Confidence.ToString(), UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeFloat))));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sc4ve:text"), GraphManager.CreateLiteralNode(Text, UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeString))));
            foreach (var word in Words)
            {
                word.Start(new Instant(word.StartedAt));
                word.End(new Instant(word.EndedAt));
                IUriNode wordNode = word.Semanticize();
                GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sc4ve:hasWord"), wordNode));
            }
            return eventNode;
        }
    }
}