using Hase.ProtocolExplorer.Hosting;
using Hase.ProtocolExplorer.Scenarios;

namespace Hase.ProtocolExplorer.Kel103;

/// <summary>
/// The protocol explorer with the KEL-103 characterization scenarios
/// composed into it.
/// </summary>
/// <remarks>
/// This is the add-on's own entry point. The published explorer ships the
/// generic surface and names no instrument; this one supplies the
/// characterization scenarios and their usage lines.
/// </remarks>
internal static class Program
{
    public static int Main(
        string[] args) =>
        ProtocolExplorerApplication.Run(
            args,
            [
                new Kel103ReadOnlyCharacterizationScenario(),
                new Kel103ReadOnlyMeasurementCharacterizationScenario(),
                new Kel103ReadOnlyStateCharacterizationScenario(),
                new Kel103ReadOnlyLimitCharacterizationScenario(),
                new Kel103ModeSelectionCharacterizationScenario(),
                new Kel103InputControlCharacterizationScenario(),
                new Kel103SetpointWriteCharacterizationScenario(),
                new Kel103SetpointChangeCharacterizationScenario()
            ],
            [
                "  kel103-characterize <COM port> <cr|lf|crlf> [baud rate]",
                "  kel103-measure-characterize <COM port> <voltage|current|power> cr 115200",
                "  kel103-state-characterize <COM port> <mode|input-state|target-voltage|target-current|target-resistance|target-power> cr 115200",
                "  kel103-limit-characterize <COM port> <target-voltage|target-current|target-resistance|target-power> <lower|upper> cr 115200",
                "  kel103-mode-select-characterize <COM port> <cv|cr|cw|short> cr 115200",
                "  kel103-input-control-characterize <COM port> true cr 115200",
                "  kel103-setpoint-write-characterize <COM port> <target-voltage|target-current|target-resistance|target-power> cr 115200",
                "  kel103-setpoint-change-characterize <COM port> <target-voltage|target-current|target-resistance|target-power> cr 115200"
            ]);
}
