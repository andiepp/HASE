/*
  HASE Arduino Uno Light - Compact Serial Protocol Endpoint

  Exposes two I2C light sensors on an Arduino Uno:
  - AS7331 UV sensor      : UV-A, UV-B, and UV-C irradiance
  - AS7343 spectral sensor: 14 spectral acquisition channels

  Protocol settings:
  - Baud rate          : 115200
  - EndpointId         : arduino-uno-light-01
  - DescriptorId       : arduino-uno-light
  - Descriptor version : 1

  Required Arduino libraries:
  - Adafruit AS7331 Library
  - Adafruit AS7343
  - Adafruit BusIO (dependency)

  Measurement model:
  The firmware refreshes one coherent snapshot of both sensors every
  MeasurementIntervalMilliseconds. Property reads return that snapshot, so a
  read never triggers a conversion and never blocks the transport. The two
  Measure Commands refresh the snapshot of one sensor immediately.

  Compact Properties, AS7331 instrument:
  - 0x01 UV-A irradiance      unsigned 16-bit little-endian, uW/cm2, read
  - 0x02 UV-B irradiance      unsigned 16-bit little-endian, uW/cm2, read
  - 0x03 UV-C irradiance      unsigned 16-bit little-endian, uW/cm2, read
  - 0x04 UV-A alarm threshold unsigned 16-bit little-endian, uW/cm2, r/w
  - 0x05 UV sensor ready      Boolean, read

  Compact Properties, AS7343 instrument:
  - 0x10 F1  405 nm           unsigned 16-bit little-endian, counts, read
  - 0x11 F2  425 nm
  - 0x12 FZ  450 nm
  - 0x13 F3  475 nm
  - 0x14 F4  515 nm
  - 0x15 F5  550 nm
  - 0x16 FY  555 nm
  - 0x17 FXL 600 nm
  - 0x18 F6  640 nm
  - 0x19 F7  690 nm
  - 0x1A F8  745 nm
  - 0x1B NIR 855 nm
  - 0x1C Visible top left
  - 0x1D Visible bottom right
  - 0x1E Spectral sensor ready Boolean, read

  Compact Commands:
  - 0x01 Measure UV
  - 0x02 Measure spectrum

  Compact Events:
  - 0x01 UV-A alarm raised, no payload

  A channel read returns ReadFailed while its sensor is absent or while its
  last acquisition failed. The sensor-ready Properties remain readable in
  both cases, so the endpoint still attaches and reports why it is degraded.

  Irradiance above 65535 uW/cm2 is reported as 65535, the declared maximum of
  the descriptor range.

  Important:
  Serial is the binary HASE transport.
  Do not write diagnostic text to Serial.

  Compact unsolicited notifications use correlation identifier 0.
  The firmware does not queue or replay alarm events.
*/

