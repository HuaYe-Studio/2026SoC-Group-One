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

    public static event Action<DevourableAnimal> OnAnimalEnterRange;

    public static void TriggerAnimalEnterRange(DevourableAnimal animal)
    {
        OnAnimalEnterRange?.Invoke(animal);
    }

    public static event Action<DevourableAnimal> OnAnimalExitRange;

    public static void TriggerAnimalExitRange(DevourableAnimal animal)
    {
        OnAnimalExitRange?.Invoke(animal);
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

    /// <summary>某只动物被玩家吞噬。</summary>
    public static event System.Action<GameObject> OnAnimalDevoured;

    public static void TriggerAnimalDevoured(GameObject victim)
    {
        OnAnimalDevoured?.Invoke(victim);
    public static event System.Action<float, float> OnStaminaChanged;

    public static void TriggerStaminaChanged(float current, float max)
    {
        OnStaminaChanged?.Invoke(current, max);
    }

}
