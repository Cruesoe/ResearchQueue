# Research Queue implementation

## Current state

Workable local test build, October 15, 2026. Both Release targets compile with zero warnings or errors. The standard deployment verified six runtime files with zero missing, extra, or mismatched files. Eighteen offline checks, 63 in-game regression checks, and 16 optional-mod checks passed. See ColonyTest.md for evidence and limits.

## Implemented

- Automatic selection and append-only queues without opening the screen. New batches sort by era, then total CostApparent, with randomized ties; existing priorities stay intact.
- Separate Standard, knowledge-category, and Gravtech queues, using their actual current-project slots.
- Saved order, snoozes, New markers, and hourly rate history. Missing definition names retain their saved positions.
- Snoozing with transitive dependency exclusion, optional dependent-only confirmation, restoration at the end, and preserved research progress.
- Blocked pending entries retain their positions and are skipped. A blocked current project returns to the head of the pending queue; eligible research proceeds. Restored requirements do not interrupt its replacement.
- A full research screen with native filtering, details, Research next, Snoozed views, manual drag reordering, and New markers cleared on closing.
- Cumulative days or approximate in-game dates, project rates with per-system fallback, idle hours, up to 240 hourly samples, and Unknown without a positive rate.
- Normal selection blocked at the research manager. Research Queue stands down when Research Auto, Semi-Random Research continued, or Research Dashboard is active.
- Node Research's Collapse method preserves its advancement counts; eligible legacy entries can be imported once. Original tabs identify Gravtech even with Node present.

## Corrections

The snoozed state was saved correctly, but its derived exclusion cache was rebuilt only on a game tick. Loading a paused colony therefore showed an empty Snoozed view. FinalizeInit now rebuilds that cache before the first tick. The regression suite loads the original failing save and performs a further save/reload while paused.

Blocked active research now yields without losing its progress or pending priority. This follows the user's authorization to resolve implementation issues and complete the workable build.

## Drawing provenance

The renderer follows the inspected continued Semi-Random Research fork's actual 48-pixel icon/name/cost geometry, era and Anomaly/Gravship palette, separators, tint, progress fill, selected border, and foundation/emergence tags. Its source authors are Cruesoe, Captain Muscles (Nathan Harbour), and Arodoid. Research Dashboard supplied the native ReorderableWidget registration pattern. No artwork was copied. No sibling repository is a production build or runtime dependency.

## Validation limits

RimWorld 1.7.4979 rev109 was tested with official DLCs, Harmony, and disposable colonies. RimWorld 1.6 is compile-checked only. Node Research and Vanilla Gravship Expanded Chapter 1 were smoke-tested together with Vanilla Expanded Framework; this does not establish every optional-mod combination or a full Progression campaign. Anomaly Basic slot isolation was exercised; a full Anomaly campaign and all other knowledge-category mods were not.

Positive-rate calculations used seeded test history, not a ten-day colony observation. The dependent-confirmation dialog and all display resolutions remain for broader player testing. About 15 complete cards fit the captured viewport using Small text; 20 is not a hard limit. Automated drag gestures were unreliable; the user confirmed manual dragging works.

The integration harness lives under Tests/Integration. Its runtime fixture is for disposable saves only and is not part of the production deployment. No release, publication, commit, or push was performed.
