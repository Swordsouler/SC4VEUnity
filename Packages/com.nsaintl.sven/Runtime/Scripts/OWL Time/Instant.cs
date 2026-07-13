// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using Sven.Utils;
using Sven.XsdData;
using System;
using VDS.RDF;

namespace Sven.OwlTime
{
    /// <summary>
    /// Represents an instant in time.
    /// </summary>
    public class Instant : TemporalEntity
    {

        /// <summary>
        /// The instant in XSDDateTime format
        /// </summary>
        public readonly DateTime inXSDDateTime;

        /// <summary>
        /// The instant in XSDDate format
        /// </summary>
        private readonly XSDDate inXSDDate;

        /// <summary>
        /// The instant in XSDDateTimeStamp format
        /// </summary>
        private readonly XSDDateTimeStamp inXSDTimeStamp;

        /// <summary>
        /// Initializes a new instance of the Instant class.
        /// </summary>
        /// <returns></returns>
        public Instant() : this(DateTime.Now) { }

        /// <summary>
        /// Initializes a new instance of the Instant class with the specified date and time.
        /// </summary>
        /// <param name="dateTime">The date and time to initialize the instance with.</param>
        public Instant(DateTime dateTime) : base(dateTime)
        {
            inXSDDateTime = dateTime;
            inXSDDate = new XSDDate(dateTime);
            inXSDTimeStamp = new XSDDateTimeStamp(dateTime);
        }

        /// <summary>
        /// Semantizes the temporal entity in the graph.
        /// </summary>
        /// <param name="graph">The graph to semantize the temporal entity.</param>
        public new IUriNode Semanticize()
        {
            IUriNode instantNode = base.Semanticize();
            if (SvenSettings.UseInside) Interval.SemanticizeInside(this);
            if (inXSDDateTime != null) GraphManager.Assert(new Triple(instantNode, GraphManager.CreateUriNode("time:inXSDDateTime"), inXSDDateTime.ToLiteralNode()));
            // seems to be useless in our case
            // if (inXSDDate != null) graph.Assert(new Triple(temporalEntityNode, graph.CreateUriNode("time:inXSDDate"), inXSDDate.ToLiteralNode(graph)));
            // if (inXSDTimeStamp != null) graph.Assert(new Triple(temporalEntityNode, graph.CreateUriNode("time:inXSDTimeStamp"), inXSDTimeStamp.ToLiteralNode(graph)));
            return instantNode;
        }
    }
}