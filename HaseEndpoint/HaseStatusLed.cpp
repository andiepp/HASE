#include "HaseStatusLed.h"

HaseStatusLed::HaseStatusLed()
    : _initialized(
        false),
      _enabled(
        false)
{
}

void HaseStatusLed::begin()
{
    pinMode(
        Pin,
        OUTPUT);

    digitalWrite(
        Pin,
        HIGH);

    _enabled =
        false;

    _initialized =
        true;
}

bool HaseStatusLed::isInitialized() const
{
    return _initialized;
}

bool HaseStatusLed::isEnabled() const
{
    return _enabled;
}

void HaseStatusLed::setEnabled(
    bool enabled)
{
    if (!_initialized)
    {
        return;
    }

    digitalWrite(
        Pin,
        enabled
            ? LOW
            : HIGH);

    _enabled =
        enabled;
}

bool HaseStatusLed::toggleEnabled()
{
    if (!_initialized)
    {
        return
            _enabled;
    }

    setEnabled(
        !_enabled);

    return
        _enabled;
}