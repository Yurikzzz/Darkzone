using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

[InitializeOnLoad]
public static class SetupPlayerAnimations
{
    private const string DoneKey = "Darkzone_PlayerAnimationsSetup_v2";

    static SetupPlayerAnimations()
    {
        EditorApplication.delayCall += OnEditorReady;
    }

    private static void OnEditorReady()
    {
        if (!SessionState.GetBool(DoneKey, false))
        {
            SessionState.SetBool(DoneKey, true);
            Execute();
        }
    }

    [MenuItem("Darkzone/Setup Player Animations")]
    public static void Execute()
    {
        string animDir = "Assets/Animations/Player";
        if (!Directory.Exists(animDir))
        {
            Directory.CreateDirectory(animDir);
            AssetDatabase.Refresh();
        }

        AnimationClip GetOrCreateClip(string clipPath, string clipName, bool loop)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, clipPath);
                Debug.Log($"[Darkzone] Created AnimationClip: {clipPath}");
            }
            else
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
            return clip;
        }

        string idlePath = animDir + "/Player_Idle.anim";
        string walkPath = animDir + "/Player_Walk.anim";
        string runPath = animDir + "/Player_Run.anim";
        string deadPath = animDir + "/Player_Dead.anim";

        AnimationClip idleClip = GetOrCreateClip(idlePath, "Player_Idle", true);
        AnimationClip walkClip = GetOrCreateClip(walkPath, "Player_Walk", true);
        AnimationClip runClip = GetOrCreateClip(runPath, "Player_Run", true);
        AnimationClip deadClip = GetOrCreateClip(deadPath, "Player_Dead", false);

        string controllerPath = animDir + "/PlayerController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            Debug.Log($"[Darkzone] Created AnimatorController at: {controllerPath}");
        }

        void EnsureParam(string paramName, AnimatorControllerParameterType type, float defaultFloat = 0f)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == paramName) return;
            }
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = paramName,
                type = type,
                defaultFloat = defaultFloat
            });
        }

        EnsureParam("IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParam("IsRunning", AnimatorControllerParameterType.Bool);
        EnsureParam("IsDead", AnimatorControllerParameterType.Bool);
        EnsureParam("MoveDirection", AnimatorControllerParameterType.Float, 1f);

        var rootStateMachine = controller.layers[0].stateMachine;

        // Clear existing states/transitions to rebuild cleanly
        var existingStates = rootStateMachine.states;
        foreach (var childState in existingStates)
        {
            rootStateMachine.RemoveState(childState.state);
        }

        var anyTransitions = rootStateMachine.anyStateTransitions;
        foreach (var t in anyTransitions)
        {
            rootStateMachine.RemoveAnyStateTransition(t);
        }

        // Create States
        var idleState = rootStateMachine.AddState("Idle", new Vector3(250, 0, 0));
        idleState.motion = idleClip;
        idleState.speed = 1f;

        var walkState = rootStateMachine.AddState("Walk", new Vector3(500, -60, 0));
        walkState.motion = walkClip;
        walkState.speed = 0.75f; // 3/4 of the run animation speed
        walkState.speedParameterActive = true;
        walkState.speedParameter = "MoveDirection";

        var runState = rootStateMachine.AddState("Run", new Vector3(500, 60, 0));
        runState.motion = runClip;
        runState.speed = 1f;
        runState.speedParameterActive = true;
        runState.speedParameter = "MoveDirection";

        var deadState = rootStateMachine.AddState("Dead", new Vector3(250, 150, 0));
        deadState.motion = deadClip;
        deadState.speed = 1f;

        rootStateMachine.defaultState = idleState;

        // Transitions:
        // Idle -> Walk
        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        idleToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0f;

        // Idle -> Run
        var idleToRun = idleState.AddTransition(runState);
        idleToRun.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        idleToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0f;

        // Walk -> Idle
        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0f;

        // Walk -> Run
        var walkToRun = walkState.AddTransition(runState);
        walkToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
        walkToRun.hasExitTime = false;
        walkToRun.duration = 0f;

        // Run -> Idle
        var runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0f;

        // Run -> Walk
        var runToWalk = runState.AddTransition(walkState);
        runToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
        runToWalk.hasExitTime = false;
        runToWalk.duration = 0f;

        // AnyState -> Dead
        var anyToDead = rootStateMachine.AddAnyStateTransition(deadState);
        anyToDead.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
        anyToDead.hasExitTime = false;
        anyToDead.duration = 0f;
        anyToDead.canTransitionToSelf = false;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // Setup Player GameObject in active scene
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(player, "Setup Player Animator");

            Animator animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                animator = player.AddComponent<Animator>();
                Debug.Log("[Darkzone] Added Animator to Player");
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            PlayerAnimation playerAnim = player.GetComponent<PlayerAnimation>();
            if (playerAnim == null)
            {
                playerAnim = player.AddComponent<PlayerAnimation>();
                Debug.Log("[Darkzone] Added PlayerAnimation to Player");
            }

            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(player.scene);
            Debug.Log("[Darkzone] Player Animator setup complete and scene saved!");
        }
        else
        {
            Debug.LogWarning("[Darkzone] Player GameObject not found in active scene!");
        }
    }
}

