Type: task
Status: open
Blocked by: 05, 04

## Question

Write the in-process quickstart as its own linkable markdown file (root-level or a `docs/` location good for direct blog-post/README links):

- Walks through adding Core + one processor package to an existing Umbraco site, registering it in `Program.cs`, and configuring local-disk storage — grounded in what the in-process sample project actually does, not aspirational.
- Demonstrates the "drop-in" story explicitly: show the one-line swap between `.UseSkiaSharp()`, `.UseImageFlow()`, and (for comparison) the stock `.AddUmbracoImageSharp()`.
- No Aspire — plain `dotnet add package` / `Program.cs` steps only, so a reader can apply it to their own site directly.
