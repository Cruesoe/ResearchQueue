# Research Queue Design

Status: workable local test build, deployed for RimWorld 1.6 and 1.7. Offline checks and isolated 1.7 regression and optional-mod smoke tests pass. Broader pack and campaign validation remains.

Last updated: October 15, 2026.

## Purpose

Research Queue is a new standalone mod built around an automatic, visible, editable research queue. Its central feature is the interface through which players inspect and change research priorities.

Research Auto remains a separate mod whose purpose is unobtrusive automatic selection of the next project. Research Queue is not an extension of Research Auto and does not require it. Research Auto's selection logic remains useful reference material, alongside Semi-Random Research's interface and selection-blocking behavior.

The interface should make research priorities easy to inspect and change while keeping research moving automatically. It must work with ordinary RimWorld mod lists and Ferny's Progression pack. Node Research integration is optional.

Players must never need to open the research screen to keep research running. Each system automatically queues available projects, selects research, and appends newly available projects. All research can eventually complete if its requirements are met, the colony supplies the necessary research work, and the player has not snoozed it. The screen exists to inspect progress, change priorities, and manage snoozed projects; opening it is not required to activate automation.

Individual research projects retain their costs, prerequisites, progress, and unlock timing. The design changes how players manage research, without combining projects or accelerating research.

## Queue behavior

- For a new colony, or when first enabling this feature in an existing save, add every eligible research project, sorted by era ascending and then cost ascending.
- After each research completion, check availability again and append newly eligible projects to the end, sorted by era ascending and then cost ascending within that new batch.
- Also check after loading and periodically during play so availability changes outside research completion are discovered. The initial implementation checks every 250 ticks, with completion-triggered refreshes.
- Only add unfinished projects that can start under the game's and applicable mods' requirements. Completed prerequisites alone are insufficient: a project requiring a high-tech research bench is not added until the required bench has been built and the project can start.
- Once added, retain a project and its queue position if its requirements later become unmet. Mark it blocked and show the reason. Temporarily skip it so other eligible research can continue. When its requirements are met again, it becomes eligible at its existing position. Availability checks must not silently remove it. Completion and deliberate snoozing remain reasons to remove entries.
- When the current project completes, continue with the next eligible queued project.
- If current research becomes blocked, return it to the head of pending research and start the next eligible project. Restoring its requirements does not interrupt the replacement. This implementation decision was made under the user's authorization to resolve errors and finish a workable build, and passed the isolated test.
- When a system is idle, automatically start its next eligible queued project, including when a periodic availability check discovers research after its queue was empty or blocked.
- Let players drag entries to reorder their priorities.
- Do not add a queue pause control. Players exclude unwanted projects through snoozing.
- Preserve existing queue order and player priorities when appending new projects. Do not re-sort the existing queue after research completion.
- For initial sorting and newly appended batches, cost means total research cost shown to the player, including applicable adjustments. Do not sort by remaining work or give partially researched projects priority. Verify the appropriate cost accessor for each research system during implementation.
- Randomize the relative order of projects tied on era and total player-facing cost when forming the initial queue or a newly appended batch. Preserve that order afterward, including across saves; do not randomize existing entries on availability checks or loading.
- Show all eligible research across eras together within each research system's queue. Eligibility is determined by the game and applicable mods.
- Use the existing game research filter. Custom category filters are out of scope.
- Show project details, including unlocks, cost, and progress.

There is no separate available-project list requiring players to manually add research. Every eligible project is already queued unless snoozed or excluded by its snoozed dependency.

### Separate research systems

Maintain separate queues for research that operates through different systems, following Semi-Random Research's approach. Standard research, Anomaly research, and Gravship Expanded's Gravtech must not share one sequential queue. Apply the same principle to other supported systems, such as Divinitech, after verifying their research slots and progression rules.

- Each system has its own queue order and current project, subject to that system's actual research rules.
- Populate and append to each queue using the agreed availability and sorting rules. Recheck all systems after a completion because it may unlock research in another system.
- Reordering or completing research in one queue must not reorder another queue or make independent research wait behind it.
- Preserve each system's eligibility, research method, and concurrency rules. Separate queues do not introduce new parallel research slots.
- Save queue and snoozed state for each system. Dependency-based snoozing must also check for affected projects in other queues.
- Provide research-system tabs at the top of the research screen: Standard, Anomaly, Gravship Expanded's Gravtech, and other supported systems as applicable to the installed game and mods. Selecting a tab displays that system's queue, current research, and snoozed projects so players can manage it separately. Switching tabs does not pause research or change queue order. These are research-system tabs, not era selectors or custom category filters.

