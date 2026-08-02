using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class EventSystemEnsurer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);

        if (eventSystems.Length == 0)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            Object.DontDestroyOnLoad(go);
        }
        else
        {
            for (int i = 1; i < eventSystems.Length; i++)
                Object.Destroy(eventSystems[i].gameObject);
        }
    }
}
