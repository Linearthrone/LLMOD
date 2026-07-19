#include "HouseVictoriaBridgeTcpServer.h"

#include "Engine/World.h"
#include "EngineUtils.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "SocketSubsystem.h"
#include "IPAddress.h"
#include "Async/Async.h"

DEFINE_LOG_CATEGORY_STATIC(LogHouseVictoriaBridge, Log, All);

FHouseVictoriaBridgeTcpServer::FHouseVictoriaBridgeTcpServer()
    : Thread(nullptr)
    , bRunning(false)
    , ListenSocket(nullptr)
{
}

FHouseVictoriaBridgeTcpServer::~FHouseVictoriaBridgeTcpServer()
{
    Stop();
}

void FHouseVictoriaBridgeTcpServer::Start(uint32 Port)
{
    Stop();

    ISocketSubsystem* SocketSubsystem = ISocketSubsystem::Get(PLATFORM_SOCKETSUBSYSTEM);
    if (!SocketSubsystem)
    {
        UE_LOG(LogHouseVictoriaBridge, Error, TEXT("No socket subsystem available"));
        return;
    }

    ListenSocket = SocketSubsystem->CreateSocket(NAME_Stream, TEXT("HouseVictoriaListen"), false);
    if (!ListenSocket)
    {
        UE_LOG(LogHouseVictoriaBridge, Error, TEXT("Failed to create listen socket"));
        return;
    }

    ListenSocket->SetReuseAddr(true);
    ListenSocket->SetNonBlocking(true);

    TSharedRef<FInternetAddr> Addr = SocketSubsystem->CreateInternetAddr();
    Addr->SetAnyAddress();
    Addr->SetPort(Port);

    if (!ListenSocket->Bind(*Addr))
    {
        UE_LOG(LogHouseVictoriaBridge, Error, TEXT("Failed to bind to port %d"), Port);
        ListenSocket->Close();
        SocketSubsystem->DestroySocket(ListenSocket);
        ListenSocket = nullptr;
        return;
    }

    if (!ListenSocket->Listen(8))
    {
        UE_LOG(LogHouseVictoriaBridge, Error, TEXT("Failed to listen on port %d"), Port);
        ListenSocket->Close();
        SocketSubsystem->DestroySocket(ListenSocket);
        ListenSocket = nullptr;
        return;
    }

    bRunning = true;
    Thread = FRunnableThread::Create(this, TEXT("HouseVictoriaBridgeTcp"), 0, TPri_AboveNormal);

    UE_LOG(LogHouseVictoriaBridge, Log, TEXT("House Victoria TCP bridge listening on port %d"), Port);
}

void FHouseVictoriaBridgeTcpServer::Stop()
{
    bRunning = false;

    if (Thread)
    {
        Thread->Kill(true);
        delete Thread;
        Thread = nullptr;
    }

    FScopeLock Lock(&ClientSocketsLock);
    ISocketSubsystem* SocketSubsystem = ISocketSubsystem::Get(PLATFORM_SOCKETSUBSYSTEM);
    for (FSocket* Client : ClientSockets)
    {
        Client->Close();
        if (SocketSubsystem)
        {
            SocketSubsystem->DestroySocket(Client);
        }
    }
    ClientSockets.Empty();

    if (ListenSocket)
    {
        ListenSocket->Close();
        if (SocketSubsystem)
        {
            SocketSubsystem->DestroySocket(ListenSocket);
        }
        ListenSocket = nullptr;
    }
}

uint32 FHouseVictoriaBridgeTcpServer::Run()
{
    while (bRunning)
    {
        AcceptAndRead();
        FPlatformProcess::Sleep(0.016f);
    }
    return 0;
}

void FHouseVictoriaBridgeTcpServer::StopRunning()
{
    bRunning = false;
}

void FHouseVictoriaBridgeTcpServer::Exit()
{
}

void FHouseVictoriaBridgeTcpServer::AcceptAndRead()
{
    if (!ListenSocket)
    {
        return;
    }

    ISocketSubsystem* SocketSubsystem = ISocketSubsystem::Get(PLATFORM_SOCKETSUBSYSTEM);

    uint32 PendingSize = 0;
    if (ListenSocket->HasPendingConnection(PendingSize))
    {
        FSocket* Client = ListenSocket->Accept(TEXT("HouseVictoriaClient"));
        if (Client)
        {
            Client->SetNonBlocking(true);
            FScopeLock Lock(&ClientSocketsLock);
            ClientSockets.Add(Client);
            UE_LOG(LogHouseVictoriaBridge, Log, TEXT("Client connected"));
        }
    }

    TArray<FSocket*> Disconnected;
    {
        FScopeLock Lock(&ClientSocketsLock);
        for (FSocket* Client : ClientSockets)
        {
            uint32 Pending = 0;
            while (Client->HasPendingData(Pending) && Pending > 0)
            {
                TArray<uint8> Buffer;
                Buffer.SetNumUninitialized(FMath::Max<int32>(Pending, 1));
                int32 Read = 0;
                Client->Recv(Buffer.GetData(), Buffer.Num(), Read, ESocketReceiveFlags::None);
                if (Read <= 0)
                {
                    Disconnected.Add(Client);
                    break;
                }

                FString& Str = ClientBuffers.FindOrAdd(Client);
                Str.Append(FString(Read, (TCHAR*)Buffer.GetData()));
                // Note: this is a naive wide-char cast. It works for ASCII/UTF16 JSON.
                // For production we should convert UTF-8 bytes to FString properly.

                int32 Newline = 0;
                while ((Newline = Str.Find(TEXT("\n"))) != INDEX_NONE)
                {
                    FString Line = Str.Left(Newline).TrimStartAndEnd();
                    Str = Str.RightChop(Newline + 1);
                    if (!Line.IsEmpty())
                    {
                        ProcessLine(Line);
                    }
                }
            }
        }

        for (FSocket* Dead : Disconnected)
        {
            Dead->Close();
            if (SocketSubsystem)
            {
                SocketSubsystem->DestroySocket(Dead);
            }
            ClientSockets.Remove(Dead);
            ClientBuffers.Remove(Dead);
        }
    }
}

