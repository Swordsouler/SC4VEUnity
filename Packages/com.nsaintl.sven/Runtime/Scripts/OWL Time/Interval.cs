// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using System;
using VDS.RDF;

namespace Sven.OwlTime
{
    /// <summary>
    /// Represents a time interval with a specific instant inside it.
    /// </summary>
    public class Interval : TemporalEntity
    {
        /// <summary>
        /// Initializes a new instance of the Interval class.
        /// </summary>
        public Interval() : base(Guid.NewGuid().ToString()) { }

        /// <summary>
        /// Initializes a new instance of the Interval class with the specified UUID.
        /// </summary>
        /// <param name="UUID">The UUID to initialize the instance with.</param>
        public Interval(string UUID) : base(UUID) { }

        /// <summary>
        /// Initializes a new instance of the Interval class.
        /// </summary>
        public Interval(DateTime dateTime) : this() { }

        /// <summary>
        /// Starts the interval.
        /// </summary>
        /// <param name="previous">The interval that occurs before this one.</param>
        public new void Start(Instant instant, TemporalEntity previous = null)
        {
            if (intervals.Contains(this)) return;
            intervals.Add(this);
            base.Start(instant, previous);
        }

        /// <summary>
        /// Ends the interval.
        /// </summary>
        /// <param name="next">The interval that occurs after this one.</param>
        public new void End(Instant instant, TemporalEntity next = null)
        {
            if (!intervals.Contains(this)) return;
            intervals.Remove(this);
            base.End(instant, next);
        }

        /// <summary>
        /// Semantizes the instant inside the interval.
        /// </summary>
        /// <param name="graph">The graph to semantize the interval.</param>
        /// <param name="instant">The instant to semantize inside the interval.</param>
        public static void SemanticizeInside(Instant instant)
        {
            IUriNode instantNode = instant.UriNode;

            foreach (Interval interval in intervals)
            {
                GraphManager.Assert(new Triple(interval.UriNode, GraphManager.CreateUriNode("time:inside"), instantNode));
            }
        }
    }
}