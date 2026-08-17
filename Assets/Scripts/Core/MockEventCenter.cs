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

    public static event Action<int, int> OnCheckPlayerHP;

    public static void TriggerCheckPlayerHP(int currentHP, int maxHP)
    {
        OnCheckPlayerHP?.Invoke(currentHP, maxHP);
    }

    public static event Action<int, int> OnPlayerHurt;

    public static void TriggerPlayerHurt(int currentHP, int maxHP)
    {
        OnPlayerHurt?.Invoke(currentHP, maxHP);
        Debug.Log($"Player hurt: {currentHP}/{maxHP}");
    }

    public static event Action<int, int> OnPlayerHeal;

    public static void TriggerPlayerHeal(int currentHP, int maxHP)
    {
        OnPlayerHeal?.Invoke(currentHP, maxHP);
        Debug.Log($"Player healed: {currentHP}/{maxHP}");
    }

    public static event Action OnPlayerDeath;

    public static void TriggerPlayerDeath()
    {
        OnPlayerDeath?.Invoke();
    }

    public static event Action OnPlayerRespawn;

    public static void TriggerPlayerRespawn()
    {
        OnPlayerRespawn?.Invoke();
    }

    /// <summary>
    /// 某只动物被攻击（任何来源：玩家吞噬、敌方碰撞、陷阱等）。
    /// 供通用复仇机制感知：攻击者是谁，受害者是谁。
    /// </summary>
    public static event Action<GameObject, GameObject, float> OnAnimalAttacked;

    public static void TriggerAnimalAttacked(GameObject victim, GameObject attacker, float damage)
    {
        OnAnimalAttacked?.Invoke(victim, attacker, damage);
    }
}
