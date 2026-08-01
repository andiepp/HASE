HASE - ADR-0043 Increment 43E2
WPF Multi-Host Composition and Lifecycle Controls

Baseline commit: 5d8d55739a120c3b0ee3db502034c6fb2a108b5d
Baseline tests : 4,270 passing
Expected tests : 4,275 passing

Extract into H:\Development with Visual Studio closed. Reopen HASE.slnx,
build Release, and run all tests. Report the build result and exact test count
before committing.

The Release client argument now identifies hase-client-hosts.json. Profiles do
not connect automatically. Selected-host endpoint and operation presentation
remain deferred to 43E3.
