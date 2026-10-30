using System;
using System.Linq;
using Shared.Graphics;
using TD;
using UnityEditor;

public static class ConfigureEnemyRewards
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before configuring reward art.");
        var shapes = AssetDatabase.LoadAssetAtPath<SharedShapes>(TD.Editor.TDSetup.ShapesPath);
        var settings = AssetDatabase.LoadAssetAtPath<TDSettings>(TD.Editor.TDSetup.SettingsPath);
        if (!shapes || !settings) throw new InvalidOperationException("TD prototype assets are missing.");
        Undo.RecordObject(shapes, "Assign enemy reward art");
        shapes.buildReward = AssetDatabase.LoadAllAssetsAtPath("Assets/Shared/HUD/Art/build.png").OfType<UnityEngine.Sprite>().First();
        shapes.claw = AssetDatabase.LoadAllAssetsAtPath("Assets/Shared/Claw/Art/claw.png").OfType<UnityEngine.Sprite>().First();
        EditorUtility.SetDirty(shapes);
        AssetDatabase.SaveAssetIfDirty(shapes);
        settings.Validate();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        return new { reward = shapes.buildReward.name, claw = shapes.claw.name,
            chance = settings.buildRewardDropChance, dropRadius = settings.buildRewardDropRadius,
            collectRadius = settings.buildRewardCollectRadius };
    }
}
