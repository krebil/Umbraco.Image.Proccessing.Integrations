Type: task
Status: open
Blocked by: 06, 04

## Question

Write the standalone-deployment quickstart as its own linkable markdown file, alongside the in-process one:

- Walks through building a bare ASP.NET Core service hosting Core + a chosen processor, grounded in what the `Umbraco.Image.Processing.Service` sample project actually does.
- Documents the redirect/CDN pattern from `imagesharp-standalone-service-plan.md`, made processor-agnostic — this is the part that stays doc-only, no code, per the earlier decision that URL generation never needs to differ by processor or deployment mode.
- Covers HMAC secret sharing between the two apps, and the same "swap the processor" one-line story as the in-process doc.
- No Aspire.
