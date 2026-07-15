// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using Sven.OwlTime;
using VDS.RDF;

namespace Sven.Context
{
    public class Event
    {
        /// <summary>
        /// The time at which the event occurred.
        /// </summary>
        private Interval _interval;
        protected Interval Interval => _interval;

        private string _label;
        public string Label => _label ?? GetType().Name;

        /// <summary>
        /// The unique identifier of the event.
        /// </summary>
        private readonly string _uuid;
        protected string UUID => _uuid;

        /// <summary>
        /// The user that triggered the event.
        /// </summary>
        protected User _user;

        public IUriNode UriNode => GraphManager.CreateUriNode(":" + _uuid);

        public Event(User user)
        {
            _user = user;
            _interval = new Interval();
            _uuid = System.Guid.NewGuid().ToString();
        }
        public Event(User user, string label)
        {
            _user = user;
            _interval = new Interval();
            _uuid = System.Guid.NewGuid().ToString();
            _label = label;
        }

        /// <summary>
        /// Starts the event.
        /// </summary>
        /// <param name="instant">The instant at which the event started.</param>
        public void Start(Instant instant)
        {
            _interval.Start(instant);
        }

        /// <summary>
        /// Ends the event.
        /// </summary>
        /// <param name="instant">The instant at which the event ended.</param>
        public void End(Instant instant)
        {
            _interval.End(instant);
        }

        /// <summary>
        /// Semantizes the event.
        /// </summary>
        /// <param name="graph">The graph to semantize the event.</param>
        public virtual IUriNode Semanticize()
        {
            IUriNode eventNode = UriNode;
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("rdf:type"), GraphManager.CreateUriNode($"sven:{GetType().Name}")));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("rdfs:label"), GraphManager.CreateLiteralNode(Label)));
            GraphManager.Assert(new Triple(eventNode, GraphManager.CreateUriNode("sven:hasTemporalExtent"), _interval.Semanticize()));
            if (_user != null) GraphManager.Assert(new Triple(GraphManager.CreateUriNode(":" + _user.UUID), GraphManager.CreateUriNode("sven:perform"), eventNode));
            return eventNode;
        }
    }
}