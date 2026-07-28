using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Protocol.Serialization;

/// <summary>
/// Maps typed Command arguments to and from endpoint descriptor extensions.
/// </summary>
internal sealed class CommandArgumentEndpointDescriptorExtensionMapper
{
    public const byte ExtensionType = 0x01;

    private readonly DataDescriptorSerializer _dataDescriptorSerializer =
        new();

    public IReadOnlyList<EndpointDescriptorExtension> CreateExtensions(
        EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        List<EndpointDescriptorExtension> extensions =
            new();

        foreach (InstrumentDescriptor instrument in descriptor.Instruments)
        {
            foreach (CommandDescriptor command in instrument.Interface.Commands)
            {
                if (command.Argument is null)
                {
                    continue;
                }

                BinaryProtocolWriter payloadWriter =
                    new();

                payloadWriter.WriteString(
                    instrument.Id.Value);

                payloadWriter.WriteString(
                    command.Path.ToString());

                payloadWriter.WriteString(
                    command.Argument.DisplayName);

                ProtocolSerializationHelper.WriteOptionalString(
                    payloadWriter,
                    command.Argument.Description);

                _dataDescriptorSerializer.Write(
                    payloadWriter,
                    command.Argument.Data);

                extensions.Add(
                    new EndpointDescriptorExtension(
                        ExtensionType,
                        payloadWriter.ToArray()));
            }
        }

        return extensions;
    }

    public EndpointDescriptor ApplyExtensions(
        EndpointDescriptor descriptor,
        IReadOnlyList<EndpointDescriptorExtension> extensions)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(extensions);

        EndpointDescriptor current =
            descriptor;

        HashSet<CommandTarget> targets =
            new();

        foreach (EndpointDescriptorExtension extension in extensions)
        {
            if (extension.Type != ExtensionType)
            {
                continue;
            }

            DecodedCommandArgument decoded =
                Decode(
                    extension);

            CommandTarget target =
                new(
                    decoded.InstrumentId.Value,
                    decoded.CommandPath.ToString());

            if (!targets.Add(target))
            {
                throw new InvalidDataException(
                    $"Duplicate Command argument extension for instrument " +
                    $"'{target.InstrumentId}' and Command " +
                    $"'{target.CommandPath}'.");
            }

            current =
                Apply(
                    current,
                    decoded);
        }

        return current;
    }

    private DecodedCommandArgument Decode(
        EndpointDescriptorExtension extension)
    {
        BinaryProtocolReader reader =
            new(
                extension.Payload.ToArray());

        InstrumentId instrumentId =
            new(
                reader.ReadString());

        DescriptorPath commandPath =
            DescriptorPath.Parse(
                reader.ReadString());

        string displayName =
            reader.ReadString();

        string? description =
            ProtocolSerializationHelper.ReadOptionalString(
                reader);

        CommandArgumentDescriptor argument =
            new(
                displayName,
                _dataDescriptorSerializer.Read(
                    reader))
            {
                Description = description
            };

        reader.EnsureFullyConsumed();

        return new DecodedCommandArgument(
            instrumentId,
            commandPath,
            argument);
    }

    private static EndpointDescriptor Apply(
        EndpointDescriptor descriptor,
        DecodedCommandArgument decoded)
    {
        int instrumentIndex =
            FindInstrumentIndex(
                descriptor,
                decoded.InstrumentId);

        if (instrumentIndex < 0)
        {
            throw new InvalidDataException(
                $"Command argument extension targets unknown instrument " +
                $"'{decoded.InstrumentId.Value}'.");
        }

        InstrumentDescriptor instrument =
            descriptor.Instruments[instrumentIndex];

        int commandIndex =
            FindCommandIndex(
                instrument,
                decoded.CommandPath);

        if (commandIndex < 0)
        {
            throw new InvalidDataException(
                $"Command argument extension targets unknown Command " +
                $"'{decoded.CommandPath}' on instrument " +
                $"'{decoded.InstrumentId.Value}'.");
        }

        CommandDescriptor command =
            instrument.Interface.Commands[commandIndex];

        if (command.Argument is not null)
        {
            throw new InvalidDataException(
                $"Command '{decoded.CommandPath}' on instrument " +
                $"'{decoded.InstrumentId.Value}' already has an argument.");
        }

        CommandDescriptor typedCommand =
            new(
                command.Path,
                command.DisplayName,
                decoded.Argument)
            {
                Description = command.Description
            };

        CommandDescriptor[] commands =
            instrument.Interface.Commands.ToArray();

        commands[commandIndex] =
            typedCommand;

        InstrumentDescriptor updatedInstrument =
            instrument with
            {
                Interface =
                    new InstrumentInterface(
                        instrument.Interface.Properties,
                        commands,
                        instrument.Interface.Events)
            };

        InstrumentDescriptor[] instruments =
            descriptor.Instruments.ToArray();

        instruments[instrumentIndex] =
            updatedInstrument;

        return new EndpointDescriptor(
            descriptor.Id,
            instruments)
        {
            Metadata = descriptor.Metadata
        };
    }

    private static int FindInstrumentIndex(
        EndpointDescriptor descriptor,
        InstrumentId instrumentId)
    {
        for (int index = 0; index < descriptor.Instruments.Count; index++)
        {
            if (descriptor.Instruments[index].Id == instrumentId)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindCommandIndex(
        InstrumentDescriptor instrument,
        DescriptorPath commandPath)
    {
        for (int index = 0;
            index < instrument.Interface.Commands.Count;
            index++)
        {
            if (instrument.Interface.Commands[index].Path == commandPath)
            {
                return index;
            }
        }

        return -1;
    }

    private sealed record DecodedCommandArgument(
        InstrumentId InstrumentId,
        DescriptorPath CommandPath,
        CommandArgumentDescriptor Argument);

    private readonly record struct CommandTarget(
        string InstrumentId,
        string CommandPath);
}
