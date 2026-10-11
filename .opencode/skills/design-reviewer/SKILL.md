---
name: design-reviewer
description: Shared review protocol for PolyStore design proposals.
---

# Design Review Protocol

## Context

Before reviewing:

1. Read `ARCHITECTURE.md` and `AGENTS.md`.
2. Read the complete proposal.
3. Inspect relevant source and tests to verify material claims.

Established architecture takes precedence over proposal assumptions.
An intentional architectural change must be identified and justified.
Report unresolved conflicts rather than silently choosing an interpretation.

Investigate only what is relevant to the assigned review domain.
Distinguish verified facts, design assumptions, and unresolved questions.

## Review Rules

- Review only within the invoking agent's assigned domain.
- Report plausible failures supported by a concrete scenario or contradiction.
- Identify the affected construct and violated requirement or invariant.
- Distinguish existing risks from risks introduced or amplified by the proposal.
- Treat material omissions as findings with an assigned severity.
- Require the necessary behavioral guarantee or design decision;
  do not mandate an implementation mechanism when alternatives suffice.
- Do not expand scope, demand speculative abstractions, or require
  genuinely deferred systems to be designed now.
- A deferred decision is blocking when the proposed design already
  depends on its answer.
- Do not reopen resolved disagreements without new evidence,
  changed requirements, or a newly identified failure scenario.
- Do not modify proposals or source code.
- Ignore the proposal's Executive Summary.

## Severity

- **Blocking:** A fundamental contradiction or failure prevents
  correct implementation of the proposed design.
- **Major:** A material requirement, guarantee, or design decision
  must be resolved before implementation.
- **Minor:** A localized clarification or improvement that does not
  prevent correct implementation.

Blocking or Major findings require CHANGES_REQUESTED.
Minor findings alone do not prevent approval.
If several minor observations expose a material problem, report
that underlying problem as a Major finding.

## Findings

For each finding provide:

- ID and severity
- Location — section, API, example, or other affected construct
- Basis — relevant requirement or invariant
- Failure scenario or contradiction
- Consequence
- Resolution criterion — what must become true or explicit

Report each distinct root cause once.
Combine related symptoms unless they require separate resolutions.
Keep findings concise; include only evidence needed to assess them.
Do not report stylistic preferences as technical findings.

## Re-review

When reviewing a revised proposal:

- Re-evaluate previous findings against the current document.
- Preserve finding IDs and mark them resolved or unresolved.
- Check changes for new issues within the assigned domain.
- Do not assume that addressing a finding preserved other guarantees.
- Do not repeat resolved findings in full.

## Output

Return:

1. Review Summary — maximum 100 words.
2. Findings — grouped by severity.
3. Required Changes — finding IDs and concise resolution criteria,
   only when requesting changes.
4. Final Disposition.

Do not include empty categories.
Do not repeat findings across sections.
Do not restate the proposal or enumerate checks that found no issues.

End with exactly APPROVED or CHANGES_REQUESTED.