Inspect Semi-Random Research's existing system classification and handling before choosing the integration details. The requirement for separate queues is agreed; the complete supported-system mapping still needs verification.

### Save and load

Save the queue order and snoozed state with the colony. Loading restores these and preserves current research and its progress; it must not rebuild or re-sort the existing queue. Reconcile completed research and append newly eligible projects without duplicates. Adding or removing mods and handling missing research definitions require a separate recovery policy.

### Research selection ownership

While this mod is active, block normal research selection outside its queue interface. Use Semi-Random Research's approach as the implementation reference, inspecting its selection-blocking code before adopting specific patches or drawing behavior. Players change research priorities through this mod's queue rather than selecting a competing current project through the normal research interface.

This resolves the handling of manual selection through other interfaces: players change priorities through the queue. Research Queue stands down with Research Auto, Semi-Random Research continued, or Research Dashboard active, so those automatic selectors cannot compete with it. Other automatic selectors require compatibility checks before being supported.

## Snoozing

Players can snooze research they do not want pursued in the current playthrough.

- Remove the snoozed project from the queue.
- Keep it excluded from automatic queue additions until the player restores it.
- Also remove queued projects that depend on it, directly or indirectly.
- Removed dependents can return automatically when their prerequisites have been completed and they are eligible, unless independently snoozed.
- Provide a Snoozed view within each research-system tab. Detailed presentation remains to be designed.
- Distinguish explicitly snoozed projects from projects excluded because of a snoozed prerequisite, and show the reason for exclusion.
- Provide one Restore action in the Snoozed view, available at any time, including after closing the research screen or loading a save and when confirmation popups are disabled. There is no separate Undo action or time limit.
- Restore clears the project's explicit snoozed state. Once eligible, it returns to the end of its research system's queue rather than its former position. Restoration does not bypass start requirements or any remaining snoozed prerequisite. Independently snoozed dependents remain snoozed.
- Snoozing current research preserves its accumulated progress and advances to the next eligible queued entry.

### Optional confirmation

A snooze confirmation popup appears only when queued dependent projects would also be removed. It lists those projects, and the player can confirm or cancel the action. Snoozing a project with no affected queued dependents does not require confirmation.

Confirmations are enabled by default and can be disabled in settings.

## Completion estimates

Each queue entry shows its cumulative estimated time until completion, including:

1. The current project's remaining research work, when it precedes that entry.
2. The remaining work of every preceding queued project.
3. The entry's own remaining work.

For example, if the first three projects require two, three, and one more days of research, their cumulative unlock estimates are approximately two, five, and six days.

Players choose the display format:

- Days remaining, such as "Unlocks in ~6 days".
- Approximate in-game completion date, such as "Unlocks around 8 Aprimay, 5501".

Follow Semi-Random Research's estimate behavior: sample research progress hourly and average available history up to the previous ten elapsed in-game days (240 hourly samples), including time when no research occurred. Recalculate estimates when queue order, remaining research work, or measured research rate changes.

Use shorter available history rather than waiting for ten full days. Prefer the project's measured rate, with a fallback to the relevant system's overall average when no positive project rate is available. If no positive rate is available, display "Unknown" instead of a time or date. Save the history with the colony so loading does not restart collection.

Calculate cumulative estimates within the relevant research system, not across independent queues. Do not use standard research speed to predict a system that progresses through a different mechanism. Determine an appropriate rate measurement for each supported system; show "Unknown" when its completion time cannot be meaningfully predicted.

This is an estimate rather than a guarantee. Semi-Random Research's shorter-history, rate-fallback, and unknown-estimate behavior are adopted, adapted to separate systems and cumulative queue completion. The implementation sums runnable entries using project rates with per-system fallback and references dates to the current or first colony map.

## Interface constraints

- The research queue is the main focus of the screen.
- Keep the screen clean, with only controls and information needed to manage research. Stay broadly consistent with RimWorld's visual style, using Semi-Random Research as the starting reference.
- Move projects by clicking and dragging their cards. Do not require a separate move mode or a sequence of reorder buttons.
- Inspecting cards must not move other entries. When entries are added, snoozed, or completed, preserve remaining entries' relative order and keep navigation stable; no physics-driven movement or automatic layout reshuffling.
- No research tree or physics simulation.
- No era selector or future-research view.
- No custom category filters or researchable parent-category projects.

The earlier Neolithic category analysis remains useful reference material, but it is not a requirement to build a category classifier or category-based research programs.

