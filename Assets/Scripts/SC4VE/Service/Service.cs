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
                    // Application.isPlaying n'est lisible que sur le thread principal : appelé
                    // depuis un thread de fond (continuations des initialisations de vocabulaires
                    // en EditMode), on se comporte comme hors Play mode → pas d'instanciation.
                    try
                    {
                        if (!Application.isPlaying) return default;
                    }
                    catch (UnityException)
                    {
                        return default;
                    }
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