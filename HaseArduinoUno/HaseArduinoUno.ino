/*
  HASE Arduino Uno - Compact Serial Protocol Endpoint

  Capability C-020:
  - Compact bootstrap
  - Execute compact command 0x01
  - Toggle LED_BUILTIN
  - Read compact property 0x01
  - Return the current LED_BUILTIN state

  Protocol settings:
  - Baud rate          : 115200
  - EndpointId         : arduino-uno-01
  - DescriptorId       : arduino-uno-validation
  - Descriptor version : 1

  Important:
  Serial is the binary HASE transport.
  Do not write diagnostic text to Serial.
*/

#include <Arduino.h>
#include <string.h>

namespace
{
  const uint32_t SerialBaudRate =
    115200UL;

  const uint8_t StartMarkerFirstByte =
    0x48;

  const uint8_t StartMarkerSecondByte =
    0x53;

  const uint8_t ProtocolVersion =
    0x01;

  const uint8_t BootstrapRequestMessageType =
    0x01;

  const uint8_t BootstrapResponseMessageType =
    0x02;

  const uint8_t ExecuteCommandRequestMessageType =
    0x03;

  const uint8_t ExecuteCommandResponseMessageType =
    0x04;

  const uint8_t ReadPropertyRequestMessageType =
    0x05;

  const uint8_t ReadPropertyResponseMessageType =
    0x06;

  const uint8_t ToggleBuiltInLedCommandId =
    0x01;

  const uint8_t BuiltInLedStatePropertyId =
    0x01;

  const uint8_t CommandStatusSuccess =
    0x00;

  const uint8_t CommandStatusUnknownCommand =
    0x01;

  const uint8_t CommandStatusExecutionFailed =
    0x02;

  const uint8_t PropertyReadStatusSuccess =
    0x00;

  const uint8_t PropertyReadStatusUnknownProperty =
    0x01;

  const uint8_t FrameOverheadLength =
    8;

  const uint8_t MaximumSupportedFrameLength =
    64;

  const char EndpointId[] =
    "arduino-uno-01";

  const char DescriptorId[] =
    "arduino-uno-validation";

  const uint16_t DescriptorVersion =
    1;

  uint8_t receiveBuffer[
    MaximumSupportedFrameLength];

  uint8_t receiveLength =
    0;

  uint8_t expectedFrameLength =
    0;

  bool builtInLedState =
    false;

  uint16_t CalculateCrc16CcittFalse(
    const uint8_t* data,
    uint8_t length)
  {
    uint16_t crc =
      0xFFFF;

    for (
      uint8_t index = 0;
      index < length;
      index++)
    {
      crc ^=
        static_cast<uint16_t>(
          data[index])
        << 8;

      for (
        uint8_t bit = 0;
        bit < 8;
        bit++)
      {
        if ((crc & 0x8000) != 0)
        {
          crc =
            static_cast<uint16_t>(
              (crc << 1)
              ^ 0x1021);
        }
        else
        {
          crc =
            static_cast<uint16_t>(
              crc << 1);
        }
      }
    }

    return crc;
  }

  void ResetReceiver()
  {
    receiveLength =
      0;

    expectedFrameLength =
      0;
  }

  void RestartReceiverFromByte(
    uint8_t value)
  {
    ResetReceiver();

    if (value == StartMarkerFirstByte)
    {
      receiveBuffer[0] =
        value;

      receiveLength =
        1;
    }
  }

  void SendFrame(
    uint8_t messageType,
    uint8_t correlationId,
    const uint8_t* payload,
    uint8_t payloadLength)
  {
    uint8_t frame[
      MaximumSupportedFrameLength];

    uint8_t offset =
      0;

    frame[offset++] =
      StartMarkerFirstByte;

    frame[offset++] =
      StartMarkerSecondByte;

    frame[offset++] =
      ProtocolVersion;

    frame[offset++] =
      messageType;

    frame[offset++] =
      correlationId;

    frame[offset++] =
      payloadLength;

    if (payloadLength > 0)
    {
      memcpy(
        &frame[offset],
        payload,
        payloadLength);

      offset =
        static_cast<uint8_t>(
          offset
          + payloadLength);
    }

    const uint16_t crc =
      CalculateCrc16CcittFalse(
        &frame[2],
        static_cast<uint8_t>(
          4
          + payloadLength));

    frame[offset++] =
      static_cast<uint8_t>(
        crc >> 8);

    frame[offset++] =
      static_cast<uint8_t>(
        crc & 0xFF);

    Serial.write(
      frame,
      offset);

    Serial.flush();
  }

