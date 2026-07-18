#include "HaseMdnsAdvertiser.h"

#include <ESPmDNS.h>

HaseMdnsAdvertiser::HaseMdnsAdvertiser()
    : advertising_(
        false)
{
}

bool HaseMdnsAdvertiser::begin(
    const char* hostName,
    const char* instanceName,
    uint16_t port)
{
    if (advertising_)
    {
        return true;
    }

    if (hostName == nullptr
        || hostName[0] == '\0')
    {
        return false;
    }

    if (instanceName == nullptr
        || instanceName[0] == '\0')
    {
        return false;
    }

    if (port == 0)
    {
        return false;
    }

    if (!MDNS.begin(
            hostName))
    {
        return false;
    }

    MDNS.setInstanceName(
        instanceName);

    if (!MDNS.addService(
            "hase",
            "tcp",
            port))
    {
        MDNS.end();

        return false;
    }

    advertising_ =
        true;

    return true;
}

void HaseMdnsAdvertiser::end()
{
    if (!advertising_)
    {
        return;
    }

    MDNS.end();

    advertising_ =
        false;
}

bool HaseMdnsAdvertiser::isAdvertising() const
{
    return advertising_;
}