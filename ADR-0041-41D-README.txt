HASE ADR-0041 Increment 41D
Pause/Resume Controls and Status

Authoritative baseline:
21ed4171d297012af149f7c1e28afc9a3cc60930

Installation:
1. Close Visual Studio and the Desktop Runtime Host.
2. Extract this archive into H:\Development.
3. Allow existing files to be replaced.
4. Open the HASE solution in Visual Studio.
5. Build the complete solution.
6. Run the complete automated test suite.

Expected automated-test baseline:
3,925 existing tests plus 5 new tests = 3,930 tests.

Manual validation:
1. Start the Desktop Runtime Host with --diagnostics=bytes.
2. Open Diagnostics and confirm Presentation: Running.
3. Confirm Pause is enabled and Resume is disabled.
4. Select Pause and confirm Presentation: Paused.
5. Confirm Pause is disabled and Resume is enabled.
6. Generate activity from both the ESP32 and Arduino Uno.
7. Confirm displayed records and the current selection remain frozen.
8. Confirm the paused description states that capture and bounded retention
   continue.
9. Change the display filter and confirm it applies to the frozen presentation.
10. Select Resume and confirm new retained activity appears immediately in
    newest-first order.
11. Confirm Presentation: Running returns.
12. Repeat pause/resume and confirm both physical endpoints remain operational.
