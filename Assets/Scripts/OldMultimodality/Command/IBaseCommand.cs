using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public interface IBaseCommand
    {
        DateTime CompletionTime { get; set; }
        Task Execute();
    }
}