---
name: ElwakeelBuild
description: Execute exactly one human-approved Medilog Cycle using the four-command workflow plan, build, review, done. Use when a contributor asks to implement, review, or close a Cycle or Task that already has an approved task file. Follows AGENTS.md strictly, produces focused diffs, runs the real checks, and prepares the Execution Report and the STATE.md update.
---

# ElwakeelBuild — Cycle Execution Skill

**Repo location:** `.ai/skills/ElwakeelBuild.md`
**Companion files:** `AGENTS.md` (rules), `STATE.md` (status), `ElwakeelManage.md` (planning), `docs/tasks/<Feature>/<CycleId>.md` (the approved Cycle).

## 0. Authority and Limits

- `AGENTS.md` is the highest authority; if this skill disagrees, `AGENTS.md` wins and the disagreement is reported.
- Work on **one Cycle at a time**, and only a Cycle whose status is `Approved` in its task file and in `STATE.md`.
- Never invent a requirement, change an architecture decision, add a NuGet/npm package for shared architecture, or implement outside the Cycle.
- The human contributor owns every line. AI output is a proposal.
- Tool-agnostic: identical behavior in any AI assistant.

## 1. Start-of-Session Checklist (before any command)

Read, in this order, and only these first:
1. `AGENTS.md`
2. `docs/PROJECT_OVERVIEW.md` (if it does not exist: say so, ask the Tech Lead to create it, continue with the rest)
3. `STATE.md`
4. The task file for the Cycle
5. Only the code files that the Cycle touches or that define patterns it must follow

Then confirm back to the contributor, in 5 lines or fewer: Cycle ID, goal, owner, dependencies satisfied (yes/no), contracts agreed (yes/no), open questions (list or none).

**Stop and report instead of proceeding if** any of these is true: the task file is missing or not `Approved`; a dependency Cycle is not `Done`; a contract/decision the Cycle needs is not recorded; the request is outside F1–F5/FS; the requirement is ambiguous or conflicts with another source.

## 2. The Four Commands

Commands are gates. One command never authorizes the next one. The contributor says the command explicitly.

### 2.1 `plan`
Goal: a reviewable implementation plan. **No implementation code.**

Do:
1. Restate the Cycle goal and acceptance criteria from the task file.
2. List the applicable BR / FR / BRULE / NFR and security constraints.
3. Inspect the relevant existing code and name the patterns to preserve (service/handler style, controller vs. endpoint style, repository/DbContext usage, result/error convention, frontend API layer, i18n usage). If a pattern is undecided in `AGENTS.md` §12, do not choose; ask.
4. List files to create and modify, per layer.
5. List dependencies and shared pieces touched (pagination DTO, validation registration, mapping profile, entity configuration, error shape).
6. List the test plan with the expected behavior derived from requirements, not from the planned code.
7. List risks, assumptions, and questions.

End with: **Awaiting approval of this plan.** Do not start `build`.

### 2.2 `build`
Precondition: the contributor approved the plan in this session.

Do:
1. Implement **only** the approved plan. If something extra seems necessary, stop and ask.
2. Prefer small focused diffs over rewriting files. Inspect before editing. Never overwrite uncommitted work.
3. Follow the Implementation Rules in §3.
4. Write tests together with the code (§4).
5. Run the checks that exist in the repo (§5) and report the real output.
6. Present the changes as a focused diff, grouped by layer, with a one-line reason per file.

Never claim something works unless it was executed and the result was observed.

### 2.3 `review`
Review the **actual diff** (not the plan) against: acceptance criteria, `AGENTS.md`, requirement sources, architecture layering, security/data scope, validation, tests, localization, and unrelated changes.

Report findings as a table:

| # | Severity (Blocker / Major / Minor / Nit) | File:line | Finding | Requirement/rule | Suggested fix |
|---|---|---|---|---|---|

Rules: no silent fixes; no unrelated refactors; if there are no findings in a category, say what was checked. Also state which acceptance criteria are met / not met / not verifiable.

### 2.4 `done`
Preconditions: acceptance criteria met, checks run, review findings resolved or accepted.

Produce, in this order:
1. **Execution Report** (template in §6)
2. **Proposed `STATE.md` update** as a diff (status, completed work, test results, blockers, decisions, next step). Do not change scope, ownership, or acceptance criteria through this update.
3. **PR description** draft (AGENTS.md §9 list)
4. A reminder that the contributor must be able to explain every committed line.

Do not claim done if any check failed, any acceptance criterion is unmet, or any required translation/contract/migration is missing.

## 3. Implementation Rules (summary of AGENTS.md — the file itself is authoritative)

