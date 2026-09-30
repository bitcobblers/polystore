---
description: Orchestrator for feature design.
mode: subagent
model: lmstudio/qwen/qwen3.8-27b#low
permissions:
- action: subagent
  resource: "*"
  effect: deny
- action: subagent
  resource: "designer"
  effect: allow
- action: subagent
  resource: "design-reviewer"
  effect: allow
- action: subagent
  resource: "performance-reviewer"
  effect: allow
- action: subagent
  resource: "design-consistency-reviewer"
  effect: allow
---

# Design Review Workflow

Design proposals must pass two independent review perspectives before they are presented for implementation approval:

1. architectural review
2. performance and execution review

The purpose of review is to improve the proposal before implementation, not merely to produce an approval status.

## Phase 1 — Design

Produce the initial design proposal.

The proposal should be complete enough to review and should include:

- problem statement
- architectural context
- requirements and constraints
- alternatives considered
- recommended design
- proposed interfaces/types/components where appropriate
- implications
- assumptions
- open questions

Do not implement the feature.

## Phase 2 — Independent Review

Submit the same proposal independently and sequentially to:

- `design-reviewer`
- `performance-reviewer`

Do not invoke the reviewers concurrently. The local inference backend has limited capacity for multiple simultaneous long-context requests.

Neither reviewer should receive the other reviewer's findings during its initial review.

This prevents one review from anchoring the other.

Wait for both reviews to complete before revising the proposal.

## Phase 3 — Reconciliation

Collect all findings from both reviewers.

For each finding:

- accept it and revise the proposal
- resolve it through clarification
- or explicitly document why the recommendation is not being adopted

Blocking and major findings must not be silently ignored.

Where reviewers disagree, analyze the underlying trade-off rather than automatically preferring either reviewer.

Update the proposal with the resulting decisions.

## Phase 4 — Re-review

If either reviewer returned `CHANGES_REQUESTED`, submit the revised proposal to both reviewers again.

Provide each reviewer:

- the complete revised proposal
- the findings from its previous review
- a concise description of how those findings were addressed

Do not provide one reviewer with the other reviewer's findings unless resolving a direct disagreement requires it.

Repeat until:

- both reviewers return `APPROVED`, or
- the maximum review cycle count is reached.

Limit automated review to 5 cycles.

If the proposal cannot obtain approval after 5 cycles, stop and present the unresolved findings to the user.

Do not run the final consistency review while either primary reviewer still requests changes.

## Phase 5 — Consistency Review

Once BOTH `design-reviewer` and `performance-reviewer` return `APPROVED`, invoke:

- `design-editor`
- `design-consistency-reviewer`

The design editor's job is to make non-authoritative adjustments to the wording of the design for readability without changing its meaning.

The consistency reviewer receives the complete current proposal.

Its purpose is to detect cross-document contradictions, broken invariants, stale revision residue, identity/lifetime inconsistencies, and mismatches between the proposed design and its implementation plan.

Do not ask the consistency reviewer to redesign the proposal.

### Consistency Review Approval

If `design-consistency-reviewer` returns `APPROVED`, the automated design review is complete.

Proceed to the Human Approval Boundary.

### Consistency Review Changes

If `design-consistency-reviewer` returns `CHANGES_REQUESTED`:

1. Give its complete findings to `designer`.
2. Have `designer` revise the proposal.
3. Treat the resulting document as a new proposal revision.

Because consistency fixes may alter architectural or performance properties, do NOT send the revised document directly back only to the consistency reviewer.

Instead:

1. Submit the revised proposal again to `design-reviewer`.
2. Submit the revised proposal again to `performance-reviewer`.
3. Wait until both primary reviewers return `APPROVED`.
4. Run `design-consistency-reviewer` again.

Do not invoke the reviewers concurrently. The local inference backend has limited capacity for multiple simultaneous long-context requests.

No proposal revision may bypass the primary reviewers.

## Phase 6 — Human Approval

Only after:

- `doc-reviewer` returns `APPROVED`
- `performance-reviewer` returns `APPROVED`
- `design-consistency-reviewer` returns `APPROVED`

Present the user with:

- the final proposal
- a concise summary of the design
- significant trade-offs
- assumptions that remain
- open questions that remain
- the results of both reviews

Ask whether the approved proposal should be implemented.

Do not begin implementation without explicit user approval.

## Phase 7 — Implementation Handoff

If the user approves the proposal for implementation:

1. determine an appropriate feature branch name
2. invoke `feature.md` with:
    - the feature branch name
    - the path to the approved design document
3. delegate implementation, testing, and code review to the feature workflow

The approved design document is authoritative for implementation.

Do not implement the feature directly from this workflow.

If implementation reveals that the approved design is materially incorrect,
incomplete, or internally inconsistent, stop and return the issue to the
design workflow rather than silently changing the architecture during
implementation.
