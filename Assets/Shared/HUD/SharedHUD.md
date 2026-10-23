# Shared HUD and six-day run

Open `Assets/Cave/Scenes/CaveExploration.unity` and press Play. The same Cave scene and TD scene repeat for six days:

`Day 1 Cave -> TD -> Day 2 Cave -> TD -> ... -> Day 6 Cave -> TD -> result`

The top-center HUD shows day and scene elapsed time on one line, followed by Health and Build Tokens. The supplied `Art/health.png` and `Art/build.png` sprite sub-assets are referenced by the HUD prefab. New runs start at **50 HP** and **2 build tokens**. Update Tokens are no longer displayed.

## Rules

- Health and token balances persist across both scenes. They are session state, not a disk save.
- Cave has no time limit. Its timer turns red after 60 seconds and keeps counting. Lose 1 HP at 65 seconds, then every additional five seconds until exiting.
- Reaching the Cave exit starts that day's TD phase with a fresh timer.
- Each successfully placed tower costs 1 build token. Occupied/invalid cells cost nothing. At zero tokens the number is red; a valid construction attempt flashes it and places no tower.
- Each enemy reaching the TD exit costs 1 shared HP. Killed or retreating enemies cost no HP.
- TD ends at 60 seconds. Remaining enemies/projectiles disappear without escape damage. The next day reuses the same Cave scene, starting at its entrance. Tower cells persist, and towers return to their original positions on the next TD visit without another charge.
- Zero HP immediately freezes gameplay and shows **GAME OVER**.
- Finishing day 6 with **more than 80 HP** freezes gameplay and shows **VICTORY**. At 80 HP or less it shows **GAME OVER**. There is no restart button; stop and restart Play mode for a fresh run.
- The 40-step Cave supplies eight health pickups (+5 HP each) and four build pickups (+1 each) per day. Collect each once per day; reloading the same day cannot duplicate rewards. Health can exceed 80.

## Setup and gameplay API

`Resources/SharedHud.prefab` starts automatically, including when Play begins directly in TD. Its `GameSession` and HUD remain alive across scene loads with `DontDestroyOnLoad`. Duplicate session roots remove themselves. Defaults are editable on the prefab's `GameSession` component.

Run **Tools > Shared > HUD > Setup Shared HUD** to update the owned prefab in place. Repeating setup does not duplicate the UI. It uses the bundled LiberationSans SDF font and the supplied sprite imports unchanged.

```csharp
using Shared.HUD;

var session = GameSession.Instance;
session.Damage(1);
session.Health += 1;       // HP is not capped at 50 or 80.
session.TowerTokens += 1;
session.UpdateTokens += 1;
```

Balances cannot become negative. Session values stop changing after the result. `Changed` refreshes the counters, day and clock; `BuildDenied` triggers insufficient-token feedback. Cave and TD controllers advance their own phase clocks before movement and combat, so a TD enemy cannot inflict escape damage after the 60-second deadline.

`TryCollectCavePickup` records rewards by day and pickup ID. `ResetForNewGame()` restores defaults, day 1 and time scale, and clears stored tower cells and pickup history. Restart/reload the gameplay scene as well when using it outside a fresh Play session.

## Verification

Start Play from Cave, then run **Tools > Shared > HUD > Verify Run Loop (Play Mode)**. This deliberately changes the running session and ends on a frozen game-over screen. The report is written to `Logs/RunLoopChecks.json`.

The 86 checks cover exact overtime thresholds, red clock text, deadline rounding, six-day advancement, strict 80/81 HP outcomes, icons, valid/invalid placement costs, zero-token feedback, enemy escape/death damage, real scene transitions, restored tower occupancy, timer resets, and frozen gameplay/results. Unity compilation and the runtime error console were also checked.
