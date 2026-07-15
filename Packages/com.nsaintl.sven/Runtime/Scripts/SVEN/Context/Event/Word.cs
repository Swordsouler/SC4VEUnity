// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using System;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sven.Context
{
    /// <summary>
    /// A word said by the user, with its temporal extent.
    /// </summary>
    [Serializable]
    public class Word : Event
    {
        /// <summary>
        /// The text of the word.
        /// </summary>
        private readonly string _text;
        public string Text => _text;

        /// <summary>
        /// The time at which the word started to be said.
        /// </summary>
        private readonly DateTime _startedAt;
        public DateTime StartedAt => _startedAt;

        /// <summary>
        /// The time at which the word ended to be said.
        /// </summary>
        private readonly DateTime _endedAt;
        public DateTime EndedAt => _endedAt;

        public Word(string text, DateTime startedAt, DateTime endedAt) : base(null)
        {
            _text = text;
            _startedAt = startedAt;
            _endedAt = endedAt;
        }

        public override string ToString()
        {
            return $"{_text} ({_startedAt:HH:mm:ss} - {_endedAt:HH:mm:ss})";
        }

        /// <summary>
        /// Semantizes the word.
        /// </summary>
        public override IUriNode Semanticize()
        {
            IUriNode eventNode = base.Semanticize();
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sven:text"), GraphManager.CreateLiteralNode(Text, UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeString))));
            return eventNode;
        }
    }
}
