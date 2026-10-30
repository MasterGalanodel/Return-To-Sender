# Enemy rewards and claw collection

Killed enemies explode and independently have a **20%** chance to drop **one build token**.
The hammer artwork is `Assets/Shared/HUD/Art/build.png`. Rewards scatter within **2 world
units** of the death position, staying inside the playable map. Escapes and scene cleanup
do not create rewards or explosions.

Walk beside a reward (within **0.6 world units**) and press **C**. The nearest reward glows
more brightly when in reach. The supplied `Assets/Shared/Claw/Art/claw.png` descends,
grips the reward, and brings it to the player. The shared HUD gains **+1 Build Token** when
the animation finishes. Each press retrieves one reward; pressing again during retrieval
cannot duplicate it. Walking over a reward alone does not collect it.

Rewards stay until collected or the current TD phase ends. A phase transition cancels an
unfinished claw retrieval without crediting it. Collected tokens persist through the existing
shared session and can be spent on towers with **B**.

Balance values live in `Assets/TD/Data/PrototypeSettings.asset`: `buildRewardDropChance`,
`buildRewardDropRadius`, and `buildRewardCollectRadius`. Sprite references live in
`Assets/Shared/Graphics/Data/PrototypeShapes.asset`. **Tools > Shared > Graphics > Assign
Gameplay Sprites** rewires the supplied artwork when needed.

Developer verification: in an initialized TD Play session, run `AgentScripts/VerifyEnemyRewards.cs`
with Unity Pipeline's `script run`. It writes `Logs/EnemyRewardChecks.json` and exercises lethal
hits, drop rates and bounds, close-range collection, duplicate prevention, cancellation,
escapes, cleanup, and the phase deadline. Use a disposable Play session: the checks reset the TD clock.
