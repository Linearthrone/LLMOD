#pragma once

#include "CoreMinimal.h"
#include "HAL/Runnable.h"
#include "HAL/ThreadSafeBool.h"
#include "Sockets.h"

class FHouseVictoriaBridgeTcpServer : public FRunnable
{
public:
    FHouseVictoriaBridgeTcpServer();
    virtual ~FHouseVictoriaBridgeTcpServer();

    void Start(uint32 Port);
    void Stop();

    // FRunnable
    virtual uint32 Run() override;
    virtual void StopRunning() override;
    virtual void Exit() override;

private:
    void AcceptAndRead();
    void ProcessLine(const FString& Line);
    void ExecuteOnGameThread(const FString& Command, const TSharedPtr<FJsonObject>& Args);

    FRunnableThread* Thread;
    FThreadSafeBool bRunning;
    FSocket* ListenSocket;
    TArray<FSocket*> ClientSockets;
    FCriticalSection ClientSocketsLock;
    TMap<FSocket*, FString> ClientBuffers;

    static AActor* FindAvatar();
};
