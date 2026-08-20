Type: task
Status: open
Blocked by: 07, 08, 09

## Question

Run the manual verification pass that proves the destination is reached:

- Boot the Aspire AppHost.
- For each processor (SkiaSharp, ImageFlow) × each mode (in-process, standalone): confirm resize, format conversion, quality, bgcolor, autoorient, and the `cc` crop/focal-point command all render correctly.
- Confirm HMAC-signed URLs are accepted and tampered/unsigned URLs are rejected, in both modes.
- Follow both quickstart docs literally (as if a blog reader) and confirm they match what's actually checked in — fix any drift found.
- Record the outcome as the ticket's answer; this closes the map.
