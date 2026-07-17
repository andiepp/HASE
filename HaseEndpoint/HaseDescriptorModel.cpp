#include "HaseDescriptorModel.h"

static_assert(
    sizeof(double) == 8,
    "HASE Protocol Version 1 requires 64-bit IEEE 754 double values.");

static_assert(
    static_cast<uint8_t>(
        HasePropertyAccessMode::None)
        == 0,
    "Unexpected HasePropertyAccessMode::None encoding.");

static_assert(
    static_cast<uint8_t>(
        HasePropertyAccessMode::Read)
        == 1,
    "Unexpected HasePropertyAccessMode::Read encoding.");

static_assert(
    static_cast<uint8_t>(
        HasePropertyAccessMode::Write)
        == 2,
    "Unexpected HasePropertyAccessMode::Write encoding.");

static_assert(
    static_cast<uint8_t>(
        HasePropertyAccessMode::ReadWrite)
        == 3,
    "Unexpected HasePropertyAccessMode::ReadWrite encoding.");

static_assert(
    static_cast<uint8_t>(
        HaseDataDescriptorType::String)
        == 1,
    "Unexpected string data-descriptor encoding.");

static_assert(
    static_cast<uint8_t>(
        HaseDataDescriptorType::Numeric)
        == 2,
    "Unexpected numeric data-descriptor encoding.");

static_assert(
    static_cast<uint8_t>(
        HaseDataDescriptorType::Boolean)
        == 3,
    "Unexpected Boolean data-descriptor encoding.");