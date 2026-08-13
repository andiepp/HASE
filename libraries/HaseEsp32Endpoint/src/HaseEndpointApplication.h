#pragma once

class HaseEndpointRuntime;

class HaseEndpointApplication
{
public:
    virtual ~HaseEndpointApplication() = default;

    virtual bool beginHardware() = 0;

    virtual void beginEventDetection() = 0;

    virtual void update(
        HaseEndpointRuntime& runtime) = 0;
};
