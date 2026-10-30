# Shared HUD

The top-center overlay contains Health (50), Tower Tokens (2), and Update Tokens (10).
It scales with the window, respects the screen safe area, and does not intercept gameplay input.

## Reuse across scenes

`Resources/SharedHud.prefab` starts automatically when Play begins, including when starting from another scene.
Its `GameSession` component keeps the HUD root and counters alive with `DontDestroyOnLoad`.
Scene changes preserve the values. Duplicate instances disable and remove themselves without replacing the existing session.
Nothing needs to be added manually to each scene. This is session persistence, not a disk save.

Run **Tools > Shared > HUD > Setup Shared HUD** to create or update the owned prefab.
The defaults are editable on its **GameSession** component. New Play sessions start from those defaults.
The prefab uses the bundled static LiberationSans SDF font from TMP Essentials.

## Update counters from gameplay

```csharp
using TD;

var session = GameSession.Instance;
session.Health = 47;
session.TowerTokens = 1;
session.UpdateTokens = 9;

// Change all three with one notification.
session.SetValues(50, 2, 10);

// Start a fresh game while retaining the shared HUD.
session.ResetForNewGame();
```

Values are nonnegative integers. The UI refreshes through `Changed` events rather than polling the counters.
Tower costs, health damage, and token awards are not assigned by this HUD feature; gameplay code can use the API above.

## Verified

- Setup run twice without duplicating the prefab hierarchy.
- Default values and event-driven updates displayed correctly.
- Original runtime scene unloaded completely and then reloaded: the same HUD and values survived.
- Explicit duplicate instance removed without replacing the session.
- Negative values clamped to zero, then defaults restored.
- Top-center anchoring and nonblocking Canvas confirmed.
- Screenshot: `Assets/Shared/HUD/Verification/SharedHud.png`.
- Automated result: `TDReports/HudChecks.json`.

The initial font-baking attempt could not read Unity's built-in runtime font. The final prefab uses the bundled TMP font.
The first scene-transition test needed to cache the scene path before unloading its Scene handle; the corrected test passed.


