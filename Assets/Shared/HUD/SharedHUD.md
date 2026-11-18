# Shared HUD and six-day run

Open `Assets/Cave/Scenes/CaveExploration.unity` and press Play. The same Cave scene and TD scene repeat for six days:

`Day 1 Cave -> TD -> Day 2 Cave -> TD -> ... -> Day 6 Cave -> TD -> result`

The top-center HUD shows day and scene elapsed time on one line, followed by Health and Build Tokens. The Health icon swaps by HP: `Shared/Graphics/Art/heath_critical.png` below 60, `health_healing.png` from 60 to 80, and `health_good.png` above 80. Build Tokens use `Shared/Graphics/Art/build_icon.png`. New runs start at **50 HP** and **2 build tokens**. Update Tokens are no longer displayed.

## Rules

- Health and token balances persist across both scenes. They are session state, not a disk save.
- Cave's normal exploration time is 20 seconds. Its timer turns red after 20 seconds and keeps counting. Lose 1 HP at 25 seconds, then every additional five seconds until exiting.
- Reaching the Cave exit starts that day's TD phase with a fresh timer.
- Each successfully placed tower costs 1 build token. Occupied/invalid cells cost nothing. At zero tokens the number is red; a valid construction attempt flashes it and places no tower.
- Each enemy reaching the TD exit costs 1 shared HP. Killed or retreating enemies cost no HP.
- Killed TD enemies explode and have a 20% chance to drop a +1 build token within 2 world units. Press **Space** to claw every token within the day's radius (0.6 units on day 1, growing each day); with none in reach, Space builds or upgrades a tower; tokens are credited when retrieval finishes. See `Assets/TD/EnemyRewards.md`.
- TD ends at 60 seconds. Remaining enemies/projectiles disappear without escape damage. The next day reuses the same Cave scene, starting at its entrance. Tower cells persist, and towers return to their original positions on the next TD visit without another charge.
- HP can go below zero (shown as a negative number); the run keeps going and only the day-6 check decides the outcome.
- Finishing day 6 with **more than 80 HP** freezes gameplay and shows the full-screen **VICTORY** end screen (`victory_screen.png`). At 80 HP or less it shows **DEFEAT** (`defeat_screen.png`). The end screen (`Scripts/EndScreen.cs`, built in code) has **Restart** (resets the session and loads build scene 0, the Cave) and **Exit** (quits the player / stops Play mode in the editor), plus a **Difficulty** slider from ×0.25 to ×4 (log scale, ×1 centered, default ×1). Difficulty is stored in `GameSession.Difficulty` (resets to ×1 each app launch, kept across Restart) and TD divides the enemy spawn interval by it (×4 = four times as many enemies), starting with the next run. If the art references are missing, the old small result panel is shown instead.
- On app launch the Cave shows a **start screen** (`Scripts/OpeningSplash.cs`, title art `opening.png`) with the intro text, the same **Difficulty** slider, and **Play** / **Exit** buttons. Gameplay stays paused (timeScale 0) until Play is pressed; Restart from the end screen skips it. Both screens share their code-built UI pieces in `Scripts/MenuUI.cs`.
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

The phase durations are independent constants in `GameSession`: `CaveSeconds = 20` and `TowerDefenseSeconds = 60`. `PhaseDurationSeconds` supplies the current phase's HUD duration; Cave overtime penalties still repeat every five seconds.

## Verification

Start Play from Cave, then run **Tools > Shared > HUD > Verify Run Loop (Play Mode)**. This deliberately changes the running session and ends on a frozen game-over screen. The report is written to `Logs/RunLoopChecks.json`.

The 90 checks cover separate 20-second Cave and 60-second TD durations and HUD labels, exact overtime thresholds, red clock text, deadline rounding, six-day advancement, strict 80/81 HP outcomes, icons, valid/invalid placement costs, zero-token feedback, enemy escape/death damage, real scene transitions, restored tower occupancy, timer resets, and frozen gameplay/results. Unity compilation and the runtime error console were also checked.
