using Sven.Context;
using Sven.GraphManagement;
using System;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sc4ve.Voice
{
    [Serializable]
    public class Word : Event
    {
        private readonly string _text;
        public string Text => _text;

        private readonly DateTime _startedAt;
        public DateTime StartedAt => _startedAt;

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

        public new IUriNode Semanticize()
        {
            IUriNode eventNode = UriNode;
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("rdf:type"), GraphManager.CreateUriNode($"sc4ve:{GetType().Name}")));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sven:hasTemporalExtent"), this.Interval.Semanticize()));
            if (_user != null) GraphManager.Assert(new Triple(GraphManager.CreateUriNode(":" + _user.UUID), GraphManager.CreateUriNode("sven:perform"), eventNode));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sc4ve:text"), GraphManager.CreateLiteralNode(Text, UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeString))));
            return eventNode;
        }
    }
}