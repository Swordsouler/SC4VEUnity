// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using System;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sven.XsdData
{
    /// <summary>
    /// Represents an XSD duration.
    /// </summary>
    public class XSDDuration
    {
        /// <summary>
        /// The duration string.
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Initializes a new instance of the XSDDuration class with the specified duration string.
        /// </summary>
        /// <param name="start">The start date and time.</param>
        /// <param name="end">The end date and time.</param>
        public XSDDuration(DateTime start, DateTime end)
        {
            // Une durée écoulée entre deux instants se calcule à partir du TimeSpan, qui est exact.
            // L'ancienne version mélangeait des jours calendaires (end.Day - start.Day, qui ignore l'heure) avec
            // les composantes horaires du TimeSpan, ce qui surcomptait d'un jour tout intervalle traversant minuit
            // (ex. 23:00 -> 01:00 donnait "P1DT2H" = 26 h au lieu de "PT2H"). On n'émet plus d'années/mois
            // (ambigus en xsd:duration) : days/hours/minutes/seconds suffisent et sont sans ambiguïté.
            Value = BuildDurationString(end - start);
        }

        /// <summary>
        /// Builds an xsd:duration string from an exact elapsed TimeSpan.
        /// </summary>
        /// <param name="span">The elapsed time span.</param>
        /// <returns>The duration string.</returns>
        private string BuildDurationString(TimeSpan span)
        {
            bool negative = span < TimeSpan.Zero;
            if (negative) span = span.Negate();

            int days = (int)span.TotalDays;
            int hours = span.Hours;
            int minutes = span.Minutes;
            double seconds = span.Seconds + span.Milliseconds / 1000.0;

            string duration = "P";
            if (days > 0) duration += $"{days}D";
            if (hours > 0 || minutes > 0 || seconds > 0)
            {
                duration += "T";
                if (hours > 0) duration += $"{hours}H";
                if (minutes > 0) duration += $"{minutes}M";
                if (seconds > 0)
                {
                    string formattedSeconds = seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0');
                    if (formattedSeconds.EndsWith(".")) // Remove trailing dot if any
                    {
                        formattedSeconds = formattedSeconds.TrimEnd('.');
                    }
                    duration += $"{formattedSeconds}S";
                }
            }

            if (duration == "P") duration = "P0D";
            return negative ? "-" + duration : duration;
        }

        /// <summary>
        /// Returns the duration string.
        /// </summary>
        /// <returns>The duration string.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Converts the duration to a literal node.
        /// </summary>
        /// <param name="factory">The node factory.</param>
        /// <returns>The literal node.</returns>
        public ILiteralNode ToLiteralNode()
        {
            return GraphManager.CreateLiteralNode(Value, new Uri(XmlSpecsHelper.XmlSchemaDataTypeDuration));
        }
    }
}