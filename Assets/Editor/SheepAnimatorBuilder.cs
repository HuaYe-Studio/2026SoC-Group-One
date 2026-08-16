using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SheepAnimatorBuilder
{
    private const string ControllerPath = "Assets/Forms/Sheep/Animations/Animator_Sheep.controller";

    [MenuItem("Tools/生成冲冲羊动画控制器")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Forms/Sheep/Animations"))
            AssetDatabase.CreateFolder("Assets/Forms/Sheep", "Animations");

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        EnsureParameter(controller, "SHEEP_AnimState", AnimatorControllerParameterType.Int);
        EnsureState(sm, "Idle");
        EnsureState(sm, "Walk");
        EnsureState(sm, "Charge");
        EnsureState(sm, "Flee");
        EnsureState(sm, "Prey");

        AssignToPrefab("Assets/Forms/Sheep/Prefabs/Sheep.prefab", controller);

        AssetDatabase.SaveAssets();
        Debug.Log("冲冲羊动画控制器已生成并挂载到 Sheep.prefab");
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
            if (p.name == name) return;
        controller.AddParameter(name, type);
    }

    private static void EnsureState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState s in sm.states)
            if (s.state.name == name) return;
        sm.AddState(name);
    }

    private static void AssignToPrefab(string prefabPath, AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) return;
        Animator anim = root.GetComponent<Animator>();
        if (anim == null) anim = root.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }
}