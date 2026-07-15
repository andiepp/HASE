#pragma once

#include <Arduino.h>

class HaseUtcClock
{
public:
    bool synchronize(
        unsigned long timeoutMilliseconds);

    bool isSynchronized() const;

    bool tryGetUnixTimeMilliseconds(
        int64_t& unixTimeMilliseconds) const;

private:
    static constexpr const char* PrimaryNtpServer =
        "pool.ntp.org";

    static constexpr const char* SecondaryNtpServer =
        "time.nist.gov";

    static constexpr time_t MinimumValidUnixTimeSeconds =
        1704067200;

    bool _synchronized =
        false;
};