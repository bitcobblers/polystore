---
description: Design and review a proposed PolyStore feature
agent: design-orchestrator
subagent: false
---

Run the complete PolyStore design workflow for the following feature.

## Proposal

Name: `$1`

Description:

$2

## Requirements

The final proposal must be written to:

`docs/design/$1.md`

The design workflow must:

1. Have the `designer` investigate the problem and produce the proposal.
2. Submit the proposal independently to:
    - `design-reviewer`
    - `performance-reviewer`
3. Collect both reviews before requesting revisions.
4. If either reviewer requests changes, return the findings to the `designer`.
5. Have the `designer` revise the proposal.
6. Re-submit the revised proposal to both reviewers.
7. Continue until:
    - both reviewers approve the proposal, or
    - three review cycles have completed.
8. If both reviewers approve, stop and present the result to the user.
9. If review does not converge after three cycles, stop and present the unresolved findings to the user.

Do not implement the feature.

Do not create a worktree.

The design document should remain in the current branch.

Reviewer approval does not authorize implementation.
