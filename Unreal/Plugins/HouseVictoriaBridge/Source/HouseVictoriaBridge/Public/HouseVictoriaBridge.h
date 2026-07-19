#pragma once

#include "CoreMinimal.h"
#include "Modules/ModuleInterface.h"

class FHouseVictoriaBridgeTcpServer;

class HOUSEVICTORIABRIDGE_API FHouseVictoriaBridgeModule : public IModuleInterface
{
public:
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;

private:
    TSharedPtr<FHouseVictoriaBridgeTcpServer> TcpServer;
};