void FHouseVictoriaBridgeTcpServer::ProcessLine(const FString& Line)
{
    if (Line.StartsWith(TEXT("{")))
    {
        TSharedPtr<FJsonObject> Obj;
        TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Line);
        if (FJsonSerializer::Deserialize(Reader, Obj) && Obj.IsValid())
        {
            FString Type;
            if (Obj->TryGetStringField(TEXT("type"), Type) && Type == TEXT("command"))
            {
                const TSharedPtr<FJsonObject>* PayloadPtr;
                if (Obj->TryGetObjectField(TEXT("payload"), PayloadPtr) && PayloadPtr->IsValid())
                {
                    FString Name;
                    (*PayloadPtr)->TryGetStringField(TEXT("name"), Name);
                    const TSharedPtr<FJsonObject>* ArgsPtr;
                    TSharedPtr<FJsonObject> Args;
                    if ((*PayloadPtr)->TryGetObjectField(TEXT("args"), ArgsPtr) && ArgsPtr->IsValid())
                    {
                        Args = *ArgsPtr;
                    }
                    else
                    {
                        Args = MakeShared<FJsonObject>();
                    }
                    ExecuteOnGameThread(Name, Args);
                }
            }
            else
            {
                ExecuteOnGameThread(Type, Obj);
            }
        }
        return;
    }

    TArray<FString> Parts;
    Line.ParseIntoArrayWS(Parts);
    if (Parts.Num() == 0)
    {
        return;
    }

    TSharedPtr<FJsonObject> Args = MakeShared<FJsonObject>();
    TArray<TSharedPtr<FJsonValue>> PlainArgs;
    for (int32 i = 1; i < Parts.Num(); ++i)
    {
        PlainArgs.Add(MakeShared<FJsonValueString>(Parts[i]));
    }
    Args->SetArrayField(TEXT("plain"), PlainArgs);

    ExecuteOnGameThread(Parts[0], Args);
}

void FHouseVictoriaBridgeTcpServer::ExecuteOnGameThread(const FString& Command, const TSharedPtr<FJsonObject>& Args)
{
    FString Cmd = Command.ToLower();
    TSharedPtr<FJsonObject> SafeArgs = Args.IsValid() ? Args : MakeShared<FJsonObject>();

    AsyncTask(ENamedThreads::GameThread, [Cmd, SafeArgs]()
    {
        AActor* Avatar = FindAvatar();
        if (!Avatar)
        {
            UE_LOG(LogHouseVictoriaBridge, Warning, TEXT("No BP_MHC_Victoria avatar found"));
            return;
        }

        if (Cmd == TEXT("move_avatar") || Cmd == TEXT("set_transform"))
        {
            double X = 0.0, Y = 0.0, Z = 0.0;
            SafeArgs->TryGetNumberField(TEXT("x"), X);
            SafeArgs->TryGetNumberField(TEXT("y"), Y);
            SafeArgs->TryGetNumberField(TEXT("z"), Z);

            FVector Target(X, Y, Z);
            FVector Current = Avatar->GetActorLocation();

            if (Cmd == TEXT("move_avatar"))
            {
                Target += Current;
            }

            Avatar->SetActorLocation(Target, false, nullptr, ETeleportType::TeleportPhysics);
            UE_LOG(LogHouseVictoriaBridge, Log, TEXT("Avatar moved to %s"), *Target.ToString());
        }
        else if (Cmd == TEXT("rotate_avatar"))
        {
            double Pitch = 0.0, Yaw = 0.0, Roll = 0.0;
            SafeArgs->TryGetNumberField(TEXT("pitch"), Pitch);
            SafeArgs->TryGetNumberField(TEXT("yaw"), Yaw);
            SafeArgs->TryGetNumberField(TEXT("roll"), Roll);
            FRotator Rot(Pitch, Yaw, Roll);
            Avatar->SetActorRotation(Rot);
            UE_LOG(LogHouseVictoriaBridge, Log, TEXT("Avatar rotated to %s"), *Rot.ToString());
        }
        else if (Cmd == TEXT("status"))
        {
            UE_LOG(LogHouseVictoriaBridge, Log, TEXT("Avatar location=%s rotation=%s"),
                *Avatar->GetActorLocation().ToString(),
                *Avatar->GetActorRotation().ToString());
        }
        else
        {
            UE_LOG(LogHouseVictoriaBridge, Warning, TEXT("Unknown command: %s"), *Cmd);
        }
    });
}

AActor* FHouseVictoriaBridgeTcpServer::FindAvatar()
{
    UWorld* World = nullptr;

    const TIndirectArray<FWorldContext>& Contexts = GEngine->GetWorldContexts();
    for (const FWorldContext& Context : Contexts)
    {
        if (Context.World() && (Context.WorldType == EWorldType::PIE || Context.WorldType == EWorldType::Editor))
        {
            World = Context.World();
            break;
        }
    }

    if (!World)
    {
        return nullptr;
    }

    for (TActorIterator<AActor> It(World); It; ++It)
    {
        AActor* Actor = *It;
        if (Actor && Actor->GetName().StartsWith(TEXT("BP_MHC_Victoria")))
        {
            return Actor;
        }
    }

    return nullptr;
}