#include <Arduino.h>
#include <string.h>
#include <Wire.h>
#include <Adafruit_AS7331.h>
#include <Adafruit_AS7343.h>

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

  const uint8_t WritePropertyRequestMessageType =
    0x07;

  const uint8_t WritePropertyResponseMessageType =
    0x08;

  const uint8_t EventNotificationMessageType =
    0x09;

  const uint8_t MeasureUvCommandId =
    0x01;

  const uint8_t MeasureSpectrumCommandId =
    0x02;

  const uint8_t UvaIrradiancePropertyId =
    0x01;

  const uint8_t UvbIrradiancePropertyId =
    0x02;

  const uint8_t UvcIrradiancePropertyId =
    0x03;

  const uint8_t UvaAlarmThresholdPropertyId =
    0x04;

  const uint8_t UvSensorReadyPropertyId =
    0x05;

  const uint8_t FirstSpectralChannelPropertyId =
    0x10;

  const uint8_t SpectralChannelCount =
    14;

  const uint8_t SpectralSensorReadyPropertyId =
    0x1E;

  const uint8_t UvaAlarmRaisedEventId =
    0x01;

  const unsigned long MeasurementIntervalMilliseconds =
    500UL;

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

  const uint8_t PropertyReadStatusReadFailed =
    0x02;

  const uint8_t PropertyWriteStatusSuccess =
    0x00;

  const uint8_t PropertyWriteStatusUnknownProperty =
    0x01;

  const uint8_t PropertyWriteStatusWriteNotSupported =
    0x02;

  const uint8_t PropertyWriteStatusInvalidValue =
    0x03;

  const uint8_t FrameOverheadLength =
    8;

  const uint8_t MaximumSupportedFrameLength =
    64;

  const char EndpointId[] =
    "arduino-uno-light-01";

  const char DescriptorId[] =
    "arduino-uno-light";

  const uint16_t DescriptorVersion =
    1;

  /*
    Index of each exposed channel inside the 18-value buffer returned by
    Adafruit_AS7343::readAllChannels, in compact Property identifier order
    from 0x10 to 0x1D.
  */
  const uint8_t SpectralChannelSourceIndex[SpectralChannelCount] =
  {
    AS7343_CHANNEL_F1,
    AS7343_CHANNEL_F2,
    AS7343_CHANNEL_FZ,
    AS7343_CHANNEL_F3,
    AS7343_CHANNEL_F4,
    AS7343_CHANNEL_F5,
    AS7343_CHANNEL_FY,
    AS7343_CHANNEL_FXL,
    AS7343_CHANNEL_F6,
    AS7343_CHANNEL_F7,
    AS7343_CHANNEL_F8,
    AS7343_CHANNEL_NIR,
    AS7343_CHANNEL_VIS_TL_0,
    AS7343_CHANNEL_VIS_BR_0
  };

  Adafruit_AS7331 uvSensor;

  Adafruit_AS7343 spectralSensor;

  uint8_t receiveBuffer[
    MaximumSupportedFrameLength];

  uint8_t receiveLength =
    0;

  uint8_t expectedFrameLength =
    0;

  bool uvSensorReady =
    false;

  bool spectralSensorReady =
    false;

  bool uvReadingValid =
    false;

  bool spectralReadingValid =
    false;

  uint16_t uvaIrradiance =
    0;

  uint16_t uvbIrradiance =
    0;

  uint16_t uvcIrradiance =
    0;

  uint16_t spectralChannel[
    SpectralChannelCount];

  uint16_t uvaAlarmThreshold =
    0;

  bool uvaAlarmLatched =
    false;

  unsigned long lastMeasurementMilliseconds =
    0UL;

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

  void SendBooleanReadPropertyResponse(
    uint8_t correlationId,
    uint8_t propertyId,
    bool value)
  {
    const uint8_t payload[] =
    {
      propertyId,
      PropertyReadStatusSuccess,
      value
        ? static_cast<uint8_t>(0x01)
        : static_cast<uint8_t>(0x00)
    };

    SendFrame(
      ReadPropertyResponseMessageType,
      correlationId,
      payload,
      sizeof(payload));
  }

  void SendUnsigned16ReadPropertyResponse(
    uint8_t correlationId,
    uint8_t propertyId,
    uint16_t value)
  {
    const uint8_t payload[] =
    {
      propertyId,
      PropertyReadStatusSuccess,
      static_cast<uint8_t>(
        value
        & 0xFF),
      static_cast<uint8_t>(
        value
        >> 8)
    };

    SendFrame(
      ReadPropertyResponseMessageType,
      correlationId,
      payload,
      sizeof(payload));
  }

  void SendFailedReadPropertyResponse(
    uint8_t correlationId,
    uint8_t propertyId,
    uint8_t status)
  {
    const uint8_t payload[] =
    {
      propertyId,
      status
    };

    SendFrame(
      ReadPropertyResponseMessageType,
      correlationId,
      payload,
      sizeof(payload));
  }

  void SendWritePropertyResponse(
    uint8_t correlationId,
    uint8_t propertyId,
    uint8_t status)
  {
    const uint8_t payload[] =
    {
      propertyId,
      status
    };

    SendFrame(
      WritePropertyResponseMessageType,
      correlationId,
      payload,
      sizeof(payload));
  }

  void SendUvaAlarmRaisedEvent()
  {
    const uint8_t payload[] =
    {
      UvaAlarmRaisedEventId
    };

    SendFrame(
      EventNotificationMessageType,
      0x00,
      payload,
      sizeof(payload));
  }

  uint16_t ToIrradianceValue(
    float microwattsPerSquareCentimetre)
  {
    if (
      !(microwattsPerSquareCentimetre
        >= 0.0f))
    {
      return 0;
    }

    if (
      microwattsPerSquareCentimetre
      >= 65535.0f)
    {
      return 65535;
    }

    return
      static_cast<uint16_t>(
        microwattsPerSquareCentimetre
        + 0.5f);
  }

  void EvaluateUvaAlarm()
  {
    if (uvaAlarmThreshold == 0)
    {
      uvaAlarmLatched =
        false;

      return;
    }

    if (!uvReadingValid)
    {
      return;
    }

    if (!uvaAlarmLatched)
    {
      if (
        uvaIrradiance
        > uvaAlarmThreshold)
      {
        uvaAlarmLatched =
          true;

        SendUvaAlarmRaisedEvent();
      }

      return;
    }

    const uint16_t rearmHysteresis =
      static_cast<uint16_t>(
        (uvaAlarmThreshold / 16)
        + 1);

    if (
      static_cast<uint32_t>(
        uvaIrradiance)
      + rearmHysteresis
      <= uvaAlarmThreshold)
    {
      uvaAlarmLatched =
        false;
    }
  }

  bool RefreshUvMeasurement()
  {
    if (!uvSensorReady)
    {
      uvReadingValid =
        false;

      return false;
    }

    float uva =
      0.0f;

    float uvb =
      0.0f;

    float uvc =
      0.0f;

    if (!uvSensor.oneShot_uWcm2(
          &uva,
          &uvb,
          &uvc))
    {
      uvReadingValid =
        false;

      return false;
    }

    uvaIrradiance =
      ToIrradianceValue(
        uva);

    uvbIrradiance =
      ToIrradianceValue(
        uvb);

    uvcIrradiance =
      ToIrradianceValue(
        uvc);

    uvReadingValid =
      true;

    EvaluateUvaAlarm();

    return true;
  }

  bool RefreshSpectralMeasurement()
  {
    if (!spectralSensorReady)
    {
      spectralReadingValid =
        false;

      return false;
    }

    uint16_t readings[18];

    if (!spectralSensor.readAllChannels(
          readings))
    {
      spectralReadingValid =
        false;

      return false;
    }

    for (
      uint8_t index = 0;
      index < SpectralChannelCount;
      index++)
    {
      spectralChannel[index] =
        readings[
          SpectralChannelSourceIndex[
            index]];
    }

    spectralReadingValid =
      true;

    return true;
  }

  void SendReadPropertyResponse(
    uint8_t correlationId,
    uint8_t propertyId)
  {
    if (propertyId == UvSensorReadyPropertyId)
    {
      SendBooleanReadPropertyResponse(
        correlationId,
        propertyId,
        uvSensorReady);

      return;
    }

    if (propertyId == SpectralSensorReadyPropertyId)
    {
      SendBooleanReadPropertyResponse(
        correlationId,
        propertyId,
        spectralSensorReady);

      return;
    }

    if (propertyId == UvaAlarmThresholdPropertyId)
    {
      SendUnsigned16ReadPropertyResponse(
        correlationId,
        propertyId,
        uvaAlarmThreshold);

      return;
    }

    if (
      propertyId == UvaIrradiancePropertyId
      || propertyId == UvbIrradiancePropertyId
      || propertyId == UvcIrradiancePropertyId)
    {
      if (!uvReadingValid)
      {
        SendFailedReadPropertyResponse(
          correlationId,
          propertyId,
          PropertyReadStatusReadFailed);

        return;
      }

      uint16_t value =
        uvaIrradiance;

      if (propertyId == UvbIrradiancePropertyId)
      {
        value =
          uvbIrradiance;
      }
      else if (propertyId == UvcIrradiancePropertyId)
      {
        value =
          uvcIrradiance;
      }

      SendUnsigned16ReadPropertyResponse(
        correlationId,
        propertyId,
        value);

      return;
    }

    if (
      propertyId >= FirstSpectralChannelPropertyId
      && propertyId
        < FirstSpectralChannelPropertyId
          + SpectralChannelCount)
    {
      if (!spectralReadingValid)
      {
        SendFailedReadPropertyResponse(
          correlationId,
          propertyId,
          PropertyReadStatusReadFailed);

        return;
      }

      const uint8_t index =
        static_cast<uint8_t>(
          propertyId
          - FirstSpectralChannelPropertyId);

      SendUnsigned16ReadPropertyResponse(
        correlationId,
        propertyId,
        spectralChannel[index]);

      return;
    }

    SendFailedReadPropertyResponse(
      correlationId,
      propertyId,
      PropertyReadStatusUnknownProperty);
  }

  bool IsKnownPropertyId(
    uint8_t propertyId)
  {
    if (
      propertyId == UvaIrradiancePropertyId
      || propertyId == UvbIrradiancePropertyId
      || propertyId == UvcIrradiancePropertyId
      || propertyId == UvaAlarmThresholdPropertyId
      || propertyId == UvSensorReadyPropertyId
      || propertyId == SpectralSensorReadyPropertyId)
    {
      return true;
    }

    return
      propertyId >= FirstSpectralChannelPropertyId
      && propertyId
        < FirstSpectralChannelPropertyId
          + SpectralChannelCount;
  }

  uint8_t ExecuteCommand(
    uint8_t commandId)
  {
    if (commandId == MeasureUvCommandId)
    {
      return
        RefreshUvMeasurement()
          ? CommandStatusSuccess
          : CommandStatusExecutionFailed;
    }

    if (commandId == MeasureSpectrumCommandId)
    {
      return
        RefreshSpectralMeasurement()
          ? CommandStatusSuccess
          : CommandStatusExecutionFailed;
    }

    return
      CommandStatusUnknownCommand;
  }

  void ProcessBootstrapRequest(
    uint8_t correlationId,
    uint8_t payloadLength)
  {
    if (
      correlationId == 0
      || payloadLength != 0)
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
    if (
      correlationId == 0
      || payloadLength != 1)
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
    if (
      correlationId == 0
      || payloadLength != 1)
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

  void ProcessWritePropertyRequest(
    uint8_t correlationId,
    uint8_t payloadLength)
  {
    if (
      correlationId == 0
      || payloadLength == 0)
    {
      return;
    }

    const uint8_t propertyId =
      receiveBuffer[6];

    if (propertyId == 0)
    {
      return;
    }

    if (!IsKnownPropertyId(
          propertyId))
    {
      SendWritePropertyResponse(
        correlationId,
        propertyId,
        PropertyWriteStatusUnknownProperty);

      return;
    }

    if (
      propertyId
      != UvaAlarmThresholdPropertyId)
    {
      SendWritePropertyResponse(
        correlationId,
        propertyId,
        PropertyWriteStatusWriteNotSupported);

      return;
    }

    if (payloadLength != 3)
    {
      SendWritePropertyResponse(
        correlationId,
        propertyId,
        PropertyWriteStatusInvalidValue);

      return;
    }

    uvaAlarmThreshold =
      static_cast<uint16_t>(
        receiveBuffer[7]
        | (static_cast<uint16_t>(
             receiveBuffer[8])
           << 8));

    uvaAlarmLatched =
      false;

    SendWritePropertyResponse(
      correlationId,
      propertyId,
      PropertyWriteStatusSuccess);
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
        != StartMarkerSecondByte
      || receiveBuffer[2]
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

      case WritePropertyRequestMessageType:
        ProcessWritePropertyRequest(
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

  void ServiceMeasurements()
  {
    const unsigned long nowMilliseconds =
      millis();

    if (
      static_cast<unsigned long>(
        nowMilliseconds
        - lastMeasurementMilliseconds)
      < MeasurementIntervalMilliseconds)
    {
      return;
    }

    lastMeasurementMilliseconds =
      nowMilliseconds;

    RefreshUvMeasurement();

    RefreshSpectralMeasurement();
  }
}

void setup()
{
  for (
    uint8_t index = 0;
    index < SpectralChannelCount;
    index++)
  {
    spectralChannel[index] =
      0;
  }

  Wire.begin();

  uvSensorReady =
    uvSensor.begin();

  spectralSensorReady =
    spectralSensor.begin();

  Serial.begin(
    SerialBaudRate);

  ResetReceiver();

  lastMeasurementMilliseconds =
    millis();

  RefreshUvMeasurement();

  RefreshSpectralMeasurement();
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

  ServiceMeasurements();
}
