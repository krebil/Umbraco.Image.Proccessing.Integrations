Type: task
Status: resolved
Blocked by: 05, 06

## Question

Add a `.NET Aspire` `Umbraco.Image.Processing.AppHost` project that orchestrates the POC for local dev/demo:

- Wire up the `Umbraco` sample project and the `Umbraco.Image.Processing.Service` standalone project as an Aspire app graph, so `dotnet run` on the AppHost boots both.
- Structure it so a storage-provider emulator (e.g. Azurite, once Blob support is built post-POC) can be added later without restructuring — but don't build Blob support now; local disk only.
- This project is dev/demo tooling only — it must not be required by, or referenced from, either quickstart doc.

## Answer

Built `src/Umbraco.Image.Processing.AppHost` via the `Aspire.ProjectTemplates` (13.5.0) `aspire-apphost` template, targeting `net10.0` to match the rest of the solution (Aspire 13.x dropped the workload requirement — it's SDK-resolved via `Aspire.AppHost.Sdk` and pulled from NuGet like any package, no `dotnet workload install` needed).

`AppHost.cs` wires the graph:

```csharp
var imageProcessingService = builder.AddProject<Projects.Umbraco_Image_Processing_Service>("image-processing-service");

builder.AddProject<Projects.Umbraco>("umbraco")
    .WithEnvironment("ImageProcessing__Standalone__BaseUrl", imageProcessingService.GetEndpoint("http"))
    .WaitFor(imageProcessingService);
```

`WithEnvironment` overrides `ImageProcessing:Standalone:BaseUrl` (the config key `Umbraco/Program.cs` already reads) with the DCP-assigned endpoint for the service resource, so the graph edge is real, not just two processes launched side by side — when the sample runs in `Standalone` mode under the AppHost, its `ExternalBaseUrl`/redirect target tracks wherever DCP actually placed the service, not a hardcoded port. Nothing here forces `Mode`; that stays config-driven in `appsettings.json` per the existing convention.

Only two resources are wired (`AddProject` calls) — no `AddContainer`/`AddAzureStorage` etc. yet — so a future Azurite (or similar) resource is a pure addition, not a restructure. Added to the solution as its own project (not referenced by, or referencing, either quickstart doc).

Verified via `dotnet run` on the AppHost with Docker Desktop as the container runtime (DCP itself requires a container runtime to be *present* even though this graph declares zero containers — Docker was already installed locally): both `umbraco` (ports 44392/30552, `200` on `/`) and `image-processing-service` (port 5050, `400` on a signed-request check against a nonexistent original — correct rejection, not a crash) came up under DCP with no errors, dashboard reachable. Processes and DCP were torn down cleanly after verification.

**Environment note**: this machine had Docker Desktop installed already, so DCP's container-runtime dependency check passed silently. If a future contributor lacks Docker/Podman entirely, `dotnet run` on the AppHost will fail at DCP's dependency check even though this graph never schedules a container — that's Aspire/DCP's own behavior, not something this ticket's wiring controls, and isn't a hard blocker since `Umbraco` and the service can still be run individually without the AppHost.

**Note for the driving session**: a concurrent session had, at answer time, added an uncommitted solution folder to `Umbraco.Image.Processing.Integrations.sln` named `Umbraco.Image.Processing.Service` — colliding with the existing *project* of the same name and breaking `dotnet build` on the full `.sln` (MSB5004: duplicate name). Not touched here since it's someone else's in-flight work; flagged so it gets resolved before the full solution is expected to build. The AppHost project itself and its direct dependents build and run cleanly in isolation.
