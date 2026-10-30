using System;
using Shared.HUD;
using TMPro;
using UnityEditor;

public static class ConfigurePhaseTimers
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before updating the HUD prefab.");
        var root = PrefabUtility.LoadPrefabContents(Shared.HUD.Editor.HudSetup.PrefabPath);
        try
        {
            var timerTransform = root.transform.Find("SafeArea/TopCenterStats/DayAndTime/Time");
            if (!timerTransform) throw new InvalidOperationException("The HUD timer is missing.");
            var timer = timerTransform.GetComponent<TextMeshProUGUI>();
            if (!timer) throw new InvalidOperationException("The HUD timer text component is missing.");
            timer.text = $"CAVE 0/{GameSession.CaveSeconds}s";
            PrefabUtility.SaveAsPrefabAsset(root, Shared.HUD.Editor.HudSetup.PrefabPath);
            return new { caveSeconds = GameSession.CaveSeconds, towerDefenseSeconds = GameSession.TowerDefenseSeconds, label = timer.text };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
