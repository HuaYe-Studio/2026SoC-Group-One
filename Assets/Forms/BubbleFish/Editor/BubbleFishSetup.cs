using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq; // 引入 Linq 以简化查询

public static class BubbleFishSetup
{
    [MenuItem("BubbleFish/SetupAnimation")]
    public static void Setup()
    {
        string animatorPath = "Assets/Forms/BubbleFish/Animations/Animator_BubbleFishForm.controller";
        string animDir = "Assets/Forms/BubbleFish/Animations/";

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(animatorPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(animatorPath);
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        string[] animFiles = Directory.GetFiles(animDir, "*.anim");

        foreach (string file in animFiles)
        {
            string assetPath = "Assets/" + file.Replace("\\", "/").Split(new string[] { "Assets/" }, System.StringSplitOptions.None)[1];
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

            if (clip != null)
            {
                // 使用 Any 检查是否存在同名状态，避免直接比较 ChildAnimatorState
                bool exists = sm.states.Any(s => s.state.name == clip.name);
                if (!exists)
                {
                    sm.AddState(clip.name);
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("泡泡鱼动画自动部署完成！");
    }
}