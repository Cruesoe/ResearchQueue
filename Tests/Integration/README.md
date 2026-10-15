# Integration fixture

Development-only RimWorld 1.7 checks. Do not enable in an established colony: the fixture changes research progress, priorities, and Anomaly study state, saves RegressionRoundTrip, and reloads it automatically.

Build Source/ResearchQueueIntegration.csproj in Release after building Research Queue. This prepares the ignored 1.7/Assemblies fixture output. Deploy the Integration folder as a separate temporary mod using the standard Deploy-Mod script with ReposRoot pointing to this repository's Tests directory and Repository set to Integration. Production deployment never includes this folder.

Use a separate savedatafolder, Harmony, Research Queue, and cruesoe.researchqueue.tests. The baseline suite expects the disposable regression save described in Docs/ColonyTest.md: paused, Recurve bow active, Carpet-making explicitly snoozed, and 29 pending Standard projects starting with Brewing. A copy is retained in the control project's Generated directory. The fixture writes Integration.txt under that separate save-data folder. Success ends with COMPLETE; a failure ends with FAIL and an exception. Bring the test window forward so its GUI callbacks execute.

With Node Research or Vanilla Gravship Expanded Chapter 1 active, the fixture instead runs optional-mod smoke checks. Include those mods' required dependencies. It verifies queue routing, Node tab collapse and emergence definitions, and independent Gravtech slot operations. This path does not replace full progression testing.

Disable and remove the temporary fixture from the game after testing. It is never included in the production mod's runtime folders.