### Filtering and reordering

Filtering changes visibility only. Keep actual queue positions and cumulative completion estimates: hidden entries still precede visible entries and contribute to their estimates. Disable drag reordering while a filter is active for the initial design.

Provide a "Research next" action that places a selected queued project immediately after the current research in its system's queue, without interrupting current work or changing other projects' relative order. Allow this only when its dependencies and start requirements permit it. Do not bypass prerequisites or automatically move prerequisite projects. When unavailable, disable the action and explain the unmet requirement in a tooltip. This action remains available while filtering when those requirements are met.

### Screen layout

The broad queue with a persistent details panel is the current layout direction. The user approved its usefulness as a layout mockup, but its visual styling does not represent the intended in-game design.

- Do not put the mod name in the screen content or reserve space for a branding header.
- Give the queue most of the screen's width and height. Roughly 20 visible projects is an initial capacity estimate to test, not a required minimum, a fixed visible count, or a limit on queue length. Scroll for longer queues.
- Place the research-system tabs at the top of the screen. Below them, keep the existing research filter and the days/date estimate selector in a compact row for the selected system.
- Place current research in a compact strip above the queue, showing its name, progress, and estimated completion.
- Use the same research-card contents as Semi-Random Research; do not define a separate custom set of card fields. Inspect its drawing code to establish the exact contents. Queue position, drag reordering, snoozing, and the agreed cumulative completion estimate still need to be accommodated around that presentation; their exact placement remains to be designed.
- Highlight the selected card and show its research details in a narrower panel on the right. Include unlocks, research cost, progress where applicable, and the cumulative completion estimate.
- Provide Queue and Snoozed views so players can inspect and restore excluded research. The exact snoozed-view layout remains open.
- Keep details outside the cards so inspecting a project does not expand the list or move other entries.
- Mark newly appended research cards with a subtle "New" indicator so additions are noticeable without a popup. Keep the indicator when a card is selected; clear it when the research screen is closed.

The revised layout sketch showed 20 rows at 1920 x 1080. This is a planning reference, not a verified in-game viewport or a fixed required resolution. Test how many complete cards comfortably fit in RimWorld's actual research window at the relevant display resolutions and UI scales, then choose dimensions based on those results rather than forcing a count of 20. Use readable `GameFont.Small` text; do not fit more entries by using `GameFont.Tiny`.

### In-game drawing and appearance

Use Semi-Random Research as the primary reference for drawing the screen in game, reusing many aspects of its existing research interface where appropriate. Adapt those drawing patterns around the automatic editable queue, including RimWorld-native controls, fonts, spacing, tooltips, selection, and research-detail presentation.

The current visual preference is to start with Semi-Random Research's card-based presentation, colors, and related styling, adapted to an ordered queue. This is a provisional direction, not a confirmed final appearance. Card contents are agreed to match Semi-Random Research rather than the earlier proposed custom fields or Node Research highlights. Explore how cards can preserve readable queue positions, cumulative completion estimates, drag reordering, and snooze actions while testing the initial estimate of roughly 20 visible projects. The earlier compact-row sketch establishes information needs; it does not require plain table rows instead of cards or a fixed visible count.

Semi-Random Research's drawing implementation has now been inspected and used as the card-rendering reference. The first test build follows its actual icon/name/cost geometry, palette, separators, progress fill, selected border, and foundation/emergence tags. The mockups establish information placement and queue capacity only; their custom colors, controls, and styling are not the visual specification.

The generated mockup images were deleted at the user's request. Example research names, costs, and estimates used in the sketches were illustrative and are not research data or balancing decisions.

## Node Research compatibility

With Node Research installed, retain its eligibility and advancement behavior, including foundation and emergence research. Do not implement a separate advancement system.

Snoozing optional research should let players leave unwanted branches out of their plans while advancing under Node Research's existing rules. Snoozing does not waive foundation, emergence, or other progression requirements.

Reuse or integrate with Node Research's existing queue state and research-start behavior as appropriate; Research Queue owns selection and the screen, calls Node's Collapse method, and imports eligible legacy queued entries once. Two competing automatic research selectors must not operate independently.

## Further validation and refinements

- Full Progression-pack, long campaign, and hundreds/thousands-of-project performance tests.
- In-game testing on 1.6, other knowledge-category mods, and optional-mod combinations beyond the tested Node/VGE Chapter 1/Framework combination.
- Dependent-confirmation popup interaction and visible capacity across supported resolutions and UI scales.
- Full Anomaly and Gravtech progression and actual elapsed ten-day rate collection. Positive-rate integration checks used seeded history.
- A player-facing recovery policy for definitions removed by changing a mod list. The current implementation retains unknown saved names for recovery if their mod returns.

