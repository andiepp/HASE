# ADR-0043 Increment 43E4 — Multi-Host Diagnostics Validation

## Purpose

Validate that the diagnostics window presents explicit runtime-host session context without inferring host ownership for context-free records. Perform this after the automated suite passes.

## Preconditions

- Configure two runtime-host profiles with distinct display names and runtime-host identities.
- Start both runtime hosts and the laptop client from the same 43E4 build.
- Open the client diagnostics window.

## Validation matrix

| Check | Action | Expected result |
|---|---|---|
| Host choices | Inspect the **Runtime Host** filter | **All Runtime Hosts** appears first, followed by configured profiles in registry order. |
| Combined projection | Select **All Runtime Hosts**, then connect both profiles | Lifecycle records for both profiles appear; the **Profile** column identifies each qualified record. Context-free records may also appear. |
| First-host isolation | Select the first profile | Only records explicitly captured with that profile identity remain. Records for the second profile and context-free records are absent. |
| Second-host isolation | Select the second profile | Only records explicitly captured with that profile identity remain. Records for the first profile and context-free records are absent. |
| Detail identity | Select a profile-qualified lifecycle record | **Profile** and **Expected host** match configuration. **Authoritative host** is blank before authority is known and matches the connected runtime host afterward. |
| Existing filters | Combine one profile selection with level and category filters | Host, level, and category predicates all apply; capture retention is unchanged. |
| Pause/resume | Pause, cause activity on both hosts, then resume | The frozen projection remains stable while paused; resume reconciles the retained snapshot using the active filters. |
| Clear | Clear while a host filter is active | Collector and projection clear without changing the selected host filter or inventing host context. |
| Host failure isolation | Stop or fault one runtime host while the other remains active | The affected profile produces profile-qualified state/fault diagnostics; the other profile remains independently usable and filterable. |

## Evidence to record

- Automated test total.
- The two profile display names used (do not record credentials or private network locations).
- Pass/fail for each matrix row.
- Any unexpected record whose profile attribution is missing or incorrect.

Do not include passwords, credentials, tokens, private keys, host addresses, hostnames, URIs, or URLs in validation evidence.
