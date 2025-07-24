using System.Threading.Tasks;

namespace Sven.Command
{
    public interface IBaseCommand
    {
        Task Execute();
    }
}