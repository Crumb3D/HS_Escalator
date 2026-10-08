# HS Escalator

You build the escalator out of half-blocks. The mod takes those blocks and runs them as a moving walkway or a real escalator (level treads, stair rise, fold under, return underneath).

## How to build one

1. Place the steps as **half-blocks of concrete or steel**. Do this before you set the two corners. Once the deck is captured, those blocks are taken and you cannot upgrade them.
2. Leave the block under every step empty, and two blocks of air above every tread.
3. Put a normal floor just outside each end, flush with the end steps.
4. Build the side supports out of **wood** (sheet, plate, or cube) in the cell beside the steps. The mod hides wood from the block above the bottom step upward. Floor beside the bottom step at the same level stays — no trenches. Leave the hidden wood. Extra wood that is not in that cell can be destroyed afterwards. Do not put the side wood on the two corner blocks you will mark, and do not put the panel in place of it.
5. Place an **Escalator Panel** beside an end. A second panel at the other end is optional. Wire at least one.
6. Setup tool: **Set End 1** and **Set End 2** on opposite corners of the step deck only, then **Register Panel**.

The mod hides the wood and draws the escalator side there. The hidden supports do not stop you, and zombies cannot break them. Open air beside the steps gets no side. Forget puts the wood back.

## Blocks that move

Concrete or steel **shaped as a half-block**. When the belt runs, every tread is painted with the escalator grate. Width is whatever you build — 1 file or 20 — as long as each column is the same height across. Mark opposite corners of the **whole** deck.

Not steps: full cubes, wedges, ramps, plates, sheets, doors, ladders, terrain. Pick concrete or steel before you set the corners. You cannot upgrade a step after that.

## Default walkway

1. Straight row of half-blocks, all the same height, at least **3** long.
2. Leave the block **under** every step empty (return + fold).
3. Leave **two** blocks of air above every tread.
4. Put a normal floor just **outside** each end, flush with the end steps.
5. Wood supports in the cell beside the steps, not on the two corners. The mod hides from the block above the bottom step up. Floor beside the bottom step stays.
6. Place an **Escalator Panel** beside an end. You can put a second panel at the other end. Wire at least one.
7. Setup tool: **Set End 1** and **Set End 2** on opposite corners of the **step deck only**, then **Register Panel** on each panel. The steps must already be concrete or steel.

## Default escalator

Same hollow and covers. Steps:

- At least **two** flat half-blocks at each end (so the landings read)
- Then each next half-block one half-step up (0.5 m rise per 1 m of run)
- Once the belt runs, those extra end flats become stairs. Only the first and last column stay a comb.

Shortest rise: `length >= 4 + rise` with rise in half-blocks. A **2-block** rise needs **8** steps: `0, 0, 0.5, 1.0, 1.5, 2.0, 2.0, 2.0`.

Straight along X or Z only.

## Setup tool (hold E)

- **New Escalator** — start a new one (does not change others)
- **Use Escalator Here** — edit the one you are aiming at
- **Set End 1 / Set End 2** — opposite corners of the steps
- **Register Panel** — the Escalator Panel beside an end
- **Jog belt** — run it for a few seconds so you can see the loop
- **Reverse** — flip direction
- **Forget / put back** — world blocks return

If the deck is wrong, the tool names the cell. It does not guess a different slope.

## Handrail

The wood beside the steps is only there to hold the escalator up. After you set both ends, the mod hides it and draws the side on the step edge. You bump that side, and you can walk under the steps.

- The side, from under the returning steps up to the handrail. Glass by default, or metal
- A rubber rail on the step side
- A glass sheet closing the bottom, so the return shows through

On each escalator in the world save's `HSEscalator.json`, `"Side": "glass"` or `"Side": "metal"`. Missing means glass. Restart the server after editing it.

The rubber runs with the belt. At each end it curves around the side and comes back just above the glass floor.

The End / Slope / Flat pieces are optional. A support block is what turns the side on. Open air beside the steps gets no rail. Forget puts the support blocks back. It updates within a second of placing or removing one.

## Power

Wire the registered panel. Hold E on it for Start/Stop, Forward/Reverse, Run Always or only when something is on it, and Speed 1, 2, or 3. The menu shows the two speeds you are not already on. Speed 1 is the normal pace, 2 is double, 3 is triple.

A solid in the return or headroom stops the belt and says why.

Admin: `giveself hsescalatorTool` / `giveself hsescalatorDrive` and `hsescalator`.

## Deck size cap (host)

No cap by default (`MaxDeckCells` 0). The host — dedicated server, or the single-player client — can set one so a giant deck cannot stall the box. The server value is sent to every client and **overrides** their local file.

- File: `HSEscalatorSettings.json` in the mod folder, or `%AppData%/7DaysToDie/HSEscalator/HSEscalatorSettings.json` (wins), or the world save folder (wins last).
- Console: `hsescalator maxdeck 256`   (`0` turns the cap off)
