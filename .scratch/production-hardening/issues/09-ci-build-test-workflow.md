# 09 — CI: build+test workflow (PR/branch push)

**What to build:** Every PR/branch push automatically builds the solution and runs the complete test suite — existing unit tests plus the new parity suite (06), cache-contract suite (04/05, including against Azurite), and HTTP integration tests (07) — with no publish step. This is the gate ticket 10's publish workflow will reuse.

**Blocked by:** 08 (needs the warnings-as-errors baseline and finished code from all prior tickets so the workflow's gate is meaningful from day one).

**Status:** ready-for-agent

- [ ] GitHub Actions workflow triggers on pull requests and branch pushes
- [ ] Workflow builds the full solution
- [ ] Workflow runs existing unit tests, the parity suite, the cache-contract suite (including the Blob backend against an Azurite service container), and the HTTP integration tests
- [ ] Workflow fails on any test failure or build warning (warnings-as-errors per ticket 08)
- [ ] Workflow has no publish/`nuget push` step