**Backend**
- Respect layers: Domain has no ASP.NET/EF/HTTP dependency; Application orchestrates and does not use the concrete DbContext; Infrastructure holds EF Core, configurations, migrations; Api is thin (no business rules, no queries).
- Use the existing service/handler pattern. Do **not** introduce MediatR, CQRS, repositories, or a Result library unless the team approved it (check `AGENTS.md` §12 / `STATE.md` decisions log).
- Every persisted entity has its own `IEntityTypeConfiguration<T>` in Infrastructure, applied centrally; define keys, relationships, indexes, precision, delete behavior (no accidental cascade over medical/financial history).
- FluentValidation validator for every request/command in Application; business invariants still enforced in the domain/application flow.
- AutoMapper profiles for DTO mapping; never map client input onto a tracked entity in a way that overwrites IDs, ownership, roles, payment state, audit or protected medical fields; never bypass domain methods.
- Filtering and pagination: use the agreed shared contract only; filter in the data source; validate page/pageSize/filters; enforce the approved max page size; deterministic ordering with a stable tie-breaker; return pagination metadata.
- Async everywhere with `Async` suffix; pass `CancellationToken`; no `async void`, `.Wait()`, `.Result`; no swallowed exceptions; do not return EF entities or internal error details.
- Security: derive the acting user and role from the authenticated server-side identity, never from client-supplied IDs; scope every doctor query to the authenticated doctor; secretaries never touch medical data; enforce checks in the application flow and the query, not just in the UI.
- Do not log passwords, tokens, payment secrets, or medical content. Synthetic data only.
- Migrations: created from the agreed integration state, reviewed, never rewrite shared migrations.

**Frontend**
- Keep the existing structure (`app`, `components`, `constants`, `data`, `features`, `i18n`, `messages`, `types`); no `src/`.
- Pages are thin; logic lives in `features/<name>/`; cross-feature imports only through the feature's `index.ts`; shared UI never imports features.
- Strict TypeScript, no `any`; handle loading, empty, success, and error states; accessible labels and keyboard use.
- Reuse the existing API/data layer and installed libraries. Check `package.json` first. Do not add Axios/TanStack Query/Zustand/Zod/toast libraries without team approval.
- All user-facing text through `next-intl`; add matching keys to **both** `messages/ar.json` and `messages/en.json`; keep RTL/LTR working (prefer logical Tailwind utilities).
- Frontend validation and route guards are convenience only; the backend is the enforcement point.

## 4. Test Requirements

- Follow the test framework already in the repo; do not add a second one.
- Arrange–Act–Assert. Expected values come from approved requirements/rules, not from the implementation.
- Cover valid and invalid cases, and validators.
- **F1 appointment tests:** end after start; no overlap; exact adjacency allowed; partial overlap rejected; full containment rejected; identical interval rejected; different doctors and different dates allowed; reschedule excludes the appointment itself (when that rule is approved).
- **Security tests:** secretary calling a medical endpoint directly is rejected; a doctor cannot read another doctor's restricted data; a secretary cannot access unassigned doctors' data (when the assignment rule is approved).
- **List endpoints:** invalid page values, max page size, stable ordering, correct total count, filter correctness.
- **Payments / dashboard:** calculations and aggregations only after the payment rules are approved.
- Bug fix = regression test first when practical (shown failing, then passing).

## 5. Quality Checks (run only what exists; report actual results)

```bash
cd Back_End
dotnet restore
dotnet build Medilog.slnx
dotnet test Medilog.slnx

cd Front_End
npm run lint
npm run build
```

If a check cannot run (missing script, no test project, environment issue), say exactly which and why. Never write "tests pass" without having seen them pass.

## 6. Execution Report Template (one per Cycle)

```markdown
# Execution Report — <CycleId>
- Date / Contributor / AI tool used:
- Branch:
- Goal:
## Delivered
<what behavior now exists>
## Files changed
| Layer | File | Change |
|---|---|---|
## Commands run and results
| Command | Result (pass/fail/not run) | Notes |
|---|---|---|
## Acceptance criteria
| AC | Met / Not met / Not verifiable | Evidence |
|---|---|---|
## Review findings
<resolved / accepted / open>
## Deviations from plan
<none or list with reason and who approved>
## Contracts / migrations / translations / config changed
<list or none>
## Decisions made or needed
<list, with approver>
## Remaining risks and blockers
## Proposed STATE.md update
<diff>
```

## 7. Stop Conditions (always ask, never guess)

Stop immediately and ask the contributor/Tech Lead when:
- a required domain decision is missing (payments, patient access relationship, dashboard revenue attribution, shared pagination contract, etc.);
- two sources conflict;
- the change would alter an agreed API contract another member depends on;
- the work needs a new shared dependency or architecture pattern;
- the diff is growing beyond the Cycle;
- a check fails and the cause is outside the Cycle.

State: what is missing, what it affects, optional alternatives, who must decide. Then wait.