  void SendBootstrapResponse(
    uint8_t correlationId)
  {
    const uint8_t endpointIdLength =
      static_cast<uint8_t>(
        strlen(
          EndpointId));

    const uint8_t descriptorIdLength =
      static_cast<uint8_t>(
        strlen(
          DescriptorId));

    uint8_t payload[
      MaximumSupportedFrameLength];

    uint8_t offset =
      0;

    payload[offset++] =
      endpointIdLength;

    memcpy(
      &payload[offset],
      EndpointId,
      endpointIdLength);

    offset =
      static_cast<uint8_t>(
        offset
        + endpointIdLength);

    payload[offset++] =
      descriptorIdLength;

    memcpy(
      &payload[offset],
      DescriptorId,
      descriptorIdLength);

    offset =
      static_cast<uint8_t>(
        offset
        + descriptorIdLength);

    payload[offset++] =
      static_cast<uint8_t>(
        DescriptorVersion
        >> 8);

    payload[offset++] =
      static_cast<uint8_t>(
        DescriptorVersion
        & 0xFF);

    SendFrame(
      BootstrapResponseMessageType,
      correlationId,
      payload,
      offset);
  }

  void SendExecuteCommandResponse(
    uint8_t correlationId,
    uint8_t commandId,
    uint8_t status)
  {
    const uint8_t payload[] =
    {
      commandId,
      status
    };

    SendFrame(
      ExecuteCommandResponseMessageType,
      correlationId,
      payload,
      sizeof(payload));
  }

  void SendReadPropertyResponse(
    uint8_t correlationId,
    uint8_t propertyId)
  {
    if (
      propertyId
      != BuiltInLedStatePropertyId)
    {
      const uint8_t payload[] =
      {
        propertyId,
        PropertyReadStatusUnknownProperty
      };

      SendFrame(
        ReadPropertyResponseMessageType,
        correlationId,
        payload,
        sizeof(payload));

      return;
    }

    const uint8_t payload[] =
    {
      propertyId,
      PropertyReadStatusSuccess,
      builtInLedState
        ? 0x01
        : 0x00
    };

    SendFrame(
      ReadPropertyResponseMessageType,
      correlationId,
      payload,
      sizeof(payload));
  }

  uint8_t ExecuteCommand(
    uint8_t commandId)
  {
    if (
      commandId
      != ToggleBuiltInLedCommandId)
    {
      return
        CommandStatusUnknownCommand;
    }

    builtInLedState =
      !builtInLedState;

    digitalWrite(
      LED_BUILTIN,
      builtInLedState
        ? HIGH
        : LOW);

    return
      CommandStatusSuccess;
  }

  void ProcessBootstrapRequest(
    uint8_t correlationId,
    uint8_t payloadLength)
  {
    if (correlationId == 0)
    {
      return;
    }

    if (payloadLength != 0)
    {
      return;
    }

    SendBootstrapResponse(
      correlationId);
  }

  void ProcessExecuteCommandRequest(
    uint8_t correlationId,
    uint8_t payloadLength)
  {
    if (correlationId == 0)
    {
      return;
    }

    if (payloadLength != 1)
    {
      return;
    }

    const uint8_t commandId =
      receiveBuffer[6];

    const uint8_t status =
      ExecuteCommand(
        commandId);

    SendExecuteCommandResponse(
      correlationId,
      commandId,
      status);
  }

