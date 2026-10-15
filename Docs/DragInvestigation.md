# Drag investigation

Date: October 15, 2026.

Status: the user confirmed manual click-and-drag reordering works. The automated gestures failed, but no change to native drag handling is justified. The precise automation event sequence remains unverified.

## Observations

- The first automated upward gesture did not reorder Pemmican; the card at the destination became selected.
- With the user's authorization, a copy of the baseline save was reloaded using the unchanged deployed build. Recurve bow and the original queue order were restored successfully, without a Research Queue error.
- A second, longer downward gesture from Beer brewing toward Smithing again selected the destination card and left the order unchanged. No reorder exception was logged.
- The disposable investigation game was left open while a manual drag result was requested. Investigation data is in `Projects/Generated/ResearchQueueColony-20261015-111423/`.

## Code inspection

Research Queue registers native reorder items before handling card selection, as Research Dashboard does. Its callback converts the native insertion boundary to an index after removal by subtracting one for a downward move. This agrees with the installed `Verse.ReorderableWidget.ReorderableWidgetOnGUI_AfterWindowStack`, which supplies a boundary in the original sequence and suppresses drops immediately before or after the dragged element.

The native widget requires a press to establish a dragged item, movement beyond its threshold during repaint, and a release. The pure queue move implementation removes the named project and reinserts it at the requested bounded index. Inspection found no demonstrated fault in those operations.

The installed engine's `RimWorldWaylith.Imgui.ImguiDispatch.DispatchInput`, decompiled from RimWorld.dll, samples one physical mouse position for the frame. It dispatches MouseDown using that position. It dispatches MouseDrag only when movement exists, the button is held, and GetMouseButtonDown is false for that button. It then dispatches MouseUp. A synthetic down/move/up sequence processed in one frame could be delivered at its destination without a usable drag phase. The two observed destination selections are consistent with that explanation.

This does not prove the automation gesture was processed in one frame and does not prove manual dragging works. Keep the failure recorded until a manual observation or event trace resolves it. Do not replace native drag handling based solely on the failed automated gestures.

## Manual result

The user reported: "Yes, I can manually move by clicking and dragging." Remaining colony checks resumed on the unchanged build. Filtering, Research next, and snoozing worked before reload. Testing then stopped on a separate snoozed-state reload issue, recorded in ColonyTest.md.

## Earlier next decision

If manual dragging works, record the automated gesture limitation and continue the remaining colony checks. If it fails, instrument the delivered event sequence and native reorder state before changing the interaction. A new mod build must be compiled, checked, and deployed, with permission before launching it for a game test.

## Final outcome

Manual dragging was confirmed by the user. The separate paused-load snooze bug was fixed and the core, Anomaly, Node Research, and Gravtech regression checks passed. See ColonyTest.md for the final results. The disposable processes are closed and the temporary fixture has been removed from the game installation.
