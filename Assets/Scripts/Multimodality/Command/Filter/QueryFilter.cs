using Sven.Content;
using Sven.GraphManagement;
using Sven.OwlTime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sven.Command
{
    public abstract class QueryFilter : Command
    {
        private readonly Instant _instant;
        public Instant Instant => _instant;

        public QueryFilter()
        {
            _instant = GraphManager.SearchInstant(DateTime.Now);
        }

        public QueryFilter(DateTime dateTime)
        {
            _instant = GraphManager.SearchInstant(dateTime);
        }

        public QueryFilter(Instant instant)
        {
            _instant = instant;
        }

        public abstract Task<List<SemantizationCore>> Query();
    }
}