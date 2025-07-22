using Sven.Content;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sven.Command
{
    public interface IQueryFilter : IBaseParameter
    {
        public abstract Task<List<SemantizationCore>> Execute();
    }
}