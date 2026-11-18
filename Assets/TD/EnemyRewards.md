# Enemy rewards and claw collection

Killed enemies explode and independently have a **20%** chance to drop **one build token**.
The hammer artwork is `Assets/Shared/Graphics/Art/build_icon.png`. Rewards scatter within **2 world
units** of the death position, staying inside the playable map. Escapes and scene cleanup
do not create rewards or explosions.

Walk near rewards and press **Space**. Every reward inside the day's claw radius (0.6 world
units on day 1, growing each day) glows more brightly. One press sends a claw
(`Assets/Shared/Claw/Art/claw.png`) to each of them; each claw descends, grips its reward and
brings it to the player, and the shared HUD gains **+1 Build Token** as each one lands.
Pressing again during retrieval cannot duplicate them. Walking over a reward alone does not
collect it. With no reward in reach, Space builds or upgrades the tower on the player's
fence cell instead.

Rewards stay until collected or the current TD phase ends. A phase transition cancels an
unfinished claw retrieval without crediting it. Collected tokens persist through the existing
shared session and can be spent on towers with **Space**.

Balance values live in `Assets/TD/Data/PrototypeSettings.asset`: `buildRewardDropChancePerDay`
(one chance per day, element 0 = day 1; days past the end reuse the last entry), `buildRewardDropRadius`, and `buildRewardCollectRadiusPerDay` (claw radius per day, same indexing; one press
of Space grabs every token inside it, one claw per token). Sprite references live in
`Assets/Shared/Graphics/Data/PrototypeShapes.asset`. **Tools > Shared > Graphics > Assign
Gameplay Sprites** rewires the supplied artwork when needed.

Developer verification: in an initialized TD Play session, run `AgentScripts/VerifyEnemyRewards.cs`
with Unity Pipeline's `script run`. It writes `Logs/EnemyRewardChecks.json` and exercises lethal
hits, drop rates and bounds, close-range collection, duplicate prevention, cancellation,
escapes, cleanup, and the phase deadline. Use a disposable Play session: the checks reset the TD clock.
