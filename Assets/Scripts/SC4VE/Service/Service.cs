using UnityEngine;

namespace Sc4ve.Service
{
    public abstract class Service { }

    public class Service<T, TService> where TService : IService<T>, new()
    {
        private T _instance;

        public T Instance
        {
            get
            {
                if (!IsInstantiated)
                {
                    if (!Application.isPlaying) return default;
                    var service = new TService();
                    _instance = service.Instantiate();
                }
                return _instance;
            }
        }

        public void Preload()
        {
            var _ = Instance;
        }

        public bool IsInstantiated => _instance != null && !(_instance is UnityEngine.Object unityObj && unityObj == null);
    }
}