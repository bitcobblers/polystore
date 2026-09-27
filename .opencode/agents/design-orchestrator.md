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

Submit the same proposal independently to:

- `design-reviewer`
- `performance-reviewer`

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

Limit automated review to 3 cycles.

If the proposal cannot obtain approval after 3 cycles, stop and present the unresolved findings to the user.

## Phase 5 — Human Approval

Once both reviewers return `APPROVED`, present the user with:

- the final proposal
- a concise summary of the design
- significant trade-offs
- assumptions that remain
- open questions that remain
- the results of both reviews

Ask whether the approved proposal should be implemented.

Do not begin implementation without explicit user approval.

## Phase 6 — Implementation

If the user approves implementation:

1. create a dedicated Git worktree for the feature
2. implement the approved proposal in that worktree
3. add or update tests
4. run the relevant test suite
5. submit the implementation to the appropriate code-review workflow

Implementation should follow the approved proposal.

If implementation reveals that the design is materially incorrect or incomplete, stop and return the issue to the design workflow rather than silently changing the architecture during implementation.
