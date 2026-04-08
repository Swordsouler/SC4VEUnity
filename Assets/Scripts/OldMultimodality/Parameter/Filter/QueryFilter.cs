using Sven.Content;
using Sven.OwlTime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sven.Command
{
    public abstract class QueryFilter<T> : BaseSettings<T>, IQueryFilter where T : BaseSettingsGUI
    {
        private readonly Instant _instant;
        public Instant Instant => _instant;

        public QueryFilter()
        {
            _instant = new Instant(DateTime.UtcNow);
        }

        public QueryFilter(DateTime dateTime)
        {
            _instant = new Instant(dateTime);
        }

        public QueryFilter(Instant instant)
        {
            _instant = instant;
        }

        public Task<List<SemantizationCore>> Execute(IReadOnlyList<SemantizationCore> _)
        {
            return Query();
        }

        public abstract Task<List<SemantizationCore>> Query();
    }
}