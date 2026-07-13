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
    /// Represents a date in the XSD format
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Converts the date to a literal node.
        /// </summary>
        /// <param name="factory">The node factory.</param>
        /// <returns>The literal node.</returns>
        public static ILiteralNode ToLiteralNode(this DateTime dateTime)
        {
            return GraphManager.CreateLiteralNode(dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture), new Uri(XmlSpecsHelper.XmlSchemaDataTypeDateTime));
        }
    }
}