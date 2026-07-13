// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace Sven.OwlTime
{
    /// <summary>
    /// Represents a proper interval in time with various temporal relationships.
    /// </summary>
    public class ProperInterval : Interval
    {
        /// <summary>
        // The interval that occurs after this interval
        /// </summary>
        public ProperInterval intervalAfter;

        /// <summary>
        // The interval that occurs before this interval
        /// </summary>
        public ProperInterval intervalBefore;

        /// <summary>
        // The interval that is contained within this interval
        /// </summary>
        public ProperInterval intervalContains;

        /// <summary>
        // The interval that is disjoint with this interval
        /// </summary>
        public ProperInterval intervalDisjoint;

        /// <summary>
        // The interval that occurs during this interval
        /// </summary>
        public ProperInterval intervalDuring;

        /// <summary>
        // The interval that is equal to this interval
        /// </summary>
        public ProperInterval intervalEquals;

        /// <summary>
        // The interval that is finished by this interval
        /// </summary>
        public ProperInterval intervalFinishedBy;

        /// <summary>
        // The interval that finishes this interval
        /// </summary>
        public ProperInterval intervalFinishes;

        /// <summary>
        // The interval that is within this interval
        /// </summary>
        public ProperInterval intervalIn;

        /// <summary>
        // The interval that meets this interval
        /// </summary>
        public ProperInterval intervalMeets;

        /// <summary>
        // The interval that is met by this interval
        /// </summary>
        public ProperInterval intervalMetBy;

        /// <summary>
        // The interval that is overlapped by this interval
        /// </summary>
        public ProperInterval intervalOverlappedBy;

        /// <summary>
        // The interval that overlaps this interval
        /// </summary>
        public ProperInterval intervalOverlaps;

        /// <summary>
        // The interval that is started by this interval
        /// </summary>
        public ProperInterval intervalStartedBy;

        /// <summary>
        // The interval that starts this interval
        /// </summary>
        public ProperInterval intervalStarts;

        /// <summary>
        /// Sets the interval that occurs after this interval and updates the before property of the specified interval.
        /// </summary>
        /// <param name="intervalAfter">The interval that occurs after this interval.</param>
        public void After(ProperInterval intervalAfter)
        {
            this.intervalAfter = intervalAfter;
            intervalAfter.intervalBefore = this;
        }

        /// <summary>
        /// Sets the interval that is met by this interval and updates the meets property of the specified interval.
        /// </summary>
        /// <param name="intervalMetBy">The interval that is met by this interval.</param>
        public void MetBy(ProperInterval intervalMetBy)
        {
            this.intervalMetBy = intervalMetBy;
            intervalMetBy.intervalMeets = this;
        }

        /// <summary>
        /// Sets the interval that is overlapped by this interval and updates the overlaps property of the specified interval.
        /// </summary>
        /// <param name="intervalOverlappedBy">The interval that is overlapped by this interval.</param>
        public void OverlappedBy(ProperInterval intervalOverlappedBy)
        {
            this.intervalOverlappedBy = intervalOverlappedBy;
            intervalOverlappedBy.intervalOverlaps = this;
        }

        /// <summary>
        /// Sets the interval that is started by this interval and updates the starts property of the specified interval.
        /// </summary>
        /// <param name="intervalStartedBy">The interval that is started by this interval.</param>
        public void StartedBy(ProperInterval intervalStartedBy)
        {
            this.intervalStartedBy = intervalStartedBy;
            intervalStartedBy.intervalStarts = this;
        }

        /// <summary>
        /// Sets the interval that is contained within this interval and updates the in property of the specified interval.
        /// </summary>
        /// <param name="intervalContains">The interval that is contained within this interval.</param>
        public void Contains(ProperInterval intervalContains)
        {
            this.intervalContains = intervalContains;
            intervalContains.intervalIn = this;
        }

        /// <summary>
        /// Sets the interval that is finished by this interval and updates the finishes property of the specified interval.
        /// </summary>
        /// <param name="intervalFinishedBy">The interval that is finished by this interval.</param>
        public void FinishedBy(ProperInterval intervalFinishedBy)
        {
            this.intervalFinishedBy = intervalFinishedBy;
            intervalFinishedBy.intervalFinishes = this;
        }

        /// <summary>
        /// Sets the interval that is equal to this interval and updates the equals property of the specified interval.
        /// </summary>
        /// <param name="intervalEquals">The interval that is equal to this interval.</param>
        public void Equals(ProperInterval intervalEquals)
        {
            this.intervalEquals = intervalEquals;
            intervalEquals.intervalEquals = this;
        }
    }
}