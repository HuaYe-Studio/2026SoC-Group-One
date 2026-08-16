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
    private readonly Dictionary<ElementType, AbilityInputBinding> _typeToBinding = new();
    private PlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        foreach (var def in elementDefinitions)
            if (def.binding != null && !_typeToBinding.ContainsKey(def.type))
                _typeToBinding[def.type] = def.binding;
    }

    public bool IsElementUnlocked(ElementType type) => _unlockedElements.Contains(type);

    public List<ElementType> GetUnlockedElements() => new(_unlockedElements);

    public void UnlockElement(ElementType type)
    {
        if (!_unlockedElements.Add(type)) return;
        if (!_typeToBinding.TryGetValue(type, out var binding)) return;

        var slime = _playerController != null ? _playerController.GetForm(FormType.Slime) : null;
        slime?.AddAbilityBinding(binding);
    }

    public void RestoreElements(IEnumerable<ElementType> elements)
    {
        _unlockedElements.Clear();
        if (elements == null) return;
        foreach (var e in elements)
            UnlockElement(e);
    }
}
