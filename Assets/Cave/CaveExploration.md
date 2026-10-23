# Vertical Cave descent

Open `Assets/Cave/Scenes/CaveExploration.unity` and press Play.

- Descend a shaft containing **40 numbered, one-way rock platforms**. The layout alternates `Assets/Shared/Graphics/Art/platform1.png` and `platform2.png` and is reused every day.
- Move with **A/D** or **Left/Right Arrow**, jump with **Space**, and drop through a ledge with **Down + Space** (or **S + Space**). A controller's left stick and south face button also work.
- Gravity pulls the player down onto platform tops. Jumping up through their undersides is allowed. The camera follows vertically and looks ahead to the next ledges.
- Falling past the bottom returns the player to the last landed step without changing resources. The Cave clock continues.
- The teal **TD** doorway on step 40 starts Tower Defense.

## Supplies

There are **12 pickups** on selected platforms: **8 hearts worth +5 HP each** and **4 build icons worth +1 build token each**. They use `Assets/Shared/HUD/Art/health.png` and `build.png`.

Walk or fall into an icon to collect it. Each pickup can be collected **once per day**; reloading the same Cave day does not duplicate rewards. Supplies become available again on the next day. Health can exceed 80, making the existing victory target reachable. Health, build balances and placed towers persist across Cave and TD.

## Run rules

The Cave timer turns red after 60 seconds. Exploration can continue, but costs 1 HP at 65 seconds and every five seconds afterward. TD lasts 60 seconds, then returns to this same Cave at the entrance on the next day. The run lasts six days. Zero HP ends it immediately; finishing day 6 requires more than 80 HP to win. Results freeze gameplay and display the outcome.

The player uses `Assets/Shared/Graphics/Art/mc.png` in both scenes; TD enemies use `enemy1.png` and towers use `castle.png` from the same folder.

## Setup and verification

**Tools > Cave > Setup Cave Scene** creates or updates the owned environment, platforms, pickups, player physics and bottom exit through Unity's Editor APIs. Repeated setup does not duplicate platforms or pickups. It also enables Cave and TD first in the build scene list.

Native-physics traversal checks live in `AgentScripts/VerticalCaveChecks.cs` and write `Logs/VerticalCaveChecks.json`. They drive the existing input actions through a temporary controller to test all 40 landings, movement, jumping, drop-through, pickup rewards, the actual bottom exit, resource persistence, daily supplies, reload protection and fall recovery.

The shared gameplay loop can also be checked from Cave Play mode with **Tools > Shared > HUD > Verify Run Loop (Play Mode)**; its report is `Logs/RunLoopChecks.json`.
