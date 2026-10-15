# Research Queue implementation

## Current state

Reviewed local test build, October 15, 2026. All six approved findings are fixed; see ReviewFixes.md. Both production Release targets compile without warnings/errors. Twenty-five offline checks and translation-key validation pass. Seventy-two in-game assertions pass across four self-contained 1.7 suites. The standard deploy verifies six runtime files with no missing, extra, or mismatched files.

## Queue and persistence

Automatic selection works without opening research. New batches sort by era and total CostApparent, randomize exact ties, and append without sorting existing priorities. Separate Standard, knowledge-category, and Gravtech queues use their actual slots. Queues, snoozes, New markers, hourly history, and deferred Node imports are saved. Missing definition names remain for recovery, and dragging maps visible identities around them.

Snoozing excludes dependent branches, optionally confirms affected queued dependents, preserves progress, and supports restoration at the end. Blocked pending projects retain position; a blocked current project returns to the head of pending research and yields. Restoring requirements does not interrupt the replacement.

The paused-load exclusion cache rebuilds during FinalizeInit. Existing Scribe labels are retained. The deferred Node list is an additive save field; it preserves ineligible legacy entries while clearing Node's old queue so its completion callback cannot consume or announce a second queue.

## Interface and estimates

Native filtering preserves actual positions and cumulative estimates. Dragging is disabled while filtering; Research next remains available when eligible. Queue snapshots cache project identities, positions, New membership, and current research per revision/system/filter/view. Native drag geometry registers all rows, while expensive drawing and eligibility work cover viewport rows only.

The selected title and Snooze/Restore/Research next actions stay above scrolling details. Blockers identify native facilities, analysis, mechanitor, inspection, techprint, and discovery requirements and Node's progression gates. Unknown third-party gates retain a fallback explanation.

Estimates sum remaining runnable work using project rates with per-system fallback. Hourly history includes idle samples and retains up to 240 samples. Days or approximate map-local dates are available; missing positive rates display Unknown.

## Compatibility and drawing

Node's Collapse method preserves advancement counts, and its original tabs identify Gravtech. Tests cover actual foundation/emergence advancement. Gravtech selection and completion are tested automatically, without a direct Start call. Research Queue stands down with Research Auto, Semi-Random Research continued, or Research Dashboard active.

Cards follow the inspected continued Semi-Random Research fork's actual geometry, palette, separators, tint, progress fill, selected border, and foundation/emergence tags. Its source authors are Cruesoe, Captain Muscles (Nathan Harbour), and Arodoid. Research Dashboard supplied the native drag pattern. No artwork was copied, and no sibling project is a production dependency.

## Validation limits

See ColonyTest.md for current evidence. The integration runner creates its own temporary colony and research baseline and requires an explicit test flag. It is not deployed with the production mod. The 1,000-row case measures viewport work and cache reuse using repeated real references, not an entire campaign with 1,000 distinct definitions. Full-pack campaigns, other optional systems, all display scales, sustained ten-day history, and 1.6 runtime remain broader tests. No Workshop upload or release is part of these review fixes.
