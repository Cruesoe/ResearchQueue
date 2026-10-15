# Research Queue colony tests

Date: October 15, 2026. Result: workable local test build.

## Environment and evidence

RimWorld 1.7.4979 rev109, Harmony 3.0, all official DLCs, and Research Queue. Tests used separate savedatafolder directories and disposable Dev quicktest colonies. The real ModsConfig.xml SHA-256 remained `24140A4EA576AB0C956CD97752897051ABEEE870C7E5CEFE0A9FBCD88EB8C092`.

- Original automatic-population/UI test: `Projects/Generated/ResearchQueueColony-20261015-110209/`.
- Original failed-reload save: `Projects/Generated/ResearchQueueColony-20261015-111423/Saves/New Arrivals2.rws`.
- Final core/Anomaly regression: `Projects/Generated/ResearchQueueColony-20261015-115535/Integration.txt`, 63 PASS checks and COMPLETE.
- Node Research plus Vanilla Gravship Expanded Chapter 1 and Vanilla Expanded Framework: `Projects/Generated/ResearchQueueColony-20261015-115802/Integration.txt`, 16 PASS checks and COMPLETE.

## Verified

- Research auto-selected Recurve bow and queued 30 pending projects before the screen was first opened.
- The actual screen uses the Semi-Random Research drawing patterns. The user confirmed manual drag reordering works. Automated drag gestures selected the destination without reordering; no native-drag replacement was made.
- Native filtering retained actual queue positions. Research next moved Carpet-making from position 30 to 1 without interrupting current work. Snooze removed it and exposed Restore in Snoozed.
- The paused-reload bug was fixed: exclusion state now rebuilds during FinalizeInit, without waiting for a tick. The original regression save and a fresh round trip both passed.
- Manual priorities, active research, explicit snoozes, and positive rate estimates survive loading while paused. Restore after loading appends to the end.
- Completion selects the player's next eligible priority, preserves remaining order, and appends newly available research sorted within the new batch.
- Normal research selection cannot bypass the queue.
- A blocked active project returns to the head of pending research and yields to eligible research. Clearing the blocker does not interrupt the replacement. Blocked entries have no completion estimate.
- Each cumulative estimate includes active remaining work and every preceding runnable entry. These calculations used seeded measured-rate history. Days and approximate-date displays were inspected in game and rendered correctly.
- Anomaly Basic selection, snoozing, and restoration use their own slot/state and preserve Standard research and priorities.
- With Node and VGE active, Research Queue owns the research window; Node's tab collapse and generated emergence definitions remain; projects route to separate queues without duplicates. VGE selects and snoozes its own Gravtech slot without interrupting Standard research.

## Logs and deployment

The final runs contained no error, ReflectionTypeLoadException, or UnityStubs warning attributable to Research Queue. Optional-mod loading of the disposable baseline emitted framework migration warnings about missing think nodes and newly introduced precepts. The temporary fixture metadata also emitted a missing dependency-download-link warning; the fixture is not shipped with Research Queue. These were not Research Queue runtime failures.

Both production Release builds passed with zero warnings/errors. Eighteen offline queue/rate checks and translation-key checks passed. The standard production deploy verified six files with zero missing, extra, or mismatched files. The temporary fixture is excluded from production deployment and removed from the game installation after testing.

## Remaining broader testing

- RimWorld 1.6 runtime, full Progression-pack and long campaign testing, and other optional-mod combinations.
- Dependent-confirmation popup interaction, all supported display/UI scales, and sustained research throughput with hundreds or thousands of projects.
- Actual elapsed ten-day research history, full Anomaly progression, and all Gravtech progression rather than slot-level smoke checks.

No established colony was modified. No release or publication was performed.
