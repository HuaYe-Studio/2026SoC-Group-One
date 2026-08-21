using System;
using System.Collections.Generic;
using AbilitySystem;
using UnityEngine;

public class ElementAbilityManager : MonoBehaviour
{
    [Serializable]
    public struct ElementAbilityDef
    {
        public ElementType type;
        public AbilityInputBinding binding;
    }

    [SerializeField] private List<ElementAbilityDef> elementDefinitions = new();

    private readonly HashSet<ElementType> _unlockedElements = new();
    private PlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

    public bool IsElementUnlocked(ElementType type) => _unlockedElements.Contains(type);

    public List<ElementType> GetUnlockedElements() => new(_unlockedElements);

    public void UnlockElement(ElementType type)
    {
        if (!_unlockedElements.Add(type)) return;

        var slime = _playerController != null ? _playerController.GetForm(FormType.Slime) : null;
        if (slime == null) return;

        // An element may grant several bindings (e.g. SpitFire start + stop), configured
        // as multiple ElementAbilityDef entries sharing the same type.
        foreach (var def in elementDefinitions)
            if (def.type == type && def.binding != null)
                slime.AddAbilityBinding(def.binding);
    }

    public void RestoreElements(IEnumerable<ElementType> elements)
    {
        _unlockedElements.Clear();
        if (elements == null) return;
        foreach (var e in elements)
            UnlockElement(e);
    }
}
