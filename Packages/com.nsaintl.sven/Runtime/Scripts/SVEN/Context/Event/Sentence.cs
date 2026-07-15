// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using Sven.OwlTime;
using System;
using System.Collections.Generic;
using System.Globalization;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sven.Context
{
    /// <summary>
    /// A sentence said by the user, composed of words with their temporal extents.
    /// </summary>
    [Serializable]
    public class Sentence : Event
    {
        /// <summary>
        /// The confidence score of the recognized sentence.
        /// </summary>
        private readonly float _confidence = 0.0f;
        public float Confidence => _confidence;

        /// <summary>
        /// The text of the sentence.
        /// </summary>
        private readonly string _text = "";
        public string Text => _text;

        /// <summary>
        /// The words of the sentence, sorted by start time.
        /// </summary>
        private readonly List<Word> _words = new();
        public List<Word> Words => _words;

        /// <summary>
        /// The time at which the sentence started to be said.
        /// </summary>
        public DateTime StartedAt => _words.Count > 0 ? _words[0].StartedAt : DateTime.MinValue;

        /// <summary>
        /// The time at which the sentence ended to be said.
        /// </summary>
        public DateTime EndedAt => _words.Count > 0 ? _words[^1].EndedAt : DateTime.MinValue;

        public Sentence() : base(null) { }

        /// <summary>
        /// Builds a sentence from a plain text, synthesizing one-second word timings.
        /// </summary>
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

        public Sentence(string text, List<Word> words, float confidence) : this(text, words)
        {
            _confidence = confidence;
        }

        public override string ToString()
        {
            return $"{_text} ({_confidence}) [{string.Join(", ", _words)}]";
        }

        /// <summary>
        /// Semantizes the sentence and its words.
        /// </summary>
        public override IUriNode Semanticize()
        {
            IUriNode eventNode = base.Semanticize();
            // InvariantCulture: in a French locale ToString() would produce "0,87" — an invalid xsd:float literal.
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sven:confidence"), GraphManager.CreateLiteralNode(Confidence.ToString(CultureInfo.InvariantCulture), UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeFloat))));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sven:text"), GraphManager.CreateLiteralNode(Text, UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeString))));
            foreach (Word word in Words)
            {
                word.Start(new Instant(word.StartedAt));
                word.End(new Instant(word.EndedAt));
                IUriNode wordNode = word.Semanticize();
                GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sven:hasWord"), wordNode));
            }
            return eventNode;
        }
    }
}
