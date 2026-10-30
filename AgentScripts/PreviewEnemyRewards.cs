using System;
using System.IO;
using TD;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class PreviewEnemyRewards
{
    private static TDBootstrap td;
    private static int initialTokens, stage;
    private static double deadline;

    public static object Main()
    {
        td = Object.FindFirstObjectByType<TDBootstrap>();
        if (!EditorApplication.isPlaying || !td || !td.Initialized) throw new InvalidOperationException("Start Play in TD first.");
        EditorApplication.isPaused = false;
        td.enabled = false;
        td.Spawner.enabled = false;
        td.World.Retreat();
        Shared.HUD.GameSession.Instance.BeginPhase(Shared.HUD.GameSession.PlayPhase.TowerDefense);
        initialTokens = Shared.HUD.GameSession.Instance.TowerTokens;
        var reward = td.World.SpawnBuildReward(td.Player.transform.position);
        reward.transform.position = td.Player.transform.position + Vector3.right * .38f;
        var other = td.World.SpawnBuildReward(td.Player.transform.position);
        other.transform.position = td.Player.transform.position + new Vector3(-.9f, -.5f, 0);
        // Drive the actual keyboard path on the next runtime Update.
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.C));
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 10;
        EditorApplication.update += Tick;
        return new { status = "Waiting for real C-key collection", tokens = initialTokens };
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
        {
            EditorApplication.update -= Tick;
            throw new InvalidOperationException("C-key preview timed out.");
        }
        if (stage == 0 && td.Claw.IsBusy)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            stage = 1;
        }
        else if (stage == 1 && !td.Claw.IsBusy)
        {
            int tokens = Shared.HUD.GameSession.Instance.TowerTokens;
            if (tokens != initialTokens + 1) throw new InvalidOperationException("Real C-key collection did not award exactly one token.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/EnemyRewardKeyboardCheck.json", "{\"passed\":true,\"actualCKey\":true,\"tokenDelta\":1}");
            EditorApplication.update -= Tick;
            EditorApplication.isPaused = true;
        }
    }

    public static object Pose()
    {
        td = Object.FindFirstObjectByType<TDBootstrap>();
        EditorApplication.isPaused = true;
        td.Claw.CancelCollection();
        td.World.Retreat();
        var reward = td.World.SpawnBuildReward(td.Player.transform.position);
        reward.transform.position = td.Player.transform.position + new Vector3(.38f, -.1f, 0);
        var other = td.World.SpawnBuildReward(td.Player.transform.position);
        other.transform.position = td.Player.transform.position + new Vector3(-.9f, -.5f, 0);
        td.Claw.TryCollect();
        td.Claw.StepCollection(.46f);
        var explosion = TDEnemyExplosion.Spawn(td.World, td.Player.transform.position + new Vector3(-.8f, .55f, 0));
        explosion.Step(.1f);
        return new { busy = td.Claw.IsBusy, rewards = td.World.Rewards.Count };
    }
}
