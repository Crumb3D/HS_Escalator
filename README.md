# HS Escalator

You build the escalator. The mod reads the half-blocks you placed and runs them as a moving walkway or a real escalator (level treads, stair rise, fold under, return underneath).

Side pieces, walls, and a ceiling stay where you put them. Do not include them in the two corners. A rubber handrail is added on each side and moves with the belt.

## Blocks that move

Any building material **shaped as a half-block**. When the belt runs, every tread is painted with the escalator grate. Width is whatever you build — 1 file or 20 — as long as each column is the same height across. Mark opposite corners of the **whole** deck.

Not steps: full cubes, wedges, ramps, plates, sheets, doors, ladders, terrain.

## Default walkway

1. Straight row of half-blocks, all the same height, at least **3** long.
2. Leave the block **under** every step empty (return + fold).
3. Leave **two** blocks of air above every tread.
4. Put a normal floor just **outside** each end, flush with the end steps.
5. Cover the sides however you want, outside the step deck.
6. Place an **Escalator Panel** beside an end. You can put a second panel at the other end. Wire at least one.
7. Setup tool: **Set End 1** and **Set End 2** on opposite corners of the **step deck only**, then **Register Panel** on each panel.

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

Place these **beside** the steps, outside the two corners, at the same block height as that step. Rotate while placing: the glass faces the steps. On a slope piece the high end points uphill. On an end piece the curve points off the end of the stairs.

Left and Right are mirrors. The same end piece works at the top or the bottom once you rotate it — pick the mirror that keeps the glass toward the steps.

- **End** — the curved newel. First and last column, or the cell just past that end beside the landing.
- **Slope** — one block of the rising run. One per rising step.
- **Flat** — walkways, and the level steps at each end.
- **Upper** — use this when the step beside the piece is the **top half** of the block. The plain piece is for the bottom half. The rubber still follows the steps if you mix them up; the glass just will not meet it.

No pieces, but a full-block wall in that side cell: the rail mounts on the wall. Half-blocks, plates, and open air get nothing. The rail appears within a second of placing or removing a piece, and it runs the same way as the belt.

## Power

Wire the registered panel. Hold E on it for Start/Stop, Forward/Reverse, and Run Always or only when something is on it.

A solid in the return or headroom stops the belt and says why.

Admin: `giveself hsescalatorTool` / `giveself hsescalatorDrive` and `hsescalator`.

## Deck size cap (host)

No cap by default (`MaxDeckCells` 0). The host — dedicated server, or the single-player client — can set one so a giant deck cannot stall the box. The server value is sent to every client and **overrides** their local file.

- File: `HSEscalatorSettings.json` in the mod folder, or `%AppData%/7DaysToDie/HSEscalator/HSEscalatorSettings.json` (wins), or the world save folder (wins last).
- Console: `hsescalator maxdeck 256`   (`0` turns the cap off)
