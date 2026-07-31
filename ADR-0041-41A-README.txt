HASE ADR-0041 Increment 41A
Desktop Diagnostics Window Lifecycle Foundation

Authoritative baseline:
6a743093bacbf64fe32d6e0db55e3c95c532d935

Installation:
1. Close Visual Studio and the Desktop Runtime Host.
2. Extract this archive into H:\Development.
3. Allow existing files to be replaced.
4. Open the HASE solution in Visual Studio.
5. Build the complete solution.
6. Run the complete automated test suite.

Expected automated-test baseline:
3,913 existing tests plus 4 new tests = 3,917 tests.

Manual validation:
1. Start the Desktop Runtime Host normally.
2. Confirm that Open Diagnostics appears in the main window.
3. Select Open Diagnostics and confirm that a separate modeless window opens.
4. Position the diagnostics window beside the main host window.
5. Select Open Diagnostics again and confirm that no second window opens.
6. Minimize the diagnostics window, select Open Diagnostics, and confirm that
   the existing window is restored and activated.
7. Close only the diagnostics window and confirm that the runtime host remains
   running.
8. Select Open Diagnostics again and confirm that a fresh window opens.
9. Close the main host window and confirm that the diagnostics window also
   closes and the process exits.

Scope note:
The complete ADR-0040 diagnostics presentation intentionally remains embedded
in the main window in 41A. It moves to the separate window in Increment 41B.
