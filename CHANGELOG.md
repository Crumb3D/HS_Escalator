# HS Escalator changelog

**1.0.12** — A running belt plays a mechanical drive rumble from underneath. Loudest on the steps, fades as you walk away, silent when the belt is stopped.

**1.0.11** — Less work every frame: grate re-check, captured-step check, trapped-entity check, and the setup HUD text are throttled. Setup-tool reflection is cached.

**1.0.10** — Running escalators and panels no longer steal the setup tool's selected escalator. Register Panel stays on the one you last set or used.

**1.0.9** — Setup tool E prompt and Use Escalator Here work on a running escalator. Aim uses the deck cells, not the world blocks that were taken.

**1.0.8** — Setup-tool info overlay (steps, panels, power, speed, state), same idea as HS_Doors / HS_Lift.

**1.0.7** — Escalator Grate paint on the paintbrush (Metal group), same atlas method as HS_Lift.

**1.0.6** — Blank escalators are never saved or synced. Old blank entries are dropped on load.

**1.0.5** — Register Panel refuses empty escalators and can move a panel from one escalator to another. New Escalator reuses empty slots.

**1.0.4** — The list is only saved after it was loaded for this world. Keeps `.bak` and `.bad` copies so a bad shutdown cannot wipe the real list.

**1.0.3** — Hide side wood only from the block above the bottom step, not beside it at floor level.

**1.0.2** — Side supports below the bottom step are left alone, so the floor under the wood stays.

**1.0.1** — Works on 7DTD 3.3 as well as 3.2. Speed 2 and 3 on the panel. Glass or metal sides, rubber handrail that runs with the belt. Comb plates at both ends. Two panels. Host deck-size cap.

**1.0.0** — Player-built escalators and moving walkways. You place the half-block steps; the setup tool marks the deck and a panel.
