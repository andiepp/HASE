HASE ADR-0041 Increment 41C
Presentation Pause State

Authoritative baseline:
fe207642f3850f899d58dc2aedc7d9eba184444b

Installation:
1. Close Visual Studio and the Desktop Runtime Host.
2. Extract this archive into H:\Development.
3. Allow existing files to be replaced.
4. Open the HASE solution in Visual Studio.
5. Build the complete solution.
6. Run the complete automated test suite.

Expected automated-test baseline:
3,919 existing tests plus 6 new tests = 3,925 tests.

There is no manual UI validation in 41C because Pause and Resume controls are
intentionally deferred to 41D.
