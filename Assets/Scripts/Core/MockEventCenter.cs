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
        
}

public enum FormType { Slime, Frog, BubbleFish, Form4 }
