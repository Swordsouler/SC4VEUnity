using System;
using UnityEngine;
using UnityEngine.Events;

namespace Sven.Command
{
    [Serializable]
    public class EventParameter : ScriptableObject, IBaseParameter
    {
        [SerializeField]
        private UnityEvent _actions = new();
        public UnityEvent Actions
        {
            get => _actions; set => _actions = value;
        }

        public EventParameter() { }

        public EventParameter(EventCommandEntry entry)
        {
            if (entry == null) return;
            _actions = entry.EventParameter.Actions;
        }
    }
}