# Low-gravity Cave loop

Open `Assets/Cave/Scenes/CaveExploration.unity` and press Play.

- Each dive **starts from the centre of the TD door** above platform 01. A strip along the **left wall has no platforms** (`freeFallLaneWidth`), so the player can drop straight to the `cave_ground.png` floor, then climb the numbered one-way platforms back to the door. The door only counts once the player has left it.
- From **day 2** the Faithful One (`faithful_one.png`) stands on the left end of platform 01, pointing at the drop, with the hint "The depths of Ashina are over there" below her (*Faithful One* in `CaveSettings`).
- The player animates from **row 1 of `character_sprite_sheet.png`**: frames 1-4 when still, 5-8 when moving.
- **Hold Space** to boost up and **hold S + Space** to boost down (S + Space still drops through a ledge). `fire.png` burns behind the backpack: upside down when boosting up, upright when boosting down. Strength and speed caps are under *Jet boost* in `CaveSettings`.
- After the 20 s cave time runs out, the screen flashes a **red border and tint for 0.3 s every 1 s** (*Overtime warning* in `CaveSettings`).
- **Every day's cave is randomly generated** at runtime by `CaveBuilder` from **`Assets/Cave/Data/CaveSettings.asset`**: per day (6 entries) you set the number of platform layers, health pickups, build pickups and the amount each gives. Shared layout rules (layer spacing, platform width, side step, second-platform chance, rest ledges) live in the same asset. `seed = 0` rolls new caves every run; any other value repeats the same six caves. Re-entering the Cave on the same day shows the same layout.
- Move with **A/D** or **Left/Right Arrow**, jump repeatedly with **Space**, and drop through a ledge with **Down + Space** (or **S + Space**). A controller's left stick and south face button also work. Every jump press restores upward speed, with no limit on airborne jumps.
- The Cave player's gravity is **one tenth of its previous strength** (Rigidbody2D gravity scale **0.28**, previously 2.8). Jumping up through platform undersides is allowed. The camera follows vertically and looks ahead to the next ledges.
- Falling past the bottom returns the player to the last landed step without changing resources. The Cave clock continues.
- The teal **TD** doorway floats **above platform 01** at the top (height set by `exitHeightAbovePlatform01`). Collect supplies on the way up and jump into it to start Tower Defense.

## Supplies

Pickups are scattered on random platforms (never 01 or the floor) according to the day's entry in `CaveSettings`. They use `Assets/Shared/HUD/Art/health.png` and `build.png`.

Walk or fall into an icon to collect it. Each pickup can be collected **once per day**; reloading the same Cave day does not duplicate rewards. Supplies become available again on the next day. Health can exceed 80, making the existing victory target reachable. Health, build balances and placed towers persist across Cave and TD.

## Run rules

The Cave timer turns red after 20 seconds. Exploration can continue, but costs 1 HP at 25 seconds and every five seconds afterward. TD has its own 60-second duration, then returns to this same Cave at the entrance on the next day. The run lasts six days. Zero HP ends it immediately; finishing day 6 requires more than 80 HP to win. Results freeze gameplay and display the outcome.

The player uses `Assets/Shared/Graphics/Art/mc.png` in both scenes; TD enemies use `enemy1.png` and towers use `castle.png` from the same folder.

## Setup and verification

**Tools > Cave > Setup Cave Scene** creates `CaveSettings.asset` if missing, wires `CaveBuilder`'s art, the player physics, controls text and TD exit. Shaft, platforms and pickups are no longer stored in the scene; they are generated on Play. It also enables Cave and TD first in the build scene list.

Native-physics traversal checks live in `AgentScripts/VerticalCaveChecks.cs` (written for the old fixed 40-platform layout; needs updating for generated caves) and write `Logs/VerticalCaveChecks.json`. They drive the existing input actions through a temporary controller to test all 40 landings, low gravity, repeated airborne jumps, drop-through, +1 pickup rewards, the return climb and starting-platform exit, resource persistence, daily supplies, reload protection and fall recovery.

The shared gameplay loop can also be checked from Cave Play mode with **Tools > Shared > HUD > Verify Run Loop (Play Mode)**; its report is `Logs/RunLoopChecks.json`.
