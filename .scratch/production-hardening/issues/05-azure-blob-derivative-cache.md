# 05 — Azure Blob derivative cache + Azurite dev/test infra

**What to build:** A platform engineer can run the standalone Service across multiple instances behind a load balancer, sharing one derivative cache in Azure Blob Storage. Add a new `IDerivativeImageCache` implementation backed by Azure Blob Storage — additive against the existing seam, including the TTL eviction behavior from ticket 04, no interface change required. Wire an Azurite resource into `Umbraco.Image.Processing.AppHost` via Aspire's `AddAzureStorage().RunAsEmulator()`, so the Blob backend is developed and tested against a real Blob-API-compatible emulator with no Azure subscription required. Extend the shared cache-contract suite from ticket 04 to run parameterized against this Blob implementation + Azurite.

**Blocked by:** 04 (extends its contract suite; needs the eviction member already defined on the interface).

**Status:** ready-for-agent

- [ ] New Blob-backed `IDerivativeImageCache` implementation exists, implementing read/write/clear and TTL eviction
- [ ] `Umbraco.Image.Processing.AppHost` wires in an Azurite resource via `AddAzureStorage().RunAsEmulator()`
- [ ] The Blob cache implementation can be configured to run against the Azurite emulator locally (no real Azure subscription needed)
- [ ] The shared `IDerivativeImageCache` contract suite from ticket 04 runs parameterized against the Blob implementation + Azurite, alongside the local-disk backend, without duplicating test logic
- [ ] Contract suite passes against both backends
