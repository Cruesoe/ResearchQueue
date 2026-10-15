# Integration fixture

Development-only RimWorld 1.7 tests. The fixture runs only with `-researchQueueTests`, creates its own research baseline in a fresh Dev quicktest colony, and saves/reloads RegressionRoundTrip. Never pass that flag while loading an established colony.

Build Source/ResearchQueueIntegration.csproj in Release after building Research Queue. Deploy this Integration folder as a separate temporary mod using the standard Deploy-Mod script, with ReposRoot pointing to the repository's Tests directory and Repository set to Integration. The ignored 1.7/Assemblies output is not part of production deployment.

Run Tools/Start-IntegrationTests.ps1 from the repository when game testing is authorized. It refuses to launch while RimWorld is running, creates a fresh temporary savedatafolder, and uses native `-quicktest` to generate the colony. It never copies an existing save or changes the real mod configuration. It returns the process ID and report/log paths. Success ends Integration.txt with COMPLETE; failure records FAIL and an exception. Keep the window visible when testing rendering.

The default suite checks selection ownership, snooze/restore, missing-definition drag mapping, real hourly progress and idle sampling, automatic completion/appending, blocker messages, paused save/load, Anomaly isolation, and cached viewport rendering with 1,000 render-only rows. The render stress case repeats a real project reference to test UI scaling without changing the game's research definitions; it is not a full 1,000-project campaign benchmark.

For optional-mod runs, provide the full ModList parameter including Harmony, Research Queue, and cruesoe.researchqueue.tests. Test Node Research alone, Vanilla Gravship Expanded Chapter 1 with Vanilla Expanded Framework, and those mods together. Node tests complete foundations, automatically select emergence, and verify era advancement and legacy-queue handover. Gravtech tests assert automatic selection and completion advancement without directly calling ResearchQueueRuntime.Start.

Optional-mod suites do not replace full-pack campaign tests. Stop the returned disposable process after the report completes, and remove the temporary fixture installation after all runs. No fixture content belongs in the production deployment.
