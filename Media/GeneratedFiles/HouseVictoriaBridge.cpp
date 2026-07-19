void FHouseVictoriaBridgeModule::StartServer()
{
    UE_LOG(LogHouseVictoriaBridge, Log, TEXT("Starting HouseVictoriaBridge server..."));
    if (!BridgeServer.IsValid())
    {
        BridgeServer = MakeUnique<FHouseVictoriaBridgeServer>();
    }
    BridgeServer->Start();
}