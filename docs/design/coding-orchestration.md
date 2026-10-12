# Coding Orchestration

**Status:** Proposed — pending human approval. **Revision:** 9, 2026-10-11.
**Requirements basis:** the approved feature request for `coding-orchestration`,
the existing `.opencode/` agents, skills, and commands, and the
[architecture](../../ARCHITECTURE.md) and
[agent guidance](../../AGENTS.md).

## Executive Summary

PolyStore's feature-implementation workflow delegates review to a single
unified `review/code-reviewer` and caps review at ten cycles, contradicting
the shared five-repair-round budget in `orchestration-rules`. This design
replaces that agent with five domain-specialized reviewers and a new
`review/implementation-fidelity` final reviewer, mirroring the design
workflow's independent, sequential reviews, per-finding reconciliation, and
final gate. All six reviewers share a new `code-reviewer` skill, and the
rewritten `code/orchestrator` anchors each review to a recorded, committed
state and verifies the integrity of the design and workflow definitions.
The trade-off is workflow cost: up to six sequential reviews, the price of
admission for domain depth and an explicit design-fidelity guarantee.

## Problem and Scope

### Current state

The code workflow (`code/orchestrator`, invoked by `commands/feature.md`
with an approved design path and feature branch) implements approved designs
from `docs/design/`:

1. `code/implementer` implements the design and its ordinary unit and
   integration tests.
2. `code/test-adversary` independently derives adversarial scenarios and
   returns `PASS`, `FAIL`, or `BLOCKED`.
3. `review/code-reviewer` — a single unified reviewer covering correctness,
   design fidelity, and code quality in one pass — returns `APPROVED` or
   `CHANGES_REQUESTED`.

Two structural gaps:

- **Budget contradiction.** The orchestrator's own contract both obligates
  it to follow `orchestration-rules`, which caps the workflow at five repair
  rounds, and allows it to run "at most 10 review cycles". Two budgets
  cannot both be enforced against one workflow; the ten-cycle cap
  effectively overrides the shared budget.
- **No domain-specific review and no explicit fidelity gate.** One reviewer
  carries five review domains plus design fidelity in a single pass. The
  design workflow's review discipline — isolated initial reviews, per-finding
  reconciliation, re-review of changed revisions, approval bound to the
  reviewed revision, and a final gate run after the technical reviews — is
  entirely absent.

### Required behavior

- The main orchestration uses the `orchestration-rules` skill. (Already true;
  retained.)
- An agent is responsible for coding the feature from the approved design.
  (`code/implementer`, retained.)
