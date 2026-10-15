# Research Queue review fixes

October 15, 2026. All six approved review findings are addressed.

## Changes

1. Drag callbacks use visible project identities and insertion anchors instead of treating visible indexes as saved-list indexes. Missing research names remain in the saved queue for recovery.
2. Node's legacy queue transfers to Research Queue and is cleared. Ineligible legacy entries remain in a saved deferred list until eligible; completed entries are discarded. A FinishProject prefix captures any late legacy additions before Node's completion postfix can consume or announce them. Existing Scribe identifiers remain intact; `researchQueueDeferredNodeProjects` is additive.
3. Queue snapshots cache projects, true positions, New membership, and the active project per revision/system/filter/view. Only viewport rows receive card drawing and eligibility work. Native dragging still registers all row rectangles during repaint to preserve its ordinal mapping. A revision check rejects a drop against a superseded snapshot.
4. The integration fixture now creates its own research baseline in a fresh native quicktest colony. It requires an explicit test flag and the runner supplies separate save data. There is no external baseline-save dependency. Tests exercise actual hourly progress/idle sampling, automatic Gravtech selection/completion, Node foundation/emergence advancement, and a 1,000-row render-only stress case. The unused empty Common load-folder entries were removed so a fresh Git checkout can deploy without an untracked empty directory.
5. Project title, Snooze, Restore, and Research next occupy a fixed area above the scrolling details. Carpet-making was selected, its long unlock list scrolled, and Research next used while filtering; the actions stayed visible and the project moved to position 1 without interrupting current research.
6. Blocker details now identify techprint counts, required benches and linked facilities, mechanitors, grav-engine inspection, analysis items/progress, discovery, Anomaly study, and Node era/foundation/emergence requirements. Unknown third-party gates retain the fallback explanation.

## Validation

- Both production Release targets compile with zero warnings/errors. The integration fixture also compiles cleanly.
- 25 offline queue/rate/viewport checks pass, including retained missing definitions and partial viewport rows. English translation keys pass validation.
- Self-contained 1.7 runs pass for core/Anomaly, Node alone, Gravtech with Vanilla Expanded Framework, and Node plus Gravtech/Framework. The four suites contain 72 assertions in total (30, 14, 9, and 19). Every report ends COMPLETE. Production logs have no attributed error, type-load exception, or UnityStubs warning.
- The standard production deployment verifies all six runtime files with zero missing, extra, or mismatched files.
- Captured reports/logs are retained in the control project under `Generated/ResearchQueueReviewTests/{Core,Node,Gravtech,Combined}`.

The 1,000-row rendering test repeats real project references to check viewport work and cache reuse; it is not a full campaign benchmark with 1,000 distinct research definitions. RimWorld 1.6 is compile-checked only. Full-pack campaigns, other optional systems, sustained ten-day histories, and all display scales remain broader validation work.

The previous uncommitted mod was preserved as a separate baseline commit before committing the review fixes. No Workshop upload or release is part of this work.
