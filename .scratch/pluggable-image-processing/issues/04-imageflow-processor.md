Type: task
Status: open
Blocked by: 01, 02

## Question

Create `src/Umbraco.Image.Processing.ImageFlow`, implementing Core's `IImageProcessor` using the integration approach the ImageFlow research ticket recommends:

- Decode → resize/crop/format/quality/bgcolor/autoorient → encode, covering the full locked command surface, driven through Core's middleware (not `Imageflow.Server`'s, unless the research ticket concluded otherwise).
- Umbraco's `cc` crop/focal-point command, translated into whatever ImageFlow's job-graph API needs.
- Register as a selectable processor via Core's DI surface (`.UseImageFlow()`).

Apply the research ticket's answer directly — don't re-litigate the integration-mechanism decision here, just build against it.