Implemented choices: 250-tick periodic checks; total CostApparent sorting; per-project rates with per-system fallback; blocked entries skipped in cumulative estimates; dates referenced to the current or first colony map; native filtering; actual Semi-Random Research card patterns; separate knowledge-category and Gravtech slots; and stand-down with competing automatic selectors. These are documented in the implementation and test records. Twenty visible entries remains a capacity estimate rather than a fixed requirement.

## Existing selection behavior: code validation

Checked the local source of Research Auto and the continued Semi-Random Research fork. These observations describe their current selection logic; differences have not been adopted automatically into this queue design.

### Research Auto

Source: `RimWorld/ResearchAuto/ResearchAuto/ResearchAuto.cs`, notably `TryStartResearch`, `PreferProjectsWithProgress`, and `SelectCandidate`.

1. Collect unfinished projects that can start now, with separate Standard, Anomaly, Gravship, and Divinitech handling.
2. Remove candidates whose required research slots are already occupied.
3. If any candidates already have progress, discard candidates without progress for this selection.
4. For Standard research, select the lowest available tech level unless Ignore Tech Level is enabled.
5. Select the lowest `baseCost`, or highest when Prioritize Expensive Projects is enabled.
6. Randomly choose among projects tied on the selected era and cost.

The default Standard ordering uses era ascending and then cost ascending, but Research Auto first prefers partially researched projects. Its existing cost comparison uses total raw `baseCost`, not remaining work or `CostApparent`, despite the About description referring to remaining cost. Research Queue instead uses total player-facing cost and does not prioritize existing progress or adopt the expensive-first option. Random ordering of equal-era, equal-cost projects is agreed for Research Queue when forming a batch; the resulting queue order is then preserved.

Research Auto schedules an idle check 300 ticks after completion, polls for idle research every 2,500 ticks, and defers selection while the Research tab is open. It selects one project at a time and has no persistent player-editable queue.

### Semi-Random Research

Source: `RimWorld/Semi-Random-Research-P-Fork-Continued/Source/ResearchTracker.cs`, notably `CheapestStartableOffer`, `CheapestStartable`, and `AutoPickNow`.

Auto mode selects the cheapest startable project from the current offers for each research system. When there is no suitable offer, it refreshes the offers and repeats. It compares total adjusted `CostApparent`, not remaining work. Ties retain offer-list order. The offered pool is controlled by the semi-random choice generation and its settings.

Automation respects paused research systems and waits while its own research window is open. When that window owns the Research tab, automation is armed by opening it and can pick when it closes; another research-tab owner arms it from the start.

### Semi-Random Research completion estimates

Checked `Source/ResearchRateTracker.cs`, `MainTabWindow_NextResearch.cs`, and `MainTabWindow_NextResearch.Progress.cs` in the local continued fork.

- Samples progress hourly, retains up to 240 samples (ten in-game days), and includes idle hours as zeros. Samples persist in saves.
- `AverageRecent` averages the available samples up to that limit; it does not require a full ten-day history before estimating.
- Project estimates use remaining `CostApparent - ProgressApparent`, divided by a positive measured rate. They prefer the project's own rate and can fall back to the global rate, which combines sampled active projects across systems.
- If neither rate is positive, the project ETA displays "Unknown". Remaining-research summary estimates display "Unknown (no 10-day average yet)" when no positive average is available, including an all-zero window.

Research Queue follows this estimate behavior, including shorter available history and "Unknown" when no positive rate is available. Adapt the global fallback to the relevant research system so different research mechanisms are not combined. Cumulative queue estimates and the optional in-game date display remain Research Queue requirements.

## Supporting research

- Master Progression Mod List in the control project: mod inventory, research counts, era breakdown, and the earlier Neolithic category study.
- Node Research code inspection in the control project: existing eligibility, advancement, queue, layout, and save behavior.

## Construction status

### Current workable test build

The standalone mod is implemented in `C:\Users\crues\source\repos\RimWorld\ResearchQueue`, package ID `cruesoe.researchqueue`. Both Release builds pass with zero warnings/errors. Eighteen offline queue/rate checks and translation-key checks pass. The standard deployment verified six runtime files with zero missing, extra, or mismatched files.

