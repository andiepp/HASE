#pragma once

#include <Arduino.h>

enum class HaseApplicationResult : uint8_t
{
    Success =
        0,

    Unavailable =
        1
};

using HaseReadNumericPropertyCallback =
    HaseApplicationResult (*)(
        void* context,
        double& value);

using HaseReadBooleanPropertyCallback =
    HaseApplicationResult (*)(
        void* context,
        bool& value);

using HaseWriteBooleanPropertyCallback =
    HaseApplicationResult (*)(
        void* context,
        bool value);

using HaseExecuteNullBooleanCommandCallback =
    HaseApplicationResult (*)(
        void* context,
        bool& returnValue);
