using Sven.Content;
using System.Collections.Generic;

namespace Sven.Command
{
    public interface IBaseCommand<T>
    {
        public abstract T Execute(IReadOnlyList<SemantizationCore> semantizationCores);
    }
}