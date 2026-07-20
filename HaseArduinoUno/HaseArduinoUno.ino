/*
  HASE Arduino Uno - Compact Serial Protocol C-018 validation endpoint

  Implements only the minimum Compact Serial Protocol Version 1 behavior
  required by Protocol Explorer capability C-018:

  - receives one or more compact serial frames incrementally
  - validates start marker, protocol version, frame length, and CRC
  - accepts BootstrapRequest (message type 0x01) with an empty payload
  - returns BootstrapResponse (message type 0x02)
  - preserves the request correlation identifier
  - reports a fixed authoritative EndpointId
  - reports a fixed DescriptorReference

  Serial protocol:
    Baud rate          : 115200
    EndpointId         : arduino-uno-01
    DescriptorId       : arduino-uno-validation
    Descriptor version : 1

  Important:
    Do not print diagnostic text to Serial. Serial is the binary HASE transport.
*/

#include <Arduino.h>
#include <string.h>

namespace
{
  const uint32_t SerialBaudRate = 115200UL;

  const uint8_t StartMarkerFirstByte = 0x48;
  const uint8_t StartMarkerSecondByte = 0x53;
  const uint8_t ProtocolVersion = 0x01;

  const uint8_t BootstrapRequestMessageType = 0x01;
  const uint8_t BootstrapResponseMessageType = 0x02;

  const uint8_t FrameOverheadLength = 8;

  // The current validation endpoint only needs small bootstrap frames.
  // Keeping this buffer small avoids wasting SRAM on the Arduino Uno.
  const uint8_t MaximumSupportedFrameLength = 64;

  const char EndpointId[] = "arduino-uno-01";
  const char DescriptorId[] = "arduino-uno-validation";
  const uint16_t DescriptorVersion = 1;

  uint8_t receiveBuffer[MaximumSupportedFrameLength];
  uint8_t receiveLength = 0;
  uint8_t expectedFrameLength = 0;

  uint16_t calculateCrc16CcittFalse(
    const uint8_t* data,
    uint8_t length)
  {
    uint16_t crc = 0xFFFF;

    for (uint8_t index = 0; index < length; index++)
    {
      crc ^= static_cast<uint16_t>(data[index]) << 8;

      for (uint8_t bit = 0; bit < 8; bit++)
      {
        if ((crc & 0x8000) != 0)
        {
          crc =
            static_cast<uint16_t>(
              (crc << 1) ^ 0x1021);
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

  void resetReceiver()
  {
    receiveLength = 0;
    expectedFrameLength = 0;
  }

  void restartReceiverFromByte(
    uint8_t value)
  {
    resetReceiver();

    if (value == StartMarkerFirstByte)
    {
      receiveBuffer[0] = value;
      receiveLength = 1;
    }
  }

  void writeBootstrapResponse(
    uint8_t correlationId)
  {
    const uint8_t endpointIdLength =
      static_cast<uint8_t>(
        strlen(EndpointId));

    const uint8_t descriptorIdLength =
      static_cast<uint8_t>(
        strlen(DescriptorId));

    const uint8_t payloadLength =
      static_cast<uint8_t>(
        1
        + endpointIdLength
        + 1
        + descriptorIdLength
        + 2);

    const uint8_t frameLength =
      static_cast<uint8_t>(
        FrameOverheadLength
        + payloadLength);

    uint8_t frame[FrameOverheadLength + 40];

    uint8_t offset = 0;

    frame[offset++] = StartMarkerFirstByte;
    frame[offset++] = StartMarkerSecondByte;
    frame[offset++] = ProtocolVersion;
    frame[offset++] = BootstrapResponseMessageType;
    frame[offset++] = correlationId;
    frame[offset++] = payloadLength;

    frame[offset++] = endpointIdLength;

    memcpy(
      &frame[offset],
      EndpointId,
      endpointIdLength);

    offset =
      static_cast<uint8_t>(
        offset
        + endpointIdLength);

    frame[offset++] = descriptorIdLength;

    memcpy(
      &frame[offset],
      DescriptorId,
      descriptorIdLength);

    offset =
      static_cast<uint8_t>(
        offset
        + descriptorIdLength);

    frame[offset++] =
      static_cast<uint8_t>(
        DescriptorVersion >> 8);

    frame[offset++] =
      static_cast<uint8_t>(
        DescriptorVersion & 0xFF);

    const uint16_t crc =
      calculateCrc16CcittFalse(
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

    if (offset == frameLength)
    {
      Serial.write(
        frame,
        frameLength);

      Serial.flush();
    }
  }

  void processCompleteFrame()
  {
    if (receiveLength < FrameOverheadLength)
    {
      return;
    }

    if (receiveBuffer[0] != StartMarkerFirstByte
        || receiveBuffer[1] != StartMarkerSecondByte)
    {
      return;
    }

    if (receiveBuffer[2] != ProtocolVersion)
    {
      return;
    }

    const uint8_t payloadLength =
      receiveBuffer[5];

    const uint8_t frameLength =
      static_cast<uint8_t>(
        FrameOverheadLength
        + payloadLength);

    if (receiveLength != frameLength)
    {
      return;
    }

    const uint8_t crcOffset =
      static_cast<uint8_t>(
        6
        + payloadLength);

    const uint16_t expectedCrc =
      static_cast<uint16_t>(
        (static_cast<uint16_t>(
          receiveBuffer[crcOffset]) << 8)
        | receiveBuffer[crcOffset + 1]);

    const uint16_t actualCrc =
      calculateCrc16CcittFalse(
        &receiveBuffer[2],
        static_cast<uint8_t>(
          4
          + payloadLength));

    if (actualCrc != expectedCrc)
    {
      return;
    }

    const uint8_t messageType =
      receiveBuffer[3];

    const uint8_t correlationId =
      receiveBuffer[4];

    if (messageType != BootstrapRequestMessageType)
    {
      return;
    }

    if (correlationId == 0)
    {
      return;
    }

    if (payloadLength != 0)
    {
      return;
    }

    writeBootstrapResponse(
      correlationId);
  }

  void receiveByte(
    uint8_t value)
  {
    if (receiveLength == 0)
    {
      if (value == StartMarkerFirstByte)
      {
        receiveBuffer[0] = value;
        receiveLength = 1;
      }

      return;
    }

    if (receiveLength == 1)
    {
      if (value == StartMarkerSecondByte)
      {
        receiveBuffer[1] = value;
        receiveLength = 2;
      }
      else
      {
        restartReceiverFromByte(
          value);
      }

      return;
    }

    if (receiveLength >= MaximumSupportedFrameLength)
    {
      restartReceiverFromByte(
        value);

      return;
    }

    receiveBuffer[receiveLength++] =
      value;

    if (receiveLength == 6)
    {
      const uint8_t payloadLength =
        receiveBuffer[5];

      const uint16_t calculatedFrameLength =
        static_cast<uint16_t>(
          FrameOverheadLength)
        + payloadLength;

      if (calculatedFrameLength
          > MaximumSupportedFrameLength)
      {
        resetReceiver();
        return;
      }

      expectedFrameLength =
        static_cast<uint8_t>(
          calculatedFrameLength);
    }

    if (expectedFrameLength != 0
        && receiveLength == expectedFrameLength)
    {
      processCompleteFrame();
      resetReceiver();
    }
  }
}

void setup()
{
  Serial.begin(
    SerialBaudRate);

  resetReceiver();
}

void loop()
{
  while (Serial.available() > 0)
  {
    const int value =
      Serial.read();

    if (value >= 0)
    {
      receiveByte(
        static_cast<uint8_t>(
          value));
    }
  }
}