  void ProcessReadPropertyRequest(
    uint8_t correlationId,
    uint8_t payloadLength)
  {
    if (correlationId == 0)
    {
      return;
    }

    if (payloadLength != 1)
    {
      return;
    }

    const uint8_t propertyId =
      receiveBuffer[6];

    if (propertyId == 0)
    {
      return;
    }

    SendReadPropertyResponse(
      correlationId,
      propertyId);
  }

  void ProcessCompleteFrame()
  {
    if (
      receiveLength
      < FrameOverheadLength)
    {
      return;
    }

    if (
      receiveBuffer[0]
        != StartMarkerFirstByte
      || receiveBuffer[1]
        != StartMarkerSecondByte)
    {
      return;
    }

    if (
      receiveBuffer[2]
      != ProtocolVersion)
    {
      return;
    }

    const uint8_t payloadLength =
      receiveBuffer[5];

    const uint8_t frameLength =
      static_cast<uint8_t>(
        FrameOverheadLength
        + payloadLength);

    if (
      receiveLength
      != frameLength)
    {
      return;
    }

    const uint8_t crcOffset =
      static_cast<uint8_t>(
        6
        + payloadLength);

    const uint16_t expectedCrc =
      static_cast<uint16_t>(
        static_cast<uint16_t>(
          receiveBuffer[
            crcOffset])
        << 8)
      | receiveBuffer[
          crcOffset + 1];

    const uint16_t actualCrc =
      CalculateCrc16CcittFalse(
        &receiveBuffer[2],
        static_cast<uint8_t>(
          4
          + payloadLength));

    if (
      actualCrc
      != expectedCrc)
    {
      return;
    }

    const uint8_t messageType =
      receiveBuffer[3];

    const uint8_t correlationId =
      receiveBuffer[4];

    switch (messageType)
    {
      case BootstrapRequestMessageType:
        ProcessBootstrapRequest(
          correlationId,
          payloadLength);
        break;

      case ExecuteCommandRequestMessageType:
        ProcessExecuteCommandRequest(
          correlationId,
          payloadLength);
        break;

      case ReadPropertyRequestMessageType:
        ProcessReadPropertyRequest(
          correlationId,
          payloadLength);
        break;

      default:
        break;
    }
  }

  void ReceiveByte(
    uint8_t value)
  {
    if (receiveLength == 0)
    {
      if (
        value
        == StartMarkerFirstByte)
      {
        receiveBuffer[0] =
          value;

        receiveLength =
          1;
      }

      return;
    }

    if (receiveLength == 1)
    {
      if (
        value
        == StartMarkerSecondByte)
      {
        receiveBuffer[1] =
          value;

        receiveLength =
          2;
      }
      else
      {
        RestartReceiverFromByte(
          value);
      }

      return;
    }

    if (
      receiveLength
      >= MaximumSupportedFrameLength)
    {
      RestartReceiverFromByte(
        value);

      return;
    }

    receiveBuffer[
      receiveLength++] =
      value;

    if (receiveLength == 6)
    {
      const uint8_t payloadLength =
        receiveBuffer[5];

      const uint16_t calculatedFrameLength =
        static_cast<uint16_t>(
          FrameOverheadLength)
        + payloadLength;

      if (
        calculatedFrameLength
        > MaximumSupportedFrameLength)
      {
        ResetReceiver();

        return;
      }

      expectedFrameLength =
        static_cast<uint8_t>(
          calculatedFrameLength);
    }

    if (
      expectedFrameLength != 0
      && receiveLength
        == expectedFrameLength)
    {
      ProcessCompleteFrame();

      ResetReceiver();
    }
  }
}

void setup()
{
  pinMode(
    LED_BUILTIN,
    OUTPUT);

  builtInLedState =
    false;

  digitalWrite(
    LED_BUILTIN,
    LOW);

  Serial.begin(
    SerialBaudRate);

  ResetReceiver();
}

void loop()
{
  while (
    Serial.available()
    > 0)
  {
    const int value =
      Serial.read();

    if (value >= 0)
    {
      ReceiveByte(
        static_cast<uint8_t>(
          value));
    }
  }
}