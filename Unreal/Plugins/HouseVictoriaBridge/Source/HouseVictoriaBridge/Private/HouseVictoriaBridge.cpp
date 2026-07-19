#include "HouseVictoriaBridge.h"
#include "HouseVictoriaBridgeTcpServer.h"

#define LOCTEXT_NAMESPACE "FHouseVictoriaBridgeModule"

void FHouseVictoriaBridgeModule::StartupModule()
{
    TcpServer = MakeShared<FHouseVictoriaBridgeTcpServer>();
    TcpServer->Start(17711);
}

void FHouseVictoriaBridgeModule::ShutdownModule()
{
    if (TcpServer.IsValid())
    {
        TcpServer->Stop();
        TcpServer.Reset();
    }
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FHouseVictoriaBridgeModule, HouseVictoriaBridge)
