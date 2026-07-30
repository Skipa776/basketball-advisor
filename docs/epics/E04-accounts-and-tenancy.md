# E04 — Accounts and tenancy

**Requirements:** R20 · **Depends on:** E03

ASP.NET Identity plus real data ownership. Runs fourth on purpose: the isolation
sweep enumerates routes by reflection, so every endpoint added by E05–E11 is covered
automatically. Landing auth last would mean retrofitting a dozen endpoints at once.

## Pre-flight

E03 landed green, all ten MVP criteria passing · `scripts/gate.sh` green · **back up
your database** — this epic migrates the schema twice.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E04 in this repository: accounts and data ownership.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R20),
   ARCHITECTURE.md post-MVP section, and context/AGENT_CONTEXT_INDEX.md. Then
   read, in full:
     context/codebase_okf/contracts/auth_tenancy_contract.md
     context/codebase_okf/safety/tenancy_policy.md
     context/codebase_okf/components/identity_and_authorization.md
     context/codebase_okf/contracts/persistence_contract.md
     context/codebase_okf/tests/test_matrix_auth_tenancy.md
   Add Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.10 from
   stack_config.toml [dependencies.epic] to Directory.Packages.props.

2. Scope, in order:
   a. FantasyUser : IdentityUser<Guid> in the existing FantasyDbContext and the
      existing migration history. One context, one connection.
   b. The ownership retrofit, in three steps and no fewer:
        i.   add OwnerId as NULLABLE to every owned entity; migrate
        ii.  on first registration, claim every null-owner row for that user
        iii. a second migration makes OwnerId non-nullable
      The pre-auth instance was single-user by construction, so step ii is
      correct and is the only moment it is correct. A one-step migration either
      loses the existing league or needs a placeholder user.
   c. Enforcement: an IOwnedResource marker interface, query filters applied in
      OnModelCreating by ITERATING entity types that implement it — never a
      hand-written list — plus explicit authorization at each endpoint.
   d. IUserContext resolving from the request principal, THROWING in a worker
      scope rather than returning a default user.
   e. Registration: first registrant becomes the sole instance owner; afterwards
      registration is closed unless Auth:OpenRegistration is true, and the
      register endpoint returns 404 when closed.
   f. Account pages: register, login, logout. Cookie auth with HttpOnly, Secure,
      SameSite=Lax; HTTPS required outside Development; 12-character minimum
      password with no composition rules; lockout at 5 failures for 15 minutes.

3. Required test rows: U-01 to U-17. Two of them are the epic:
   - U-01 enumerates owned routes BY REFLECTION and calls each as user B against
     user A's resources, asserting 404 — never 403, never 200.
   - U-02 plants a deliberately unfiltered owned entity in a fixture and asserts
     the sweep FAILS. A sweep that cannot fail is not a test.

4. The owned/shared split in the contract is exact. Reference data (players,
   stats, schedules, ADP, baseline projections) is SHARED and must not be
   duplicated per user: the scrape budget is 6 requests per minute, and per-user
   duplication of NBA data is a ban rather than a feature. AdjustedProjection is
   the one per-row case — nullable owner, null meaning global.

5. Commit after each of a-f. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

6. Budget ladder: (a) Identity + the nullable migration; (b) the claim step and
   the non-nullable migration; (c) query filters and endpoint authorization;
   (d) the sweep; (e) account pages. Do not stop between b and c — a schema with
   ownership columns and no enforcement is worse than no ownership at all.

7. Non-negotiable: IgnoreQueryFilters appears nowhere in src/; another user's
   resource returns 404 and never 403; workers touch only shared tables; no
   default credential is seeded anywhere; no cross-tenant aggregate is ever
   displayed; never weaken safety/tenancy_policy.md to make a feature work.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: implemented / partial / missing, the U-01 sweep's route count and
result, every command with its actual result, and the one next step. Under-claim
rather than over-claim.
```

---

## Exit gate

Rows U-01…U-17 green — with U-02 proving the sweep can fail — existing pre-auth data
claimed by the first registered user with nothing lost, and a second account unable
to see the first's league through any route.
