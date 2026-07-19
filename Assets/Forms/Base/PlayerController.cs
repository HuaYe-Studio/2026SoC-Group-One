using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private BaseForm[] allForms;
    [SerializeField] private int activeFormIndex;

    public Transform SpawnedObjectContainer { get; private set; }

    private Dictionary<FormType, int> formTypeToIndex = new Dictionary<FormType, int>();
    private HashSet<FormType> unlockedForms = new HashSet<FormType>();

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

            FormType formType = ResolveFormType(allForms[i], i);
            formTypeToIndex[formType] = i;
        }

        unlockedForms.Add(FormType.Slime);
    }

    private FormType ResolveFormType(BaseForm form, int index)
    {
        FormType declared = form.FormType;
        // 如果 FormType 已在 Inspector 中显式配置过（非默认值或类型匹配），直接使用
        if (!formTypeToIndex.ContainsKey(declared))
            return declared;
        // 回退：Inspector 未配置时通过类型推断
        if (form is SlimeForm) return FormType.Slime;
        if (form is FrogForm) return FormType.Frog;
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
        if (formTypeToIndex.ContainsKey(formType))
        {
            unlockedForms.Add(formType);
            Debug.Log($"3C: Unlocked form [{formType}]");
        }
    }

    public bool IsFormUnlocked(FormType formType)
    {
        return unlockedForms.Contains(formType);
    }

    public void SwitchToFormByType(FormType formType)
    {
        if (formTypeToIndex.TryGetValue(formType, out int index))
            SwitchToForm(index);
    }

    private void Update()
    {
        float horizontal = PlayerInputReader.Instance.MoveValue.x;

        if (ActiveForm != null)
            ActiveForm.ProcessInput(horizontal);

        HandleFormSwitch();
    }

    private void HandleFormSwitch()
    {
        float scroll = PlayerInputReader.Instance.ScrollValue;
        if (scroll > 0.1f)
            CycleForm(1);
        else if (scroll < -0.1f)
            CycleForm(-1);
    }

    public void SwitchToForm(int index)
    {
        if (index < 0 || index >= allForms.Length) return;
        if (index == activeFormIndex) return;
        if (allForms[index] == null) return;
        if (!unlockedForms.Contains(allForms[index].FormType)) return;

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

        var ordered = new List<int>();
        for (int i = 0; i < allForms.Length; i++)
        {
            if (allForms[i] != null && unlockedForms.Contains(allForms[i].FormType))
                ordered.Add(i);
        }

        if (ordered.Count <= 1) return;

        int curPos = ordered.IndexOf(activeFormIndex);
        if (curPos < 0) return;

        int nextPos = (curPos + direction + ordered.Count) % ordered.Count;
        SwitchToForm(ordered[nextPos]);
    }

}
