#include "HaseUtcClock.h"

#include <sys/time.h>
#include <time.h>

bool HaseUtcClock::synchronize(
    unsigned long timeoutMilliseconds)
{
    _synchronized =
        false;

    configTime(
        0,
        0,
        PrimaryNtpServer,
        SecondaryNtpServer);

    unsigned long startedAt =
        millis();

    while (millis() - startedAt
           < timeoutMilliseconds)
    {
        time_t currentTime =
            time(
                nullptr);

        if (currentTime
            >= MinimumValidUnixTimeSeconds)
        {
            _synchronized =
                true;

            return true;
        }

        delay(
            100);
    }

    return false;
}

bool HaseUtcClock::isSynchronized() const
{
    return _synchronized;
}

bool HaseUtcClock::tryGetUnixTimeMilliseconds(
    int64_t& unixTimeMilliseconds) const
{
    unixTimeMilliseconds =
        0;

    if (!_synchronized)
    {
        return false;
    }

    timeval currentTime;

    if (gettimeofday(
            &currentTime,
            nullptr)
        != 0)
    {
        return false;
    }

    if (currentTime.tv_sec
        < MinimumValidUnixTimeSeconds)
    {
        return false;
    }

    constexpr int64_t MillisecondsPerSecond =
        1000;

    constexpr int64_t MicrosecondsPerMillisecond =
        1000;

    unixTimeMilliseconds =
        static_cast<int64_t>(
            currentTime.tv_sec)
        * MillisecondsPerSecond
        + static_cast<int64_t>(
              currentTime.tv_usec)
          / MicrosecondsPerMillisecond;

    return true;
}