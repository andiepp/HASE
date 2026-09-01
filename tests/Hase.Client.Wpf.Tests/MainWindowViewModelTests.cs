using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Operator.Presentation;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void OpenDiagnosticsCommand_Configured_DelegatesToWindowController()
    {
        var viewModel = new MainWindowViewModel();
        var diagnostics = new StubDiagnosticsWindowController();
        viewModel.Configure(
            new StubController(),
            new StubFilePicker(null),
            diagnostics);

        viewModel.OpenDiagnosticsCommand.Execute();

        Assert.Equal(1, diagnostics.OpenCount);
    }

    [Fact]
    public void Constructor_ShouldExposeDisconnectedShell()
    {
        var viewModel =
            new MainWindowViewModel();

        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            viewModel.SessionStatus.State);
        Assert.Equal(
            "Disconnected",
            viewModel.SessionState);
        Assert.Equal(
            "Not connected",
            viewModel.RuntimeHostId);
        Assert.Equal(
            "Not available",
            viewModel.ApiVersion);
        Assert.True(
            viewModel.CanConnect);
        Assert.False(
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_Connected_ShouldExposeIdentityAndVersion()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));

        Assert.Equal(
            "Connected",
            viewModel.SessionState);
        Assert.Equal(
            "runtime-host-01",
            viewModel.RuntimeHostId);
        Assert.Equal(
            "1.0",
            viewModel.ApiVersion);
        Assert.False(
            viewModel.CanConnect);
        Assert.True(
            viewModel.CanDisconnect);
        Assert.True(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_Reconnecting_ShouldRetainStaleBaseline()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Reconnecting));

        Assert.Equal(
            "runtime-host-01",
            viewModel.RuntimeHostId);
        Assert.Equal(
            "1.0",
            viewModel.ApiVersion);
        Assert.False(
            viewModel.CanConnect);
        Assert.True(
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.True(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_Faulted_ShouldAllowDeliberateReconnect()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Faulted));

        Assert.True(
            viewModel.CanConnect);
        Assert.False(
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    [Fact]
    public void ApplySessionStatus_ShouldRaiseDependentProperties()
    {
        var viewModel =
            new MainWindowViewModel();
        var changedProperties =
            new List<string?>();
        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));

        Assert.Contains(
            nameof(MainWindowViewModel.SessionStatus),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.SessionState),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.RuntimeHostId),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.ApiVersion),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.CanConnect),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.CanDisconnect),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.IsOperational),
            changedProperties);
        Assert.Contains(
            nameof(MainWindowViewModel.IsStale),
            changedProperties);
    }

    [Fact]
    public void ApplySessionStatus_Null_ShouldThrow()
    {
        var viewModel =
            new MainWindowViewModel();

        Assert.Throws<ArgumentNullException>(
            () =>
                viewModel.ApplySessionStatus(
                    null!));
    }

    [Fact]
    public void ApplyObservationState_ShouldExposeStateAndEndpointCount()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplyObservationState(
            RemoteObservationState.Empty);

        Assert.Same(
            RemoteObservationState.Empty,
            viewModel.CurrentState);
        Assert.Equal(
            0,
            viewModel.EndpointCount);
    }

    [Fact]
    public void ApplyObservationState_Null_ShouldThrow()
    {
        var viewModel =
            new MainWindowViewModel();

        Assert.Throws<ArgumentNullException>(
            () =>
                viewModel.ApplyObservationState(
                    null!));
    }

    [Fact]
    public void ApplyObservationState_ShouldNotifyEndpointCount()
    {
        var viewModel =
            new MainWindowViewModel();
        var changedProperties =
            new List<string?>();
        viewModel.PropertyChanged +=
            (_, eventArgs) =>
                changedProperties.Add(
                    eventArgs.PropertyName);

        viewModel.ApplyObservationState(
            RemoteObservationState.Empty);

        Assert.Contains(
            nameof(MainWindowViewModel.EndpointCount),
            changedProperties);
    }

    [Fact]
    public async Task ApplyObservationState_AfterAuthoritativeRead_ShouldRetainConfirmedValue()
    {
        var propertyDescriptor =
            new PropertyDescriptor(
                new PropertyId(
                    "label"),
                DescriptorPath.Parse(
                    "Editor.Label"),
                "Label",
                new StringDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "editor-01"),
                "Editor",
                new InstrumentKind(
                    "Validation"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            propertyDescriptor
                        ])
            };
        var endpoint =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "f1259181-b8ad-4985-9803-2e65d76d4390")),
                new EndpointDescriptor(
                    new EndpointId(
                        "simulation-01"),
                    [
                        instrument
                    ]),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready));
        RemoteObservationState state =
            new RemoteObservationReducer().Initialize(
                RemoteObservationState.Empty,
                new RemoteObservationInitialSnapshot(
                    new RemoteRuntimeHostSnapshot(
                        new RemoteRuntimeHostId(
                            "runtime-01"),
                        RuntimeHostClientApiVersion.Current,
                        [
                            endpoint
                        ]),
                    new RemoteObservationSequence(
                        0)));
        var controller =
            new StubController
            {
                ReadResult =
                    RemotePropertyOperationResult.Successful(
                        new RemotePropertyValue(
                            RemoteValue.FromString(
                                "confirmed"),
                            new DateTimeOffset(
                                2026,
                                7,
                                29,
                                18,
                                0,
                                0,
                                TimeSpan.Zero),
                            RemotePropertyQuality.Good))
            };
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        viewModel.ApplyObservationState(
            state);
        PropertyInventoryItemViewModel property =
            Assert.Single(
                Assert.Single(
                    Assert.Single(
                        viewModel.Endpoints)
                    .Instruments)
                .Properties);

        await viewModel.ReadPropertyAsync(
            property);
        viewModel.ApplyObservationState(
            state);

        PropertyInventoryItemViewModel refreshed =
            Assert.Single(
                Assert.Single(
                    Assert.Single(
                        viewModel.Endpoints)
                    .Instruments)
                .Properties);
        Assert.Equal(
            "confirmed",
            refreshed.Value);
    }

    [Fact]
    public void ApplyEventOccurred_ShouldAddTransientOccurrence()
    {
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "controller-01"),
                "Controller",
                new InstrumentKind(
                    "Controller"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            new EventDescriptor(
                                DescriptorPath.Parse(
                                    "Controller.ButtonPressed"),
                                "Button Pressed")
                        ])
            };
        var endpoint =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "7f88a60b-ff77-420f-bc7d-73ad82c718e9")),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-01"),
                    [instrument]),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready));
        var viewModel =
            new MainWindowViewModel();
        viewModel.ApplyObservationState(
            new RemoteObservationReducer().Initialize(
                RemoteObservationState.Empty,
                new RemoteObservationInitialSnapshot(
                    new RemoteRuntimeHostSnapshot(
                        new RemoteRuntimeHostId(
                            "runtime-01"),
                        RuntimeHostClientApiVersion.Current,
                        [endpoint]),
                    new RemoteObservationSequence(
                        0))));

        viewModel.ApplyEventOccurred(
            new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(
                    1),
                endpoint.Key,
                new RemoteEventOccurredObservationPayload(
                    instrument.Id,
                    DescriptorPath.Parse(
                        "Controller.ButtonPressed"),
                    new DateTimeOffset(
                        2026,
                        7,
                        27,
                        16,
                        0,
                        0,
                        TimeSpan.Zero),
                    null)));

        EventOccurrenceItemViewModel occurrence =
            Assert.Single(
                viewModel.EventOccurrences);
        Assert.Equal(
            "Button Pressed",
            occurrence.DisplayName);
        Assert.Equal(
            "2026-07-27T16:00:00.0000000+00:00",
            occurrence.OccurredAtUtc);
        Assert.Equal(
            "Payload",
            occurrence.PayloadDisplayName);
        Assert.Equal(
            "No payload",
            occurrence.PayloadText);
        Assert.Equal(
            EventPayloadFormatStatus.NoPayload,
            occurrence.PayloadStatus);
        Assert.False(
            occurrence.HasPayloadDescription);
        Assert.False(
            occurrence.HasPayloadDiagnostic);
        Assert.True(
            viewModel.HasEventOccurrences);
    }

    [Theory]
    [MemberData(nameof(TypedEventPayloadCases))]
    public void ApplyEventOccurred_TypedPayload_ShouldUseDescriptorDrivenPresentation(
        EventPayloadDescriptor? descriptor,
        RemoteValue? value,
        string expectedText,
        EventPayloadFormatStatus expectedStatus)
    {
        EventOccurrenceItemViewModel occurrence =
            ApplyEvent(
                descriptor,
                value);

        Assert.Equal(
            descriptor?.DisplayName
                ?? "Payload",
            occurrence.PayloadDisplayName);
        Assert.Equal(
            descriptor?.Description
                ?? string.Empty,
            occurrence.PayloadDescription);
        Assert.Equal(
            expectedText,
            occurrence.PayloadText);
        Assert.Equal(
            expectedStatus,
            occurrence.PayloadStatus);
        Assert.Equal(
            expectedStatus is not EventPayloadFormatStatus.Formatted
                and not EventPayloadFormatStatus.NoPayload,
            occurrence.HasPayloadDiagnostic);
    }

    public static TheoryData<
        EventPayloadDescriptor?,
        RemoteValue?,
        string,
        EventPayloadFormatStatus> TypedEventPayloadCases =>
        new()
        {
            {
                CreateEventPayload(
                    new BooleanDataDescriptor()),
                RemoteValue.FromBoolean(
                    true),
                "True",
                EventPayloadFormatStatus.Formatted
            },
            {
                CreateEventPayload(
                    CreateNumericDescriptor()),
                RemoteValue.FromNumeric(
                    12.5),
                "12.5",
                EventPayloadFormatStatus.Formatted
            },
            {
                CreateEventPayload(
                    new StringDataDescriptor()),
                RemoteValue.FromString(
                    "ready"),
                "ready",
                EventPayloadFormatStatus.Formatted
            },
            {
                CreateEventPayload(
                    new ByteArrayDataDescriptor()),
                RemoteValue.FromByteArray(
                    new ByteArrayValue(
                        new byte[]
                        {
                            0x00,
                            0x53,
                            0xFF
                        })),
                "0053FF",
                EventPayloadFormatStatus.Formatted
            },
            {
                CreateEventPayload(
                    new BooleanDataDescriptor()),
                null,
                "Missing payload",
                EventPayloadFormatStatus.MissingPayload
            },
            {
                null,
                RemoteValue.FromBoolean(
                    true),
                "Unexpected payload",
                EventPayloadFormatStatus.UnexpectedPayload
            },
            {
                CreateEventPayload(
                    new BooleanDataDescriptor()),
                RemoteValue.FromString(
                    "true"),
                "Invalid payload",
                EventPayloadFormatStatus.TypeMismatch
            }
        };

    [Fact]
    public async Task ConnectAsync_SelectedConfiguration_ShouldUseController()
    {
        var controller =
            new StubController();
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                @"C:\HASE\client.json");

        await viewModel.ConnectAsync();

        Assert.Equal(
            @"C:\HASE\client.json",
            controller.ConfigurationFilePath);
        Assert.Null(
            viewModel.FailureMessage);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ConnectAsync_CancelledSelection_ShouldNotConnect()
    {
        var controller =
            new StubController();
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                null);

        await viewModel.ConnectAsync();

        Assert.Null(
            controller.ConfigurationFilePath);
    }

    [Fact]
    public async Task ConnectAsync_Failure_ShouldExposeGenericMessage()
    {
        var controller =
            new StubController
            {
                ConnectFailure =
                    new InvalidDataException(
                        "Secret path and thumbprint")
            };
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                @"C:\Secret\client.json");

        await viewModel.ConnectAsync();

        Assert.Equal(
            "The runtime-host connection could not be started.",
            viewModel.FailureMessage);
        Assert.DoesNotContain(
            "Secret",
            viewModel.FailureMessage);
    }

    [Fact]
    public async Task ConnectAsync_ExplicitPath_ShouldBypassFilePicker()
    {
        var controller =
            new StubController();
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                null);

        await viewModel.ConnectAsync(
            @"C:\Configuration\laptop-private-network.json");

        Assert.Equal(
            @"C:\Configuration\laptop-private-network.json",
            controller.ConfigurationFilePath);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ConnectAsync_EmptyExplicitPath_ShouldReject()
    {
        var viewModel =
            CreateConfiguredViewModel(
                new StubController(),
                null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => viewModel.ConnectAsync(
                " "));
    }

    [Fact]
    public async Task DisconnectAsync_ShouldUseController()
    {
        var controller =
            new StubController();
        var viewModel =
            CreateConfiguredViewModel(
                controller,
                null);

        await viewModel.DisconnectAsync();

        Assert.Equal(
            1,
            controller.DisconnectCount);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ReadPropertyAsync_ConnectedReadableProperty_ShouldUseExactTarget()
    {
        var target =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new Hase.Core.Domain.Identity.EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "4f88a60b-ff77-420f-bc7d-73ad82c718e9"))),
                new Hase.Core.Domain.Identity.InstrumentId(
                    "instrument-01"),
                new Hase.Core.Domain.Identity.PropertyId(
                    "property-01"));
        var controller =
            new StubController
            {
                ReadResult =
                    RemotePropertyOperationResult.Successful(
                        new RemotePropertyValue(
                            RemoteValue.FromNumeric(
                                22.5),
                            new DateTimeOffset(
                                2026,
                                7,
                                27,
                                13,
                                0,
                                0,
                                TimeSpan.Zero),
                            RemotePropertyQuality.Good))
            };
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        var property =
            new PropertyInventoryItemViewModel(
                target,
                "property-01",
                "Environment.Temperature",
                "Temperature",
                "Read",
                "Numeric",
                "°C",
                "No cached value",
                null,
                null,
                false,
                true,
                true,
                false,
                false);

        await viewModel.ReadPropertyAsync(
            property);

        Assert.Same(
            target,
            controller.ReadTarget);
        Assert.Equal(
            "Temperature: endpoint-confirmed value received.",
            viewModel.PropertyReadMessage);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task WriteBooleanPropertyAsync_ShouldSendSelectedValueOnce()
    {
        var target =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new Hase.Core.Domain.Identity.EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "5f88a60b-ff77-420f-bc7d-73ad82c718e9"))),
                new Hase.Core.Domain.Identity.InstrumentId(
                    "controller-01"),
                new Hase.Core.Domain.Identity.PropertyId(
                    "led-enabled"));
        var controller =
            new StubController
            {
                ReadResult =
                    RemotePropertyOperationResult.Successful(
                        new RemotePropertyValue(
                            RemoteValue.FromBoolean(
                                true),
                            new DateTimeOffset(
                                2026,
                                7,
                                27,
                                14,
                                0,
                                0,
                                TimeSpan.Zero),
                            RemotePropertyQuality.Good))
            };
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        var property =
            new PropertyInventoryItemViewModel(
                target,
                "led-enabled",
                "Controller.StatusLedEnabled",
                "Status LED Enabled",
                "ReadWrite",
                "Boolean",
                null,
                "False",
                null,
                null,
                false,
                true,
                true,
                true,
                true)
            {
                RequestedBooleanValue =
                    true
            };

        await viewModel.WriteBooleanPropertyAsync(
            property);

        Assert.Same(
            target,
            controller.WriteTarget);
        Assert.Equal(
            1,
            controller.WriteCount);
        Assert.Equal(
            RemoteValueKind.Boolean,
            controller.RequestedValue!.Kind);
        Assert.True(
            controller.RequestedValue.BooleanValue!.Value);
        Assert.Equal(
            "Status LED Enabled: endpoint-confirmed write completed.",
            viewModel.PropertyReadMessage);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task WritePropertyAsync_ValidNumericInput_ShouldSendTypedValueOnce()
    {
        var propertyId =
            new PropertyId(
                "setpoint");
        var descriptor =
            new PropertyDescriptor(
                propertyId,
                DescriptorPath.Parse(
                    "Environment.Setpoint"),
                "Setpoint",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -10,
                        50)))
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };
        var target =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "7fb61219-8040-4a57-ae5d-592a6c08d03b"))),
                new InstrumentId(
                    "controller-01"),
                propertyId);
        var controller =
            new StubController
            {
                ReadResult =
                    RemotePropertyOperationResult.Successful(
                        new RemotePropertyValue(
                            RemoteValue.FromNumeric(
                                23.5),
                            new DateTimeOffset(
                                2026,
                                7,
                                29,
                                12,
                                0,
                                0,
                                TimeSpan.Zero),
                            RemotePropertyQuality.Good))
            };
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        var property =
            new PropertyInventoryItemViewModel(
                target,
                propertyId.Value,
                descriptor.Path.ToString(),
                descriptor.DisplayName,
                descriptor.AccessMode.ToString(),
                "Numeric",
                "°C",
                "20",
                null,
                null,
                false,
                true,
                true,
                false,
                true,
                descriptor,
                PropertyInputEditorKind.Text,
                "23.5");

        await viewModel.WritePropertyAsync(
            property);

        Assert.Equal(
            1,
            controller.WriteCount);
        Assert.Equal(
            RemoteValueKind.Numeric,
            controller.RequestedValue!.Kind);
        Assert.Equal(
            23.5,
            controller.RequestedValue.NumericValue);
        Assert.Equal(
            "Setpoint: endpoint-confirmed write completed.",
            viewModel.PropertyReadMessage);
    }

    [Fact]
    public async Task WritePropertyAsync_InvalidNumericInput_ShouldRemainLocal()
    {
        var propertyId =
            new PropertyId(
                "setpoint");
        var descriptor =
            new PropertyDescriptor(
                propertyId,
                DescriptorPath.Parse(
                    "Environment.Setpoint"),
                "Setpoint",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius))
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };
        var target =
            new RemotePropertyTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "8fb61219-8040-4a57-ae5d-592a6c08d03b"))),
                new InstrumentId(
                    "controller-01"),
                propertyId);
        var controller =
            new StubController();
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        var property =
            new PropertyInventoryItemViewModel(
                target,
                propertyId.Value,
                descriptor.Path.ToString(),
                descriptor.DisplayName,
                descriptor.AccessMode.ToString(),
                "Numeric",
                "°C",
                "20",
                null,
                null,
                false,
                true,
                true,
                false,
                true,
                descriptor,
                PropertyInputEditorKind.Text,
                "23,5");

        await viewModel.WritePropertyAsync(
            property);

        Assert.Equal(
            0,
            controller.WriteCount);
        Assert.Equal(
            "Setpoint: Enter a finite number using '.' as the decimal separator.",
            viewModel.PropertyReadMessage);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldSendParameterlessCommandOnce()
    {
        var target =
            new RemoteCommandTarget(
                new RemoteEndpointAttachmentKey(
                    new Hase.Core.Domain.Identity.EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "6f88a60b-ff77-420f-bc7d-73ad82c718e9"))),
                new Hase.Core.Domain.Identity.InstrumentId(
                    "controller-01"),
                Hase.Core.Domain.Properties.DescriptorPath.Parse(
                    "Controller.ToggleLed"));
        var controller =
            new StubController();
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        var command =
            new CommandInventoryItemViewModel(
                target,
                "Controller.ToggleLed",
                "Toggle LED",
                null,
                true);

        await viewModel.ExecuteCommandAsync(
            command);

        Assert.Same(
            target,
            controller.CommandRequest!.Target);
        Assert.Null(
            controller.CommandRequest.Argument);
        Assert.Equal(
            "Toggle LED: Command completed.",
            viewModel.PropertyReadMessage);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ValidByteArrayArgument_ShouldSendExactBytesOnce()
    {
        var target =
            new RemoteCommandTarget(
                new RemoteEndpointAttachmentKey(
                    new Hase.Core.Domain.Identity.EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "7f88a60b-ff77-420f-bc7d-73ad82c718e9"))),
                new Hase.Core.Domain.Identity.InstrumentId(
                    "controller-01"),
                Hase.Core.Domain.Properties.DescriptorPath.Parse(
                    "Controller.Send"));
        var controller =
            new StubController();
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        var command =
            new CommandInventoryItemViewModel(
                target,
                "Controller.Send",
                "Send",
                null,
                true)
            {
                Descriptor =
                    new Hase.Core.Domain.Commands.CommandDescriptor(
                        Hase.Core.Domain.Properties.DescriptorPath.Parse(
                            "Controller.Send"),
                        "Send",
                        new Hase.Core.Domain.Commands.CommandArgumentDescriptor(
                            "Payload",
                            new Hase.Core.Domain.Data.ByteArrayDataDescriptor())),
                RequestedArgumentText =
                    "00 7f FF"
            };

        await viewModel.ExecuteCommandAsync(
            command);

        Assert.Equal(
            1,
            controller.CommandCount);
        Assert.Same(
            target,
            controller.CommandRequest!.Target);
        Assert.Equal(
            RemoteValueKind.ByteArray,
            controller.CommandRequest.Argument!.Kind);
        Assert.Equal(
            new byte[]
            {
                0x00,
                0x7F,
                0xFF
            },
            controller.CommandRequest.Argument.ByteArrayValue!.ToArray());
        Assert.Equal(
            "Send: Command completed.",
            viewModel.PropertyReadMessage);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ExecuteCommandAsync_InvalidByteArrayArgument_ShouldRemainLocal()
    {
        var target =
            new RemoteCommandTarget(
                new RemoteEndpointAttachmentKey(
                    new Hase.Core.Domain.Identity.EndpointId(
                        "endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "8f88a60b-ff77-420f-bc7d-73ad82c718e9"))),
                new Hase.Core.Domain.Identity.InstrumentId(
                    "controller-01"),
                Hase.Core.Domain.Properties.DescriptorPath.Parse(
                    "Controller.Send"));
        var controller =
            new StubController();
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(
                controller,
                null);
        viewModel.ApplySessionStatus(
            CreateStatus(
                RuntimeHostClientSessionState.Connected));
        var command =
            new CommandInventoryItemViewModel(
                target,
                "Controller.Send",
                "Send",
                null,
                true)
            {
                Descriptor =
                    new Hase.Core.Domain.Commands.CommandDescriptor(
                        Hase.Core.Domain.Properties.DescriptorPath.Parse(
                            "Controller.Send"),
                        "Send",
                        new Hase.Core.Domain.Commands.CommandArgumentDescriptor(
                            "Payload",
                            new Hase.Core.Domain.Data.ByteArrayDataDescriptor())),
                RequestedArgumentText =
                    "0"
            };

        await viewModel.ExecuteCommandAsync(
            command);

        Assert.Equal(
            0,
            controller.CommandCount);
        Assert.Null(
            controller.CommandRequest);
        Assert.StartsWith(
            "Send:",
            viewModel.PropertyReadMessage);
        Assert.Contains(
            "hexadecimal",
            viewModel.PropertyReadMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            viewModel.IsBusy);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ConfirmedShortCircuit_ShouldSendTrueOnceAndClearConfirmation()
    {
        var controller = new StubController();
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(controller, null);
        viewModel.ApplySessionStatus(
            CreateStatus(RuntimeHostClientSessionState.Connected));
        CommandInventoryItemViewModel command =
            CreateConfirmedShortCircuitActivation(true);

        await viewModel.ExecuteCommandAsync(command);

        Assert.Equal(1, controller.CommandCount);
        Assert.Equal(
            RemoteValueKind.Boolean,
            controller.CommandRequest!.Argument!.Kind);
        Assert.True(controller.CommandRequest.Argument.BooleanValue);
        Assert.Null(command.RequestedBooleanArgument);
        Assert.Equal(
            "Activate short circuit: Command completed.",
            viewModel.PropertyReadMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ExecuteCommandAsync_UnconfirmedShortCircuit_ShouldRemainLocal()
    {
        var controller = new StubController();
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(controller, null);
        viewModel.ApplySessionStatus(
            CreateStatus(RuntimeHostClientSessionState.Connected));
        CommandInventoryItemViewModel command =
            CreateConfirmedShortCircuitActivation(false);

        await viewModel.ExecuteCommandAsync(command);

        Assert.Equal(0, controller.CommandCount);
        Assert.False(command.RequestedBooleanArgument);
        Assert.Equal(
            "Activate short circuit: explicit Boolean confirmation true is required.",
            viewModel.PropertyReadMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ExecuteCommandAsync_FailedShortCircuit_ShouldClearConfirmation()
    {
        var controller = new StubController
        {
            CommandResult =
                RemoteCommandOperationResult.Failed(
                    RemoteCommandOperationStatus.EndpointRejected)
        };
        MainWindowViewModel viewModel =
            CreateConfiguredViewModel(controller, null);
        viewModel.ApplySessionStatus(
            CreateStatus(RuntimeHostClientSessionState.Connected));
        CommandInventoryItemViewModel command =
            CreateConfirmedShortCircuitActivation(true);

        await viewModel.ExecuteCommandAsync(command);

        Assert.Equal(1, controller.CommandCount);
        Assert.Null(command.RequestedBooleanArgument);
        Assert.Equal(
            "Activate short circuit: Command failed (EndpointRejected).",
            viewModel.PropertyReadMessage);
    }

    private static CommandInventoryItemViewModel CreateConfirmedShortCircuitActivation(
        bool requestedConfirmation)
    {
        DescriptorPath path =
            DescriptorPath.Parse("ShortCircuit.Activate");
        var target =
            new RemoteCommandTarget(
                new RemoteEndpointAttachmentKey(
                    new EndpointId("endpoint-01"),
                    new RemoteEndpointAttachmentGeneration(
                        Guid.Parse(
                            "9f88a60b-ff77-420f-bc7d-73ad82c718e9"))),
                new InstrumentId("electronic-load-01"),
                path);

        return new CommandInventoryItemViewModel(
            target,
            path.ToString(),
            "Activate short circuit",
            null,
            true)
        {
            Descriptor =
                DeclaredCommandDescriptors.Confirmed(path, "Activate short circuit"),
            RequestedBooleanArgument = requestedConfirmation
        };
    }

    [Fact]
    public void Configure_SecondCall_ShouldThrow()
    {
        var viewModel =
            CreateConfiguredViewModel(
                new StubController(),
                null);

        Assert.Throws<InvalidOperationException>(
            () =>
                viewModel.Configure(
                    new StubController(),
                    new StubFilePicker(
                        null)));
    }

    [Fact]
    public void ApplySessionFailure_ShouldPreserveCategoryAndRedactDiagnostic()
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionFailure(
            RuntimeHostClientFailureCategory.Authentication);

        Assert.Equal(
            RuntimeHostClientFailureCategory.Authentication,
            viewModel.LastFailureCategory);
        Assert.Equal(
            "Runtime-host authentication failed.",
            viewModel.FailureMessage);
    }

    [Theory]
    [InlineData(
        RuntimeHostClientSessionState.Connecting,
        false,
        true)]
    [InlineData(
        RuntimeHostClientSessionState.Disconnecting,
        false,
        false)]
    public void ApplySessionStatus_TransitionalState_ShouldControlActions(
        RuntimeHostClientSessionState state,
        bool canConnect,
        bool canDisconnect)
    {
        var viewModel =
            new MainWindowViewModel();

        viewModel.ApplySessionStatus(
            new RuntimeHostClientSessionStatus(
                state));

        Assert.Equal(
            canConnect,
            viewModel.CanConnect);
        Assert.Equal(
            canDisconnect,
            viewModel.CanDisconnect);
        Assert.False(
            viewModel.IsOperational);
        Assert.False(
            viewModel.IsStale);
    }

    private static EventOccurrenceItemViewModel ApplyEvent(
        EventPayloadDescriptor? payloadDescriptor,
        RemoteValue? value)
    {
        var eventDescriptor =
            new EventDescriptor(
                DescriptorPath.Parse(
                    "Controller.ButtonPressed"),
                "Button Pressed")
            {
                Payload =
                    payloadDescriptor
            };
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "controller-01"),
                "Controller",
                new InstrumentKind(
                    "Controller"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            eventDescriptor
                        ])
            };
        var endpoint =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "7f88a60b-ff77-420f-bc7d-73ad82c718e9")),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-01"),
                    [instrument]),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready));
        var viewModel =
            new MainWindowViewModel();
        viewModel.ApplyObservationState(
            new RemoteObservationReducer().Initialize(
                RemoteObservationState.Empty,
                new RemoteObservationInitialSnapshot(
                    new RemoteRuntimeHostSnapshot(
                        new RemoteRuntimeHostId(
                            "runtime-01"),
                        RuntimeHostClientApiVersion.Current,
                        [endpoint]),
                    new RemoteObservationSequence(
                        0))));

        viewModel.ApplyEventOccurred(
            new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(
                    1),
                endpoint.Key,
                new RemoteEventOccurredObservationPayload(
                    instrument.Id,
                    eventDescriptor.Path,
                    DateTimeOffset.UnixEpoch,
                    value)));

        return Assert.Single(
            viewModel.EventOccurrences);
    }

    private static EventPayloadDescriptor CreateEventPayload(
        DataDescriptor descriptor) =>
        new(
            "Reported value",
            descriptor)
        {
            Description =
                "Value reported by the Event."
        };

    private static NumericDataDescriptor CreateNumericDescriptor()
    {
        Quantity quantity =
            new(
                "ratio",
                "Ratio");
        Unit unit =
            new(
                "ratio",
                "Ratio",
                "ratio",
                quantity);

        return new NumericDataDescriptor(
            quantity,
            unit);
    }

    private static RuntimeHostClientSessionStatus CreateStatus(
        RuntimeHostClientSessionState state) =>
        new(
            state,
            new RemoteRuntimeHostId(
                "runtime-host-01"),
            new RuntimeHostClientApiVersion(
                1,
                0));

    private static MainWindowViewModel CreateConfiguredViewModel(
        StubController controller,
        string? configurationFilePath)
    {
        var viewModel =
            new MainWindowViewModel();
        viewModel.Configure(
            controller,
            new StubFilePicker(
                configurationFilePath));

        return viewModel;
    }

    private sealed class StubFilePicker
        : IClientConfigurationFilePicker
    {
        private readonly string? configurationFilePath;

        public StubFilePicker(
            string? configurationFilePath)
        {
            this.configurationFilePath =
                configurationFilePath;
        }

        public string? PickConfigurationFile() =>
            configurationFilePath;
    }

    private sealed class StubDiagnosticsWindowController
        : IClientDiagnosticsWindowController
    {
        public int OpenCount { get; private set; }

        public void Open() => OpenCount++;

        public void Close()
        {
        }
    }

    private sealed class StubController
        : IRuntimeHostClientSessionController
    {
        public Exception? ConnectFailure
        {
            get;
            init;
        }

        public string? ConfigurationFilePath
        {
            get;
            private set;
        }

        public int DisconnectCount
        {
            get;
            private set;
        }

        public RemotePropertyOperationResult? ReadResult
        {
            get;
            init;
        }

        public RemotePropertyTarget? ReadTarget
        {
            get;
            private set;
        }

        public RemotePropertyTarget? WriteTarget
        {
            get;
            private set;
        }

        public RemoteValue? RequestedValue
        {
            get;
            private set;
        }

        public int WriteCount
        {
            get;
            private set;
        }

        public RemoteCommandExecutionRequest? CommandRequest
        {
            get;
            private set;
        }

        public int CommandCount
        {
            get;
            private set;
        }

        public RemoteCommandOperationResult? CommandResult
        {
            get;
            init;
        }

        public Task ConnectAsync(
            string configurationFilePath,
            CancellationToken cancellationToken = default)
        {
            ConfigurationFilePath =
                configurationFilePath;

            return ConnectFailure is null
                ? Task.CompletedTask
                : Task.FromException(
                    ConnectFailure);
        }

        public Task DisconnectAsync()
        {
            DisconnectCount++;
            return Task.CompletedTask;
        }

        public Task<RemotePropertyOperationResult> ReadPropertyAsync(
            RemotePropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            ReadTarget =
                target;

            return Task.FromResult(
                ReadResult
                ?? RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.EndpointUnavailable));
        }

        public Task<RemotePropertyOperationResult> WritePropertyAsync(
            RemotePropertyTarget target,
            RemoteValue requestedValue,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            WriteTarget =
                target;
            RequestedValue =
                requestedValue;

            return Task.FromResult(
                ReadResult
                ?? RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.EndpointUnavailable));
        }

        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(
            RemoteCommandExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            CommandCount++;
            CommandRequest =
                request;

            return Task.FromResult(
                CommandResult
                ?? RemoteCommandOperationResult.Successful());
        }

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
    }
}
