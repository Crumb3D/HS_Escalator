# HS Escalator

You build the escalator. The mod reads the half-blocks you placed and runs them as a moving walkway or a real escalator (level treads, stair rise, fold under, return underneath).

Side walls, skirts, a ceiling, lights, and a still handrail stay where you put them. Do not include them in the two corners.

## Blocks that move

Any building material **shaped as a half-block**. Paint and mixed materials ride. A wide tread is several half-blocks side by side.

Not steps: full cubes, wedges, ramps, plates, sheets, doors, ladders, terrain.

## Default walkway

1. Straight row of half-blocks, all the same height, at least **3** long.
2. Leave the block **under** every step empty (return + fold).
3. Leave **two** blocks of air above every tread.
4. Put a normal floor just **outside** each end, flush with the end steps.
5. Cover the sides however you want, outside the step deck.
6. Place an **Escalator Drive** beside an end. Wire it.
7. Setup tool: **Set End 1** and **Set End 2** on opposite corners of the **step deck only**, then **Register Drive**.

## Default escalator

Same hollow and covers. Steps:

- At least **two** flat half-blocks
- Then each next half-block one half-step up (0.5 m rise per 1 m of run)
- Then at least **two** flat half-blocks at the top

Shortest rise: `length >= 4 + rise` with rise in half-blocks. A **2-block** rise needs **8** steps: `0, 0, 0.5, 1.0, 1.5, 2.0, 2.0, 2.0`.

Straight along X or Z only.

## Setup tool (hold E)

- **New Escalator** — start a new one (does not change others)
- **Use Escalator Here** — edit the one you are aiming at
- **Set End 1 / Set End 2** — opposite corners of the steps
- **Register Drive** — the Escalator Drive beside an end
- **Jog belt** — run it for a few seconds so you can see the loop
- **Reverse** — flip direction
- **Forget / put back** — world blocks return

If the deck is wrong, the tool names the cell. It does not guess a different slope.

## Power

Wire the registered drive. Powered = run. Activate the drive to reverse.

A solid in the return or headroom, or a rider on a folding step, stops the belt and says why.

Admin: `giveself hsescalatorTool` / `giveself hsescalatorDrive` and `hsescalator`.
