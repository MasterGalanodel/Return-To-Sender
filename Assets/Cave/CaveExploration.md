# Cave exploration

Open `Assets/Cave/Scenes/CaveExploration.unity` and press Play.

- Move with A/D or Left/Right Arrow. The existing Player/Move action also supports a controller stick.
- Follow the flat black floor to the blue opening and arrow on the right. No jumps or hazards are required.
- The room is 24 units wide, with a short horizontal camera scroll.
- Reaching the exit automatically loads `TD.unity`.
- The same persistent HUD survives the transition, retaining health, tower tokens, and update tokens. A fresh Play session starts at 50, 2, and 10.

The build scene list starts with Cave Exploration, then TD, followed by the existing sample scene. The repeatable `Tools > Cave > Setup Cave Scene` command creates or updates the cave through Unity's Editor APIs. It does not modify the TD scene or shared HUD prefab.

Automated Play verification: `AgentScripts/TDCaveChecks.cs`; results: `TDReports/CaveChecks.json`. Checks cover actual D/right-arrow input, the left boundary, floor alignment, camera scrolling, automatic scene loading, successful TD initialization, and shared HUD identity and values.

