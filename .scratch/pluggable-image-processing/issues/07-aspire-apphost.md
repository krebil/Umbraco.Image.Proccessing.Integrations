Type: task
Status: open
Blocked by: 05, 06

## Question

Add a `.NET Aspire` `Umbraco.Image.Processing.AppHost` project that orchestrates the POC for local dev/demo:

- Wire up the `Umbraco` sample project and the `Umbraco.Image.Processing.Service` standalone project as an Aspire app graph, so `dotnet run` on the AppHost boots both.
- Structure it so a storage-provider emulator (e.g. Azurite, once Blob support is built post-POC) can be added later without restructuring — but don't build Blob support now; local disk only.
- This project is dev/demo tooling only — it must not be required by, or referenced from, either quickstart doc.
