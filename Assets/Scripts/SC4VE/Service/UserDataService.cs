using Sc4ve.Multimodality;
using UnityEngine;

namespace Sc4ve.Service
{
    public class UserDataService : IService<UserData>
    {
        public UserData Instantiate()
        {
            GameObject go = new("User Data");
            GameObject.DontDestroyOnLoad(go);
            return go.AddComponent<UserData>();
        }
    }
}