using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private BaseForm[] allForms;
    [SerializeField] private int activeFormIndex;

    [Header("Form Switch Keys")]
    [SerializeField] private KeyCode[] formSwitchKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

    public Transform SpawnedObjectContainer { get; private set; }

    private Dictionary<FormType, int> formTypeToIndex = new Dictionary<FormType, int>();
    private HashSet<int> unlockedFormIndices = new HashSet<int>();

    public BaseForm ActiveForm => (allForms != null && activeFormIndex < allForms.Length)
        ? allForms[activeFormIndex]
        : null;

    private void Awake()
    {
        EnsureSpawnedObjectContainer();
        InitializeForms();
        ActivateDefaultForm();
    }

    private void EnsureSpawnedObjectContainer()
    {
        var existing = transform.Find("FormSpawnedObjects");
        if (existing != null)
        {
            SpawnedObjectContainer = existing;
            return;
        }

        var go = new GameObject("FormSpawnedObjects");
        go.transform.SetParent(transform);
        SpawnedObjectContainer = go.transform;
    }

    private void InitializeForms()
    {
        if (allForms == null) return;

        for (int i = 0; i < allForms.Length; i++)
        {
            if (allForms[i] == null) continue;

            allForms[i].Initialize(this);
            allForms[i].gameObject.SetActive(false);

            FormType formType = GetFormTypeForIndex(i);
            formTypeToIndex[formType] = i;
        }

        unlockedFormIndices.Add(0);
    }

    private FormType GetFormTypeForIndex(int index)
    {
        if (allForms[index] is SlimeForm) return FormType.Slime;
        if (allForms[index] is FrogForm) return FormType.Frog;
        return (FormType)index;
    }

    private void ActivateDefaultForm()
    {
        if (allForms == null || allForms.Length == 0) return;

        activeFormIndex = Mathf.Clamp(activeFormIndex, 0, allForms.Length - 1);

        if (allForms[activeFormIndex] != null)
        {
            allForms[activeFormIndex].gameObject.SetActive(true);
            allForms[activeFormIndex].OnFormActivated();
        }
    }

    private void OnEnable()
    {
        MockEventCenter.OnFormUnlocked += AddNewForm;
    }

    private void OnDisable()
    {
        MockEventCenter.OnFormUnlocked -= AddNewForm;
    }

    private void AddNewForm(FormType formType)
    {
        if (formTypeToIndex.TryGetValue(formType, out int index))
        {
            unlockedFormIndices.Add(index);
            Debug.Log($"3C: Unlocked form [{formType}] at index {index}");
        }
    }

    public bool IsFormUnlocked(FormType formType)
    {
        if (formTypeToIndex.TryGetValue(formType, out int index))
            return unlockedFormIndices.Contains(index);
        return false;
    }

    public void SwitchToFormByType(FormType formType)
    {
        if (formTypeToIndex.TryGetValue(formType, out int index))
            SwitchToForm(index);
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");

        if (ActiveForm != null)
            ActiveForm.ProcessInput(horizontal);

        HandleFormSwitch();
    }

    private void HandleFormSwitch()
    {
        for (int i = 0; i < formSwitchKeys.Length && i < allForms.Length; i++)
        {
            if (Input.GetKeyDown(formSwitchKeys[i]))
                SwitchToForm(i);
        }

        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scroll > 0.1f)
            CycleForm(1);
        else if (scroll < -0.1f)
            CycleForm(-1);
    }

    public void SwitchToForm(int index)
    {
        if (index < 0 || index >= allForms.Length) return;
        if (index == activeFormIndex) return;
        if (!unlockedFormIndices.Contains(index)) return;
        if (allForms[index] == null) return;

        if (ActiveForm != null)
        {
            ActiveForm.OnFormDeactivated();
            ActiveForm.gameObject.SetActive(false);
        }

        activeFormIndex = index;
        allForms[index].gameObject.SetActive(true);
        allForms[index].OnFormActivated();

        Debug.Log($"3C: Switched to form [{index}]");
    }

    private void CycleForm(int direction)
    {
        if (allForms == null || allForms.Length <= 1) return;

        int next = activeFormIndex;
        for (int attempt = 0; attempt < allForms.Length; attempt++)
        {
            next = (next + direction + allForms.Length) % allForms.Length;
            if (unlockedFormIndices.Contains(next) && allForms[next] != null)
            {
                SwitchToForm(next);
                return;
            }
        }
    }

}