The user confirmed manual drag reordering works. Native filtering preserves queue positions, and Research next moves an entry without interrupting current research. The actual Semi-Random Research card renderer supplied the icon/name/cost geometry, era and special-system palette, separators, tint, progress fill, selected border, and foundation/emergence tags. The native reorder pattern follows Research Dashboard. There are no sibling build or runtime dependencies.

A paused-load bug was found and fixed: explicit snoozes were saved correctly, but the derived exclusion cache was empty until the first tick. FinalizeInit now rebuilds that cache before the screen can use it. The integration suite loads the original failing save, changes priorities and snoozes, saves again, and reloads while paused.

Sixty-three in-game regression checks passed in the disposable core/Anomaly run, including paused-load restoration, order persistence, selection ownership, completion advancement, append ordering, blocked-current handling, cumulative estimates, saved rate history, and Anomaly slot isolation. Both days and approximate-date displays were inspected. Positive rates were seeded for calculation tests rather than measured over a ten-day campaign. The user subsequently authorized autonomous fixes and tests, superseding the earlier stop-and-ask workflow.

Sixteen additional smoke checks passed with Node Research, Vanilla Gravship Expanded Chapter 1, and Vanilla Expanded Framework together. Research Queue owns the research screen; Node's collapsed Main tabs and generated emergence definitions remain; queue routing has no duplicates; and Gravtech selection and snoozing preserve Standard research. These smoke checks do not establish every optional-mod combination or a full Progression campaign.

Logs and saves are retained under `Projects/Generated/ResearchQueueColony-20261015-115535/` and `ResearchQueueColony-20261015-115802/`. Detailed results and limitations are in the mod repository's Docs/ColonyTest.md and Docs/Implementation.md. The development-only integration harness is retained under Tests/Integration and is excluded from production deployment. The temporary test fixture is removed from the game after testing. The real active mod configuration was verified unchanged. No established colony, release, publication, commit, or push was involved.

The initial implementation retains unknown saved definition names rather than silently deleting them. Blocked pending entries retain their positions; a blocked current project returns to the head of pending research while an eligible replacement proceeds. Clearing its requirements does not interrupt that replacement. Broader display-scale, large-pack, and long campaign testing remains as listed above.

### Previously verified skeleton

The standalone skeleton is in `C:\Users\crues\source\repos\RimWorld\ResearchQueue`, initialized as a local Git repository on `main`, with package ID `cruesoe.researchqueue`.

It contains mod metadata, a localized settings entry with the agreed dependent-snooze confirmation default, and saved state containers for separate queues, explicitly snoozed projects, and new-project markers. Research selection and the existing research screen remain unchanged at this stage. Queue population, automation, drag-and-drop cards, estimates, and compatibility patches are not implemented.

Release-configuration builds for RimWorld 1.6 (`net472`) and 1.7 (`net10.0`) passed with zero warnings and errors. All three runtime XML files parsed, and the load-folder paths exist. This is compile and static validation only; persistence and UI behavior have not been tested in game.

After the user approved creating a new deployment target, the standard `Deploy-Mod.ps1 -Repository ResearchQueue -NewTarget` script deployed the skeleton to `Mods\Research Queue`. It rebuilt both versions with zero warnings and errors and verified all five deployed files by SHA-256: zero missing, extra, or mismatched files. The deployed metadata and both versioned assemblies were also spot-checked.

With the user's permission, an isolated RimWorld 1.7.4979 rev109 startup check was launched with Core, the installed DLCs, and `cruesoe.researchqueue`, using separate save data and `-NoFileLog` with captured console output. The original process reached the main menu, confirmed visually, and was then closed through the game's Quit to OS control. No Research Queue errors, assembly type-load exceptions, or stub warnings from its code appeared in the captured output. The skeleton startup check passed; colony behavior, saved-state round trips, and the settings UI still require separate tests.

The initial report that the process had ended was incorrect: the restricted process list did not show the game, while an elevated read confirmed that the original process was still running and responding. Windows had no corresponding crash or hang event. An attempted repeat launch was stopped by the already-running-process guard, so no second game instance was started. No mod code fix was necessary.

The log contains unrelated installed-mod dependency-link warnings from mod discovery, a Vulkan layer registry warning, and a Steam Fossilize filter message. These did not prevent the main menu from loading and are not attributed to Research Queue.

Captured output is retained under `Projects/Generated/ResearchQueueStartup-20261015-093127/`. The active mod configuration was not edited. No commit, push, or publication was performed. The repository's `Docs/Implementation.md` records the skeleton scope and subsequent construction stages.
