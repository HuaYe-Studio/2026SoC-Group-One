using System;
using UnityEngine;

public static class MockEventCenter
{
    public static event Action<FormType> OnFormUnlocked;

    public static void TriggerFormUnlock(FormType newForm)
    {
        OnFormUnlocked?.Invoke(newForm);
        Debug.Log($"Form unlocked: {newForm}");
    }

    public static event Action<IDevourable> OnDevourableEnterRange;

    public static void TriggerDevourableEnterRange(IDevourable target)
    {
        OnDevourableEnterRange?.Invoke(target);
    }

    public static event Action<IDevourable> OnDevourableExitRange;

    public static void TriggerDevourableExitRange(IDevourable target)
    {
        OnDevourableExitRange?.Invoke(target);
    }

    public static event Action<FormType> OnFormChanged;

    public static void TriggerFormChange(FormType formType)
    {
        OnFormChanged?.Invoke(formType);
    }

    public static event System.Action<int, int> OnCheckPlayerHP;

    public static void TriggerCheckPlayerHP(int currentHP, int maxHP)
    {
        OnCheckPlayerHP?.Invoke(currentHP, maxHP);
    }

    public static event System.Action<int, int> OnPlayerHurt;

    public static void TriggerPlayerHurt(int currentHP, int maxHP)
    {
        OnPlayerHurt?.Invoke(currentHP, maxHP);
        Debug.Log($"Player hurt: {currentHP}/{maxHP}");
    }

    public static event System.Action<int, int> OnPlayerHeal;

    public static void TriggerPlayerHeal(int currentHP, int maxHP)
    {
        OnPlayerHeal?.Invoke(currentHP, maxHP);
        Debug.Log($"Player healed: {currentHP}/{maxHP}");
    }

    public static event System.Action OnPlayerDeath;

    public static void TriggerPlayerDeath()
    {
        OnPlayerDeath?.Invoke();
    }

    public static event System.Action<GameObject> OnAnimalDevoured;

    public static void TriggerAnimalDevoured(GameObject victim)
    {
        OnAnimalDevoured?.Invoke(victim);
    }
    public static event System.Action<float, float> OnStaminaChanged;

    public static void TriggerStaminaChanged(float current, float max)
    {
        OnStaminaChanged?.Invoke(current, max);
    }

    /// <summary>某只动物被玩家吞噬（用于同类复仇感知：如冲冲羊感知同类被吃后冲撞攻击）。</summary>
    public static event System.Action<GameObject> OnAnimalDevoured;

    public static void TriggerAnimalDevoured(GameObject victim)
    {
        OnAnimalDevoured?.Invoke(victim);
    }

}
