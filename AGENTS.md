# HASE Agent Reliability Baseline

This file applies to the entire repository. Its rules are mandatory for every
agentic change unless the user explicitly approves an exception.

## 1. Reconstruct the authoritative state

Before proposing or implementing an increment:

1. Run `git fetch origin main`.
2. Record `HEAD`, `origin/main`, branch status, and the working-tree status.
3. Require the expected repository baseline and inspect the current repository
   files relevant to the task.
4. Treat committed repository content as the authority for implementation.
   User reports are authoritative evidence of physical results. Chat history,
   summaries, old ZIPs, and scratch worktrees are context, not source.
5. Never build a package from an older or dirty worktree. Create it from the
   exact approved commit and verify its final file scope.

If the repository, physical evidence, or requested baseline disagrees, stop and
classify the discrepancy. Do not guess which state is correct.

## 2. Do not infer missing facts

- Do not invent or infer APIs, constructors, paths, filenames, host roles,
  credentials, ACL behavior, process state, deployment state, or physical
  custody.
- Discover uncertain facts read-only. If discovery cannot resolve exactly one
  safe answer, ask the user.
- Preserve the established architecture unless the user explicitly approves a
  change.
- Never print certificate contents, private keys, secrets, remote addresses, or
  other protected values. Report only the minimum sanitized evidence required.

## 3. Use explicit increments and stop points

Every increment must state:

- goal and exact scope;
- authoritative starting commit;
- files to add or modify;
- automated validation;
- physical or deployment effects, including `none`;
- rollback or recovery boundary;
- definition of done.

Keep these as separate stop points unless the user explicitly combines them:

1. proposal and approval;
2. repository application;
3. focused and complete automated validation;
4. commit and push;
5. synchronization of AEPRAKETE, LABC, and LTAEP;
6. controlled physical operation;
7. independent physical validation;
8. documentation-only closure.

Repository changes never imply permission for deployment or physical mutation.
A successful Begin never implies permission to Finalize. A successful physical
operation never implies permission to delete retained evidence.

## 4. Deliver complete executable handoffs

- Give the user complete copy-and-paste PowerShell blocks, not partial command
  fragments or prose-only instructions.
- State exactly which computer runs each block.
- Use source ZIP packages for multi-file repository changes. Bind each package
  to the exact starting commit and publish its byte length and SHA-256.
- Keep transferred ZIPs and extracted source folders outside the repository
  status checks, or explicitly allow only their exact status entries.
- Remove temporary source ZIPs and folders before staging.
- Stage and commit only an explicit reviewed path list.
- Never claim a commit, push, synchronization, test, deployment, or physical
  result that the script did not compute or the user did not report.

Maintain visible progress during long work. Do not leave the user waiting
without a concise update.

## 5. Windows PowerShell rules

Operational scripts must target the actual Windows PowerShell environment used
on the three HASE computers.

- Start scripts with `$ErrorActionPreference = "Stop"` and
  `Set-StrictMode -Version Latest`.
- Wrap command output in `@(...)` before relying on `.Count`.
- Check native command exit codes where failure matters.
- Parse every packaged and installed `.ps1` with
  `System.Management.Automation.Language.Parser` before execution.
- Prefer simple statements over compressed one-line PowerShell.
- Use ASCII control tokens in validation. Do not make correctness depend on
  Unicode punctuation, console localization, Markdown wrapping, or line layout.
- Validate semantics and exact paths/hashes, not presentation text.
- Do not use a textual `Write-Host "...: True"` as evidence unless the value
  was actually computed.
- If the target Windows PowerShell version was not executed, say so explicitly;
  syntax inspection on another operating system is not equivalent validation.

PowerShell tooling changes require focused automated tests for parsing and for
the relevant success, rejection, failure, and recovery paths.

## 6. Security and physical mutation

Before credential, ACL, enrollment, authorization, deployment, recovery, or
deletion work:

- require explicit user approval for the named increment;
- prove the exact computer, repository revision, stopped processes, inputs,
  custody, target, and transaction identity;
- separate non-mutating preflight from mutation;
- preserve recoverable evidence until a later explicit cleanup approval;
- use byte-exact hashes for identity-sensitive files;
- verify the result independently after mutation.

After any failure, stop. First issue a read-only classification block that
identifies the completed phase, created artifacts, installed content, ACL state,
repository state, and safe recovery options. Do not rerun, repair, delete, or
continue until that state is known.

## 7. Validation and review

- Run the smallest relevant focused suite first, then the complete suite.
- Report exact totals, failures, skips, configuration, and meaningful warnings.
- Run repository diff validation and verify the exact changed-path set.
- Inspect the final diff; do not rely only on a generated success message.
- Treat line-ending warnings as warnings, but fail on actual `git diff --check`
  errors.
- Keep validation proportional. Avoid brittle duplicate checks that compare
  formatting when structural Git scope and tests already prove the result.
- Never weaken, delete, or rewrite a correct implementation merely to satisfy a
  faulty validation script. Correct the validator after classifying state.

## 8. Definition of done

An increment is complete only when its approved scope is implemented, relevant
focused and complete tests pass, the final diff is reviewed, the commit is
pushed, all required computers are synchronized and clean, and any separately
approved physical validation is recorded. An ADR is complete only when its ADR,
Project Status, and Roadmap consistently mark it closed.

End every handoff with the exact current baseline and the single next authorized
action. If no action remains, say so plainly.
