HASE ADR-0041 Increment 41B
Separate Diagnostics Presentation

Authoritative baseline:
0bc545f50b06b5f1ceace60f472d6563ac11aec8

Installation:
1. Close Visual Studio and the Desktop Runtime Host.
2. Extract this archive into H:\Development.
3. Allow existing files to be replaced.
4. Open the HASE solution in Visual Studio.
5. Build the complete solution.
6. Run the complete automated test suite.

Expected automated-test baseline:
3,917 existing tests plus 2 new tests = 3,919 tests.

Manual validation:
1. Start the Desktop Runtime Host with --diagnostics=bytes.
2. Confirm that Runtime Diagnostics no longer appears in the main window.
3. Select Open Diagnostics.
4. Confirm capture level, display filter, Clear diagnostics, byte warning,
   counters, record grid, selected-record details, and captured bytes appear
   in the separate window.
5. Exercise Operational, Protocol, and Bytes display filters.
6. Select several records and confirm details and bytes update correctly.
7. Close and reopen the diagnostics window and confirm current retained records
   remain available.
8. Clear diagnostics and confirm both counters and the record list update.
9. Confirm the runtime host and both physical endpoints continue normally.

Pause and Resume are intentionally not present in Increment 41B.