- Each feature contains unit tests, integration tests, and performance tests
  where applicable. (The implementer owns unit and integration tests; the
  benchmark policy in [Implementer changes](#implementer-changes-codeimplementermd) covers
  performance tests.)
- Each implementation is vetted by specialized reviewers responsible for
  coding best practices, security, scalability, performance, and
  concurrency.
- A final review verifies that the implementation faithfully implements the
  feature as prescribed in the approved design.

### Resolved decisions

Recorded as authoritative inputs to this design, not re-opened here:

1. **Reviewer structure.** `review/code-reviewer` is removed entirely and
   replaced by five new domain reviewers plus a new final fidelity reviewer,
   mirroring the design workflow's five independent technical domain
   reviewers plus one final consistency reviewer.
2. **Domain clarification.** The requirement's fifth domain, "Currency," is
   interpreted as **Concurrency**: the reviewer checks thread safety of
   executing code and atomicity of transactions executed within PolyStore.
   This is a clarification of the requirement, not a new decision. No
   repository-freshness or "currency" reviewer domain is designed.
3. **Completion boundary.** The workflow ends after the final review and a
   completion report, followed by a human decision. Pull-request creation and
   merge are explicitly out of scope. The repository's incomplete `pr` skill
   stub is noted as a follow-up observation and is not designed here.
4. **Performance tests.** Benchmarks are required when the approved design
   establishes measurable performance or allocation requirements. Otherwise
   the implementer exercises discretion to add benchmarks when the feature
   touches a hot path. The existing `code/test-adversary` benchmark clauses
   are kept consistent with this policy.
5. **Repair budget.** The code workflow follows the shared `orchestration-rules`
   five-repair-round budget. The existing "Allow at most 10 review cycles"
   cap in `code/orchestrator.md` is removed to eliminate the contradiction.

### Non-goals

- No engine code changes: this feature's deliverables are agent definitions,
  skills, and commands under `.opencode/`.
- No change to `code/test-adversary` (its existing benchmark clauses already
  match decision 4) or to `commands/feature.md`.
- No pull-request or merge workflow; the `pr` skill stub is not designed or
  modified.

## Proposed Design

### Artifact inventory

| Artifact | Action | Role |
|---|---|---|
| `.opencode/skills/code-reviewer/SKILL.md` | New | Shared review protocol for all code reviewers |
| `.opencode/agents/review/code-best-practices.md` | New | Best-practices domain reviewer |
| `.opencode/agents/review/code-security.md` | New | Security domain reviewer |
| `.opencode/agents/review/code-scalability.md` | New | Scalability domain reviewer |
| `.opencode/agents/review/code-performance.md` | New | Performance domain reviewer |
| `.opencode/agents/review/code-concurrency.md` | New | Concurrency domain reviewer |
| `.opencode/agents/review/implementation-fidelity.md` | New | Final design-fidelity reviewer |
| `.opencode/agents/code/orchestrator.md` | Rewritten | Workflow phases, permissions, budget |
| `.opencode/agents/code/implementer.md` | Modified | Finding sources; benchmark policy |
| `.opencode/agents/code/test-adversary.md` | Unchanged | Adversarial validation |
| `.opencode/agents/review/code-reviewer.md` | Removed | Superseded by the six new reviewers |
| `.opencode/commands/feature.md` | Unchanged | Invokes `code/orchestrator` |

All agents use `mode: subagent` and `model: llama/qwen`. Reviewer agents
carry exactly one permission entry — the `code-reviewer` skill — and receive
no edit permission. Belt and braces: the global configuration denies the
file-edit tool by default, so reviewer file-tool edits are denied.
Shell-mediated access
remains available globally; the [risks](#risks) section states what the
permission model does and does not enforce. `code/implementer` keeps its
`edit: "*"` permission and `code/test-adversary` keeps its three
project-scoped edit permissions, both unchanged.

### Shared `code-reviewer` skill

New skill at `.opencode/skills/code-reviewer/SKILL.md`:

```yaml
---
name: code-reviewer
description: Shared review protocol for PolyStore feature implementations.
---
```

Its content mirrors `design-reviewer` with implementation-specific context
and rules:

- **Context.** Read the approved design in full; read `ARCHITECTURE.md` and
  `AGENTS.md`; inspect the complete feature diff (feature branch against base
  branch) and the relevant production source, ordinary tests, adversarial
  tests, and benchmarks. Established architecture takes precedence over the
  implementation. An intentional architectural change must be identified and
  justified. Report unresolved conflicts rather than silently choosing an
  interpretation.
- **Review rules.** Review only within the invoking agent's assigned domain.
  Findings must identify code in the feature diff or behavior the diff
  introduces; do not report pre-existing defects in unchanged code unless the
  change exposes or relies on them. A reviewer may build affected projects
  and run tests to verify suspected defects but must not modify source,
  tests, the approved design, or the branch. Distinguish defects introduced
  or amplified by the change from existing risks. Do not report stylistic
  preferences.
- **Severity.** **Blocking** — the implementation violates a requirement or
  invariant of the approved design, or a defect prevents correct behavior of
  the specified feature. **Major** — a material defect that must be corrected
  before approval. **Minor** — a localized improvement that does not prevent
  correct behavior. All findings require `CHANGES_REQUESTED`.
- **Findings.** For each finding: ID; severity; file and line; basis — the
  design requirement, invariant, or repository constraint violated; concrete
  defect description or failure scenario; consequence; resolution criterion.
  Report each distinct root cause once; combine related symptoms. A
  suspected defect that cannot be verified is reported as unverified, with
  the evidence available, rather than asserted as established — suspicion is
  a category, not a verdict.
- **Re-review.** Preserve finding IDs and mark each prior finding resolved or
  unresolved; check changed areas for new issues within the domain; do not
  repeat resolved findings in full; do not assume that addressing a finding
  preserved other guarantees.
- **Output.** Review summary (maximum 100 words); findings grouped by
  severity; required changes (finding IDs and concise resolution criteria)
  when requesting changes; a final disposition of exactly `APPROVED` or
  `CHANGES_REQUESTED`. Omit empty categories and do not restate the
  implementation.

Each reviewer agent file states its domain in a short prologue and instructs
the agent to load the skill and follow its context, finding, severity,
re-review, and output rules — the same pattern as the `review/design-*`
agents.

### Domain reviewers

Common frontmatter for all five (mirroring the old unified reviewer's
`variant: xhigh` and the design reviewers' skill-only permissions):

```yaml
description: Reviews PolyStore feature implementations for <domain>
mode: subagent
model: llama/qwen
variant: xhigh
permissions:
  - action: skill
    resource: "code-reviewer"
    effect: allow
```

The orchestrator supplies each reviewer the approved design path, the
feature branch, and the recorded state anchor. On re-review it additionally
supplies the reviewer's own previous finding IDs, a concise change summary
scoped to the diff and the reviewer's own findings and dispositions, and
the implementer's dispositions for those findings, each disposition
limited to that reviewer's own findings and the changes made — a
disposition may reference the changes but not other reviewers' findings or
dispositions. Initial reviews receive no findings from other reviewers.

| Reviewer | Review domain |
|---|---|
| `review/code-best-practices` | Semantic correctness of changed code — logic defects, incorrect or missing edge-case and error handling, and state-handling mistakes — insofar as they do not rise to a violation of an approved design requirement (such violations are fidelity findings); repository convention adherence (`AGENTS.md` style and test conventions); readability and maintainability; minimal correct abstractions; no speculative infrastructure or unrelated refactoring; test quality — tests demonstrate specified behavior, follow project naming and placement, and do not merely mirror implementation or encode behavior the design leaves unspecified. |
| `review/code-security` | Untrusted-input handling and injection; authorization and isolation across relation, tenant, transaction, and execution-context boundaries; identifier security; integrity of canonical state, indexes, and metadata; resource exhaustion; information disclosure; sensitive operations (unsafe or native code, deserialization, filesystem and network access); security-sensitive dependencies. |
| `review/code-scalability` | Growth behavior and resource bounds: unbounded memory or retained state, index-maintenance amplification, quadratic work growing with cardinality, coordination bottlenecks, and capacity limits as input size or concurrency grows. |
| `review/code-performance` | Per-operation and allocation cost: allocation, boxing, copying, hidden scans, buffering that undermines streaming, hot-path behavior, and BenchmarkDotNet results against the design's measurable requirements. A benchmark result is a failure only when the design establishes an explicit measurable requirement the implementation violates; otherwise report it as an observation. |
| `review/code-concurrency` | Thread safety of executing code and atomicity of transactions executed within PolyStore: shared mutable state, ownership, and synchronization; atomicity and partial-state visibility; memory ordering and safe publication; async interleaving, reentrancy, and state across await boundaries; cancellation, disposal, and cleanup races; lock ordering and forward progress. |

Domain boundaries mirror the design reviewers. `ARCHITECTURE.md`
conformance is owned by `review/implementation-fidelity` (its checklist
item); all reviewers share the skill's architecture-precedence and
conflict-reporting context, and a reviewer reports an architectural
conflict it encounters within its domain. Performance and scalability may
overlap on the same defect, as may concurrency and performance; a reviewer
reports an overlapping defect when it falls within its own domain and
identifies the affected domain so the orchestrator can route it. The
orchestrator reconciles each distinct finding once — overlap is expected;
double bookkeeping is not. A reviewer does not demand a specific mechanism
when another mechanism satisfies the required guarantee.

### Final fidelity reviewer

`review/implementation-fidelity` uses `mode: subagent`, `model: llama/qwen`,
`variant: xhigh`, and the `code-reviewer` skill permission.

It runs last, against the implementation state approved by all five domain
reviewers, mirroring the design workflow's final consistency review (design
Phase 5). Unlike the design consistency reviewer — a document-internal check
assigned `variant: medium` — fidelity review is a substantive comparison of
code against the approved design; a document checked against itself does not
need heavyweight scrutiny, but an implementation checked against its approved
design does, which is why it uses `xhigh`.

Its checks absorb the design-fidelity responsibilities of the removed
`review/code-reviewer`:

- each requirement and invariant of the approved design is observable in the
  implementation or demonstrated by tests;
- non-goals and scope boundaries are respected — no changes outside the
  approved scope;
- questions the design explicitly defers are not settled by the
  implementation;
- tests do not encode behavior the design leaves unspecified and do not
  merely mirror implementation details;
- implementation details have not been accidentally promoted into public
  contracts;
- performance behavior agrees with explicit design requirements (overlap
  with `code-performance` allowed);
- documentation and code agree after the change;
- the implementation introduces no architectural deviation not sanctioned
  by the approved design — `ARCHITECTURE.md` conformance, with intentional
  changes identified and justified and unresolved conflicts reported for
  escalation.

The fidelity reviewer distinguishes implementation defects from design
questions: it does not request an architectural redesign, does not invent
behavior for intentionally unresolved areas, and reports design questions to
the orchestrator for escalation.

The orchestrator supplies the fidelity reviewer the design path, the
feature branch, and the recorded state anchor. On a re-run after fidelity
findings, it additionally supplies the fidelity reviewer's own previous
finding IDs, a concise change summary, and the implementer's dispositions
for those findings, each scoped to the diff and the fidelity reviewer's
own findings and dispositions — a disposition may reference the changes
made but not other reviewers' findings or dispositions — mirroring the
Phase 5 re-review payload.

**Relationship to the domain reviewers.** If the fidelity reviewer returns
material (Blocking or Major) findings that require a correction, the
implementation changes; domain approvals are bound to the state reviewed,
so all five domain reviewers must re-approve the changed state before the
fidelity review runs again. A material finding closed without a change
leaves the domain approvals in place. Fidelity findings, including Minor
findings, go through the same reconciliation and shared repair budget as
domain findings.

### Code orchestrator (`code/orchestrator.md`, rewritten)

Frontmatter:

```yaml
description: Orchestrates implementation and review of approved PolyStore designs
mode: subagent
model: llama/qwen
variant: low
permissions:
  - action: subagent
    resource: "code/implementer"
    effect: allow
  - action: subagent
    resource: "code/test-adversary"
    effect: allow
  - action: subagent
    resource: "review/code-best-practices"
    effect: allow
  - action: subagent
    resource: "review/code-security"
    effect: allow
  - action: subagent
    resource: "review/code-scalability"
    effect: allow
  - action: subagent
    resource: "review/code-performance"
    effect: allow
  - action: subagent
    resource: "review/code-concurrency"
    effect: allow
  - action: subagent
    resource: "review/implementation-fidelity"
    effect: allow
  - action: skill
    resource: "orchestration-rules"
    effect: allow
```

The domain reviewer resources are listed explicitly rather than as a
`review/code-*` glob: the glob would also match the deferred old
`review/code-reviewer` during the bootstrap window, admitting an agent
outside the orchestrator's stated role. A glob is a pattern; the explicit
list is a policy. This departs from the
design-workflow orchestrator's `review/design-*` glob precedent, which is
acceptable because the minimality requirement is specific to this design.

The body keeps the current opening — the invoking command supplies the
approved design path and feature branch; the orchestrator reads the design,
`ARCHITECTURE.md`, and `AGENTS.md` completely, and follows
`orchestration-rules` — and replaces everything from the subagent list
onward with the phases below.

**Context and state.** The orchestrator determines and records the base
reference before any work: the merge base of the feature branch and `main`,
used for all reviewer diffs. If the merge base cannot be determined, stop
and report the configuration error. Recorded at start are: the feature
head SHA at start, which is the initial anchor's head; the content hash of
the design file; the content hashes of the existing `.opencode/` workflow
files (the files under `.opencode/agents/`, `.opencode/skills/`,
`.opencode/commands/`, and `opencode.json`); the pre-start working-tree
dirt set (path plus content hash, from `git status --porcelain`); and, if
the approved design designates `.opencode/` workflow files it changes as
deliverables, that designated set. The design and definition hashes are
verified before each review and at completion; a mismatch is an escalation
rather than silent continuation.

The orchestrator's invocation instructions to the implementer (Phase 1 and
each repair round) and to the adversary (Phase 2 and each re-validation)
require committing all of their changes on the feature branch before
returning and reporting the head SHA of the last commit made (or the
unchanged head if nothing was committed). When the orchestrator
re-invokes the named agent to commit the dirt, that re-invocation
likewise requires the agent to commit the dirt on the feature branch and
to report the head SHA of its commit (or the unchanged head if the tree
was already clean on re-invocation).

The reviewed state is the tuple: the feature head SHA, the recorded base
reference, a clean working tree (no uncommitted or untracked changes other
than ignored files, the approved design file — whose content is anchored by
the recorded hash whether or not it is tracked — and files identical to
the pre-start dirt set, excluded only while unchanged from that baseline),
and the verified design content hash. At each phase boundary — after the
implementer or adversary returns, before each review — the orchestrator
verifies the clean tree, records the current feature head, and classifies
the boundary per the state-mismatch machinery below, using the head
recorded at start as the initial anchor for the first boundary. A review
is anchored to the state recorded before it begins. If the working tree is dirty
(excluding ignored files, the approved design file, and files unchanged
from the pre-start dirt set), the state is unrecordable: the orchestrator
reports the dirt — uncommitted and untracked contents — with the name of
the agent whose invocation just returned, without presuming a cause. When
the named agent is an implementing agent (the implementer or the
adversary), the orchestrator re-invokes it to commit the dirt, with the
commit-and-report requirement above; if the tree is still dirty after
that re-invocation, the orchestrator stops and reports. When the named
agent is a read-only reviewer, the orchestrator does not re-invoke it to
commit: the recovery is stop-and-report, and the human or another
human-directed action cleans the tree, after which the state is
re-recorded.
Phase-boundary checks are tight (after every agent returns), so dirt present
at a boundary has exactly two suspects: the named agent's invocation, or the
environment.

State-mismatch events are classified at the detection points: the phase
boundary comparison above, a post-review re-verification after each review
completes, and the Phase 7 pre-decision re-verification. At a boundary
following a committing agent's return (the implementer, the adversary, or
the named agent's dirt-commit re-invocation), the orchestrator compares
the current head to the head SHA the agent reported for its last commit.
Equality is expected only when the reported SHA equals or descends from the
previously recorded anchor head; at a dirt-commit re-invocation boundary
that is the anchor in effect before the dirty boundary, which recorded no
new anchor because the state was unrecordable. The normal
commit-before-return flow (Phase 1, Phase 2, each repair round, each
re-validation, and a dirt-commit re-invocation) re-anchors the state
contract to the current head with no event, no re-run, and no bound
consumption — so a normal run produces zero state-mismatch events; the
machinery is designed to be boring. Any other outcome is a state-mismatch
event: a head differing from the reported SHA (an external or mixed
commit, or an agent that did not commit as instructed), or a reported head
that is a rewind or otherwise non-descendant of the previously anchored
head. At a boundary following a read-only reviewer's return, the head must
equal the recorded anchor — any difference is an event. At the post-review
and pre-decision re-verifications, a head differing from the head recorded
before the review began or reported in the Phase 7 state anchor (an
advance or a rewind) is an event.

Every state-mismatch event invalidates approvals anchored to the prior
state and requires a state-driven re-review of every prior review whose
approval no longer covers the current state; for a post-review event this
includes the review whose anchor just failed, and for a boundary event the
upcoming review runs against the newly recorded anchor. A state-driven
re-review is not a findings-driven re-review and consumes no repair round.

State-mismatch events are bounded twice. A consecutive-mismatch counter
increments for each event and resets only when an entire review cycle —
boundary comparison, review, and post-review re-verification — completes
without an event; two consecutive events, including two within one review
cycle, escalate to the human as a workflow violation. A lifetime
state-mismatch count, which never resets during the workflow, allows at
most three events in total; a fourth event escalates to the human as a
workflow violation.

Approvals apply only to the state they are anchored to. An unexpected
state change — a state-mismatch event, a dirty tree at a phase boundary,
or a design or definition hash mismatch — invalidates approvals anchored
to the prior state and requires re-review of the affected state. A
re-anchor to a committing agent's reported head is not a state change:
upcoming reviews anchor to the new state, and existing approvals of the
changed state are re-obtained through the findings-driven re-review flow
(Phase 5), not invalidated by the re-anchor.

If the approved design designates `.opencode/` workflow files it changes
as deliverables — as this workflow's own implementation does — the
definition-integrity anchor is re-anchored at each phase boundary before a
review: the set check is always evaluated against the initial start-time
anchor — within the anchored scope (`.opencode/agents/`,
`.opencode/skills/`, `.opencode/commands/`, `opencode.json`), the set of
files whose current existence or content differs from start must equal
the design-designated set exactly (any other change escalates) — and
re-anchoring then refreshes only the content hashes of the designated
files. The Phase 7 report lists the re-anchored files and their final
hashes. After any re-anchoring, the
anchor no longer detects redefinition of the workflow by the feature
itself; detection is the exact-set match plus human visibility of the
re-anchored files at Phase 7. For features that do not designate
workflow-definition deliverables, the anchor is never re-anchored, and a
definition-hash mismatch remains an escalation.

The orchestrator also tracks repair rounds used under
`orchestration-rules`, per-reviewer dispositions and the state they apply
to, and unresolved findings.

The state contract, the Gate, and the detection claims in the
Procedural-guarantees risk item apply to runs that execute the new
orchestrator definition. The run that implements this feature (the
bootstrap run) executes the old orchestrator body and is governed per the
bootstrap-governance note below.

**Phase 1 — Implementation.** Invoke `code/implementer` with the design
path, the branch, and instructions to implement the design and its ordinary
test suite, including the benchmark policy from the implementer's contract,
and to commit all changes on the feature branch before returning.

**Phase 2 — Adversarial validation.** Invoke `code/test-adversary` with the
design path, the branch, and instructions to commit all test and benchmark
changes on the feature branch before returning. On `FAIL`, begin a repair
round: the implementer corrects, the adversary re-validates. On `BLOCKED`, escalate as a specification ambiguity. When the
adversary reports all required tests pass, proceed to Phase 3. If the
implementer disputes that an adversarial failure asserts behavior the design
does not require, the orchestrator does not instruct a production change
merely to satisfy the test; the disagreement is treated under the
escalation rules.

**Phase 3 — Independent domain review.** Submit the same unchanged
implementation state, anchored per the state contract, sequentially to:

1. `review/code-best-practices`
2. `review/code-security`
3. `review/code-scalability`
4. `review/code-performance`
5. `review/code-concurrency`

Do not give one reviewer another's findings during initial review —
independence is what makes the isolation worth it. Wait for all five before
permitting implementation changes. Record each disposition
and the state anchor it applies to.

**Phase 4 — Reconciliation.** If any reviewer returns `CHANGES_REQUESTED`,
collect the findings and require a disposition for every finding:

- addressed by an implementation change;
- resolved by clarification;
- rejected with supporting reasoning; or
- unresolved, with an explanation.

Keep dispositions in the orchestrator's handoff, not in the code. Do not
require changes solely to eliminate optional Minor findings.

A repair round begins when the implementer is directed to make changes in
response to collected findings; the implementer commits the resulting
changes on the feature branch before returning. Findings that are only
clarified or rejected without a correction attempt — including a review that
returned only Minor findings — do not consume a round. A repair attempt is
counted even if it produces no changes.

The implementer may dispute a finding but cannot approve its own rejection: a
disputed Blocking or Major finding returns to the originating reviewer with
the implementer's supporting reasoning. Disagreement earns a hearing, not a
veto. A dispute that does not direct a
correction attempt is a routine clarification and does not consume a round.
If the reviewer maintains the finding, the orchestrator presents both
positions to the human. A material finding left "unresolved, with an
explanation" is likewise escalated to the human without entering the
dispute flow. The human's decision on an escalated material finding closes
it or resumes the workflow, per the Gate's closure rule: the finding is
closed when the human adjudicates it invalid or supplies the clarifying
resolution that settles it (recorded in the Phase 7 report with the other
decisions); a decision that directs a correction begins a repair round
within the shared budget; a decision that identifies a design ambiguity or
contradiction the approved design does not resolve is a stop-and-report,
since design questions are outside this workflow. A material finding held
for a human decision (a maintained dispute or an "unresolved" escalation)
blocks the start of new repair rounds while pending. If a directed
correction arrives when no round remains, the workflow stops per budget
exhaustion and records the pending correction in the stop report, for
pickup by a new workflow invocation with a fresh budget. The orchestrator
does not adjudicate architectural decisions.

**Phase 5 — Re-review.** If reconciliation changes the implementation,
first re-invoke `code/test-adversary` — supplying the design path, the
branch, and at most the adversary's own prior scenarios; other agents'
findings and implementer dispositions are not supplied, preserving its
independent derivation — then submit the changed state to all five domain
reviewers sequentially. Each reviewer receives the current state, its own
previous finding IDs, a concise change summary, and the implementer's
dispositions for its findings; the change summary and each disposition are
scoped to the diff and that reviewer's own findings and dispositions — a
disposition may reference the changes made but not other reviewers'
findings or dispositions — so neither identifies another reviewer's
findings. Unrelated findings from other reviewers are not shared. Reviewers that previously approved must approve
the changed state again. If the implementation is unchanged and only a
finding is disputed, return the supporting reasoning to the originating
reviewer; existing approvals of the unchanged state remain valid. If a
re-validation fails, the revision is not complete: the failure is part of
the same repair round, and a new correction attempt begins a new round.
Re-review and re-validation within a repair round do not themselves consume
additional rounds.

**Gate.** The Phase 5 gate is satisfied when every material (Blocking or
Major) finding across the five domain reviewers is closed. A finding is
closed when it is addressed by a change, resolved by clarification, or
closed by a human decision on an escalated material finding (from the
dispute flow or an "unresolved" escalation) that adjudicates it invalid or
supplies the clarifying resolution that settles it; such decisions are
recorded in the Phase 7 report. Open Minor findings do not block the gate
and are reported in Phase 7. Because the shared skill requires
`CHANGES_REQUESTED` for any finding, a `CHANGES_REQUESTED` return whose
findings were all closed without an implementation change does not block
the gate; the gate is defined by findings, not by return words. The same
gate rule applies to the Phase 6 and Phase 7 gates.

**Phase 6 — Fidelity review.** Invoke `review/implementation-fidelity`
with the design path, the feature branch, and the recorded state anchor,
on the state that satisfies the Phase 5 gate. On a re-run after fidelity
findings, supply the Phase 5 re-review payload: the fidelity reviewer's
own previous finding IDs, a concise change summary, and the implementer's
dispositions for those findings. Material (Blocking or Major) findings are
reconciled per Phase 4: a correction begins a repair round — the
implementer corrects, the adversary re-validates, all five domain
reviewers re-approve the changed state, and the fidelity review runs
again — while a material finding closed without a change (clarification or
a human decision) leaves the state and the domain approvals in place.
Minor findings are reported and dispositioned without forcing changes and
do not block the gate per the gate rule; if a Minor finding is addressed
by a change, the changed state requires the same re-approval path as
material findings.

**Phase 7 — Completion report and human decision.** When the Phase 5 gate
is satisfied and every material fidelity finding is closed per the gate
rule, present to the human:

- implementation summary and production files changed;
- ordinary, adversarial, and benchmark tests added;
- repair rounds used;
- reviewer dispositions, per finding and per reviewer;
- human decisions on escalated material findings, if any;
- re-anchored workflow-definition files and their final hashes, if the
  design designated deliverables;
- remaining open Minor findings;
- the final state anchor: feature head SHA, base reference, and verified
  design and workflow-definition hashes;
- unresolved items, if any.

The human's decision applies to the reported state. The orchestrator
re-verifies the feature head before presenting the decision; a difference
from the reported head is a state-mismatch event — counted and bounded per
the state-mismatch machinery — and anchored approvals are invalidated and
re-review of the current state is required before the decision proceeds.
The human decides what happens next. The workflow does not create or
merge pull requests.

**Budget.** The workflow follows the shared `orchestration-rules` budget: at
most five repair rounds; the initial implementation, first validation, and
first reviews consume no rounds; findings reconciled together consume one
round regardless of how many agents reported them; the budget is shared
across all phases, including fidelity, and never resets. The current "Allow
at most 10 review cycles" cap is removed. Mandated commit-before-return
advances are re-anchors, not events. State-driven re-reviews
(state-mismatch events) do not consume repair rounds; they are bounded by
the state-mismatch bounds in the state contract.

**Escalation.** Stop and request human review rather than inventing behavior
when:

- the approved design conflicts with `ARCHITECTURE.md`;
- implementation requires resolving a question explicitly deferred by the
  design;
- a reviewer requests an architectural change rather than correction of an
  implementation defect — the implementer does not make the architectural
  change, the approved design is not modified during feature development,
  and the orchestrator presents the request to the human;
- the implementer and adversary disagree about behavior the specification
  does not clearly determine, or validation is `BLOCKED` on a
  specification ambiguity;
- review findings oscillate rather than converge;
- the fifth repair round completes without approval — the stop report
  includes any pending directed corrections, for pickup by a new workflow
  invocation with a fresh budget;
- a human decision on an escalated material finding identifies a design
  ambiguity or contradiction the approved design does not resolve;
- a design or workflow-definition integrity anchor mismatches, including a
  re-anchoring whose changed-file set (files whose current existence or
  content differs from the start-time anchor) does not equal the
  design-designated set exactly — a file outside the designated set that
  differs from start, or a designated file reverted to its start state;
- a state-mismatch bound is reached (two consecutive state-mismatch
  events, or a fourth state-mismatch event);
- the working tree is dirty at a phase boundary whose named agent is a
  read-only reviewer, or still dirty after the named agent's
  re-invocation;
- the merge base cannot be determined (configuration error).

### Implementer changes (`code/implementer.md`)

Two minimal edits; permissions and all other sections are unchanged:

1. **Finding sources.** The "Responding to Findings" section currently reads
   "You may receive findings from `code/test-adversary` or
   `review/code-reviewer`." Replace only the `review/code-reviewer`
   reference with the five domain reviewers and
   `review/implementation-fidelity`; `code/test-adversary` remains a
   finding source alongside them. The existing behavior contract — determine
   whether a finding demonstrates a violation of the approved design,
   correct valid defects, dispute design questions without changing
   production merely to silence a reviewer, and escalate
   architectural-change requests — applies to all finding sources.
2. **Benchmark policy.** Add to the tests section: benchmarks are required
   when the approved design establishes measurable performance or allocation
   requirements; otherwise the implementer exercises discretion to add
   benchmarks when the feature touches a hot path.

### Adversary: unchanged

`code/test-adversary` already modifies benchmark projects only when the
design contains testable performance or allocation requirements, runs
relevant benchmarks when the design establishes measurable requirements, and
treats a benchmark result as a failure only when the design establishes an
explicit measurable requirement the implementation violates. That is exactly
decision 4, so the agent's benchmark behavior requires no edit; it remains
the validator of benchmark results against the design's explicit
requirements. The commit-before-return obligation for the adversary's
changes is carried by the orchestrator's Phase 2 and re-validation
instructions, not by an edit to this file.

### Removal and reference updates

- Remove `.opencode/agents/review/code-reviewer.md`, deferred until after
  the bootstrap review completes (bootstrap-governance note). Its
  design-fidelity and architectural-violation checks move to
  `review/implementation-fidelity` (the checklist's `ARCHITECTURE.md`
  conformance item); its code-level semantic correctness, quality, and
  test-quality checks move to `review/code-best-practices` (semantic
  correctness and test quality) and the domain reviewers (security,
  scalability, performance, concurrency).
- `code/orchestrator.md`: the `review/code-reviewer` permission and all
  body references are replaced as shown above.
- `code/implementer.md`: the finding-source reference is updated as shown
  above.
- No other references exist: `commands/feature.md` invokes
  `code/orchestrator` by name, and a repository-wide search confirms no
  other file references `review/code-reviewer`.

### Bootstrap governance

The run that implements this feature executes the old `code/orchestrator`
body (loaded at invocation): it performs no design or definition hash
verification, no clean-tree predicate, no head anchors, and no
re-anchoring, and its review step invokes `review/code-reviewer`, which
the implementation removes. The bootstrap run is therefore governed
separately from the state contract:

- At the start of the bootstrap run, the human records the pre-run state
  of the feature branch (the head SHA) and the content hash of the
  approved design file, and persists a pre-run content copy of the design
  file at a human-chosen path outside the anchored scope
  (`.opencode/agents/`, `.opencode/skills/`, `.opencode/commands/`,
  `opencode.json`) and, if practical, outside the repository working
  tree. The copy is the pinned reference the six reviewers use, and the
  hash is its integrity check; the copy's integrity rests on the explicit
  verification steps below, not on the exact-set check, which therefore
  needs no special-casing of the copy. After the implementer and
  adversary phases commit their work and immediately before the bootstrap
  reviewers consume the copy, the human verifies the copy's current
  content hash against the recorded pre-run hash — a mismatch is an
  escalation (the copy is compromised): stop and report. The human (or a
  human-directed manual invocation) then runs the six new reviewers — the
  five domain reviewers and `review/implementation-fidelity` — against
  the implemented state, using the persisted pre-run copy as their
  reference rather than the working-tree copy, so that a mid-bootstrap
  modification of the design file is detected by the post-removal hash
  verification rather than silently becoming the reviewers' reference.
- The re-anchor procedure does not apply to the bootstrap review: the
  bootstrap run records no start-time anchor to re-anchor against, and at
  bootstrap review time the deferred removal means the changed-file set is
  a strict subset of the design-designated set, which the exact-set check
  would fail. The bootstrap run's integrity verification is therefore
  human inspection of the implemented workflow definitions.
- The removal of `review/code-reviewer.md` is deferred until after the
  bootstrap review completes, so the old flow's review step remains
  functional mid-run; the Implementation Outline ordering reflects this.
  After the deferred removal completes, the human verifies the approved
  design file's integrity against the content hash recorded at the start
  of the run (the design-content-hash anchor, adapted to the
  human-governed bootstrap), and verifies the full-designated-set
  exact-set check against the recorded pre-run state: the set of files
  within the anchored scope whose existence or content differs from the
  pre-run state must equal the design-designated set exactly, including
  the removed `review/code-reviewer.md`.

### Out of scope: `pr` skill stub

`.opencode/skills/pr/SKILL.md` is an incomplete stub: malformed `--`
frontmatter, a bare heading, and a numbered list truncated at an empty item
3 — a cliffhanger for a future feature to resolve. It is recorded as a
follow-up observation and is neither designed nor modified by this feature.

## Alternatives

1. **Five domain reviewers plus final fidelity reviewer, shared skill
   (recommended).** Mirrors the design workflow's proven structure: isolated
   sequential domain reviews, per-finding reconciliation, re-review of
   changed states, and a final gate. The shared `code-reviewer` skill keeps
   the protocol in one place, matching the `design-reviewer` pattern.
2. **Protocol duplicated per reviewer.** Each of the six reviewers embeds the
   full review protocol in its own file, with no shared skill. This avoids a
   skill dependency, but six copies of one protocol is six places to keep in
   sync; it invites drift between reviewers and departs from the established
   shared-skill pattern without benefit.
3. **Parallel fidelity review.** Run the fidelity reviewer as a sixth
   independent initial review alongside the five domain reviewers. It would
   surface fidelity gaps earlier, but the fidelity reviewer's contract is
   defined against the state approved by all five; reviewing an
   unapproved state wastes work whenever domain findings change the
   implementation, and it departs from the design workflow's final-gate
   semantics that resolved decision 1 mirrors.

## Risks

- **Workflow cost.** Up to six sequential initial reviews per state, plus
  re-reviews after each repair round. Mitigations: the shared five-round
  budget bounds total work; the unchanged-state short-circuit means
  disputed-only findings do not trigger full re-reviews; findings are
  reconciled in a single batch (one round). Mandated commit-before-return
  advances are re-anchors, not events, so a normal run produces zero
  state-mismatch events. State-driven re-reviews from unexpected state
  changes — post-review mismatch re-runs and boundary-triggered
  re-reviews — do not consume repair rounds; the consecutive- and
  lifetime-mismatch bounds in the state contract bound them instead. The
  cost is workflow time, not repository complexity.
- **Domain overlap.** Performance/scalability and
  concurrency/performance boundaries are judgment calls; overlapping
  findings are expected, are reconciled once, and each reviewer identifies
  the affected domain so routing is unambiguous.
- **Approval invalidation.** Anchoring approvals to the recorded state
  (committed feature head, fixed base reference, clean tree, verified design
  hash) makes "an approval applies only to the state reviewed" concrete.
  The cost is full domain re-approval when a late fidelity finding forces an
  implementation change, and re-review when the state changes; this mirrors
  design Phase 5 and is accepted.
- **Procedural guarantees.** The global configuration denies the file-edit
  tool by default but allows `bash` and `external_directory` for every
  agent. Reviewer agents hold no edit permission, so file-tool edits are
  denied; shell-mediated modification, including git, remains available. The
  implementer's `edit: "*"` is carried forward unchanged and reaches the
  approved design file and the `.opencode/` workflow definitions. The
  workflow's guarantees — reviewers read-only, the approved design immutable
  mid-workflow, workflow definitions immutable mid-workflow, branch state
  mutated only by the implementer and the adversary — are therefore
  procedural: they rest on agent contracts (the skill's prohibition, the
  commit-before-return instructions) and sequential invocation, not on the
  permission model. The dirty-tree recovery path upholds the
  reviewers-read-only guarantee: a dirty boundary whose named agent is a
  read-only reviewer stops and reports rather than directing the reviewer
  to commit. Detection is at verification points, per class: the
  design content hash covers specification integrity; the `.opencode/`
  definition hashes cover workflow-definition integrity; the
  commit-before-return instructions and the reported-head SHA, the
  phase-boundary clean-tree check and head classification, and the
  post-review head re-verification cover the branch state. None
  prevents a deviating or compromised subagent from modifying state and
  restoring it between verification points, and none protects the
  orchestrator itself; the anchors detect most violations after the fact —
  an audit trail, not a lock.
  These detection claims apply to runs executing the new orchestrator
  definition. The bootstrap run (which implements this feature) performs
  no anchor verification; its detection mechanism is human verification
  of the implemented workflow definitions, of the persisted pre-run
  design copy's integrity against the content hash recorded at the start
  of the run (immediately before the bootstrap reviewers consume the
  copy), of the approved design file's integrity against that hash after
  the deferred removal, and of the full-designated-set exact-set check,
  per the bootstrap-governance note.
- **Reviewer verification.** A reviewer may build affected projects and run
  tests (file editing remains denied by the permission model) to verify
  suspected defects; unverified suspected defects are reported per the
  skill's findings rule.
- **Workflow-definition deliverables.** A feature whose approved design
  designates `.opencode/` workflow files it changes as deliverables — this
  workflow's own implementation is one — has its definition-integrity
  anchor re-anchored at each pre-review boundary by exact-set match of the
  start-time changed-file set against the designated set, with the
  re-anchored files visible in the Phase 7 report. For such features the
  anchor no longer detects redefinition of the workflow by the feature
  itself; detection is the exact-set match plus human visibility,
  consistent with the procedural-guarantee framing. For features that
  designate no deliverables, the anchor is never re-anchored and a
  definition-hash mismatch deterministically escalates rather than
  continuing.

## Implementation Outline

1. Create the `code-reviewer` skill — the foundation all reviewers load.
2. Create the five domain reviewers and `review/implementation-fidelity`;
   each depends only on the skill.
3. Rewrite `code/orchestrator.md` (its permission list depends on the
   reviewers existing) and apply the two `code/implementer.md` edits;
   leave `review/code-reviewer.md` in place (its removal is deferred per
   the bootstrap-governance note).
4. At the start of the bootstrap run, record the pre-run branch head SHA,
   the approved design file's content hash, and a pre-run content copy of
   the design file persisted at a human-chosen path outside the anchored
   scope and, if practical, outside the repository working tree.
   Complete the implementer and adversary phases, verify the persisted
   copy's current content hash against the recorded pre-run hash (a
   mismatch is an escalation: stop and report), run the human-directed
   bootstrap review (the six new reviewers against the implemented state,/
   referencing the persisted pre-run copy), then remove
   `review/code-reviewer.md`, verify no remaining references to the
   agent path, verify the approved design file's integrity against the
   recorded hash, and verify the full-designated-set exact-set check
   against the recorded pre-run state (bootstrap-governance note).
5. The implementer verifies: YAML frontmatter parses for every changed or new agent;
   permission resource lists match existing agent and skill paths;
   `commands/feature.md` and `code/test-adversary.md` are unchanged.

No source code, test, or benchmark project is modified. `AGENTS.md` and
`ARCHITECTURE.md` are not affected; no architectural decision is changed.
