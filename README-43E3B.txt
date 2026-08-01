HASE - ADR-0043 Increment 43E3B
Selected-Host Event Presentation and Isolation

Baseline commit: 3c506b83e6bb53da71493cc7b31a9fb51b0b6078
Baseline tests : 4,279 passing
Expected tests : 4,283 passing

Extract into H:\Development with Visual Studio closed. Reopen HASE.slnx,
build Release, and run all tests. Report the build result and exact test count
before committing.

Event observations are qualified at the owning profile controller and filtered
by selected profile plus authoritative RuntimeHostId in WPF. No cross-host
ordering, persistence, replay, or diagnostics filtering is introduced.
