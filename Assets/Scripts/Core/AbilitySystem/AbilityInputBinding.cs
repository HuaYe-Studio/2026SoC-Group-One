using System;
using UnityEngine;
using UnityEngine.Events;

namespace AbilitySystem
{
    [Serializable]
    public class AbilityInputBinding
    {
        public string abilityName;
        public InputActionSlot inputSlot;
        public InputPhase phase;
        public UnityEvent onAbilityActivated;
    }
}
