namespace Sc4ve.Service
{
    public interface IService<T>
    {
        T Instantiate();
    }
}