#pragma once

#include <Arduino.h>

class HaseStatusLed
{
public:
    HaseStatusLed();

    void begin();

    bool isInitialized() const;

    bool isEnabled() const;

    void setEnabled(
        bool enabled);

private:
    static constexpr uint8_t Pin =
        16;

    bool _initialized;
    bool _enabled;
};