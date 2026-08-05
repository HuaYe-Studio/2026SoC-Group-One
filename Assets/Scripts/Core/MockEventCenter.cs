using System;
using UnityEngine;

public static class MockEventCenter
{
    public static event Action<FormType> OnFormUnlocked;

    public static void TriggerFormUnlock(FormType newForm)
    {
        OnFormUnlocked?.Invoke(newForm);
        Debug.Log($"解锁形态：{newForm}");
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

    public static event System.Action<float, float> OnStaminaChanged;

    public static void TriggerStaminaChanged(float current, float max)
    {
        OnStaminaChanged?.Invoke(current, max);
    }

}

public enum FormType { Slime, Frog, BubbleFish, Form4 }
