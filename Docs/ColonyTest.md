# Research Queue colony tests

October 15, 2026. All six approved review findings are addressed and tested.

## Current runs

RimWorld 1.7.4979 rev109, Harmony 3.0, and official DLCs. Every run uses a fresh temporary savedatafolder and native quicktest colony generation; no existing save is required. The fixture creates its own research baseline. It runs only with the explicit researchQueueTests flag.

| Suite | Assertions | Result |
| --- | ---: | --- |
| Core and Anomaly | 30 | COMPLETE |
| Node Research alone | 14 | COMPLETE |
| Vanilla Gravship Expanded Chapter 1 and Vanilla Expanded Framework | 9 | COMPLETE |
| Node plus Gravtech and Framework | 19 | COMPLETE |

Reports and logs are retained in the control project under `Generated/ResearchQueueReviewTests/{Core,Node,Gravtech,Combined}`. The earlier save-dependent runs remain under `Generated/ResearchQueueColony-*` as historical evidence.

## What passed

- Automatic initial population/selection without opening research; selection ownership; snooze/restore; progress preservation; completion advancement and appending; retained blocked priorities; and independent Anomaly operations.
- Real game progress deltas and an idle hour recorded by native hourly sampling, followed by persistence of measured history, priorities, and snoozes across a save/reload. Calculations no longer rely solely on seeded history.
- Missing-definition drag mapping through the actual queue state, plus offline upward/downward/end-boundary regressions.
- Node foundation gating, specific blocker text, automatic emergence selection, and actual era advancement. Legacy entries transfer before Node's completion callback can consume them; deferred entries return when their era becomes eligible.
- Automatic Gravtech selection and completion advancement, tested alone and with Node. The fixture never directly calls ResearchQueueRuntime.Start to supply the tested Gravtech project. Standard research continues independently.
- Actual rendering of a 1,000-row view draws only viewport cards, and repeated GUI passes reuse the cached snapshot. Native dragging still registers all row rectangles. The render-only stress rows repeat a real project reference; this is not a full distinct-definition campaign benchmark.
- Blocker messages name research benches, linked facilities, required analysis items, mechanitors, inspection, and Node foundations.
- Visual Carpet-making check: the title and action buttons stayed fixed while its long unlock list scrolled. Research next moved it from position 32 to 1 while filtering without interrupting Psychoid brewing. The native filter retained true queue positions.

## Build, deployment, and cleanup

Both production Release builds and the integration fixture compile with zero warnings/errors. Twenty-five offline checks and translation-key validation pass. The standard production deploy verifies six files with zero missing, extra, or mismatched files. Final run logs contain no Research Queue error, ReflectionTypeLoadException, or UnityStubs warning from its code. Installed-mod discovery and engine warnings unrelated to Research Queue are not treated as its failures.

The real mod configuration differs from the earlier session snapshot; its last write predates the review-fix test runs, and its current contents were preserved. The temporary game processes and fixture installation are removed after testing. Production deployment excludes all source, tests, tools, and docs.

## Broader testing

RimWorld 1.6 is compile-checked only. Full Progression-pack campaigns, additional optional research systems, all display scales, dependent-confirmation popup interaction, sustained ten-day research history, and throughput with thousands of distinct definitions remain broader validation. The user previously confirmed manual native dragging; automated physical gestures remain unreliable. No Workshop release was performed.
