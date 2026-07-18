#pragma once

#include <Arduino.h>
#include <stdint.h>

/// <summary>
/// Advertises the physical HASE TCP endpoint through mDNS/DNS-SD.
/// </summary>
class HaseMdnsAdvertiser
{
public:
    HaseMdnsAdvertiser();

    /// <summary>
    /// Starts mDNS and advertises _hase._tcp on the supplied port.
    /// </summary>
    /// <param name="hostName">
    /// The mDNS host name without the .local suffix.
    /// </param>
    /// <param name="instanceName">
    /// The DNS-SD service instance name.
    /// </param>
    /// <param name="port">
    /// The available HASE TCP endpoint port.
    /// </param>
    /// <returns>
    /// True when mDNS and the service advertisement were started.
    /// </returns>
    bool begin(
        const char* hostName,
        const char* instanceName,
        uint16_t port);

    /// <summary>
    /// Stops mDNS and removes all advertisements owned by this endpoint.
    /// </summary>
    void end();

    /// <summary>
    /// Gets whether the HASE service is currently advertised.
    /// </summary>
    bool isAdvertising() const;

private:
    bool advertising_;
};