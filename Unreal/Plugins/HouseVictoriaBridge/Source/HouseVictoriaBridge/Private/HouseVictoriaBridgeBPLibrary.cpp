#include "HouseVictoriaBridgeBPLibrary.h"

#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

bool UHouseVictoriaBridgeBPLibrary::ParseWebSocketMessage(const FString& RawText, FHouseVictoriaParsedMessage& OutMessage)
{
    OutMessage = FHouseVictoriaParsedMessage();

    const FString T = RawText.TrimStartAndEnd();
    if (T.IsEmpty())
    {
        return false;
    }

    if (T.StartsWith(TEXT("{")))
    {
        TSharedPtr<FJsonObject> Obj;
        const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(T);
        if (!FJsonSerializer::Deserialize(Reader, Obj) || !Obj.IsValid())
        {
            return false;
        }

        OutMessage.bParsedOk = true;
        OutMessage.bWasJson = true;

        FString Ty;
        Obj->TryGetStringField(TEXT("type"), Ty);
        OutMessage.MessageType = Ty;

        if (Ty == TEXT("command"))
        {
            const TSharedPtr<FJsonObject>* PayloadPtr;
            if (Obj->TryGetObjectField(TEXT("payload"), PayloadPtr) && PayloadPtr->IsValid())
            {
                FString Name;
                (*PayloadPtr)->TryGetStringField(TEXT("name"), Name);
                OutMessage.PrimaryVerb = Name;

                if (Name == TEXT("companion_remote_exchange"))
                {
                    const TSharedPtr<FJsonObject>* ArgsPtr;
                    if ((*PayloadPtr)->TryGetObjectField(TEXT("args"), ArgsPtr) && ArgsPtr->IsValid())
                    {
                        (*ArgsPtr)->TryGetStringField(TEXT("user"), OutMessage.CompanionUser);
                        (*ArgsPtr)->TryGetStringField(TEXT("assistant"), OutMessage.CompanionAssistant);
                        (*ArgsPtr)->TryGetStringField(TEXT("correlation_id"), OutMessage.CorrelationId);
                    }
                }
            }
        }
        else if (Ty == TEXT("status"))
        {
            OutMessage.PrimaryVerb = TEXT("status");
            Obj->TryGetStringField(TEXT("scene"), OutMessage.StatusScene);

            double AvatarCount = 0.0;
            if (Obj->TryGetNumberField(TEXT("avatar_count"), AvatarCount))
            {
                OutMessage.StatusAvatarCount = static_cast<int32>(AvatarCount);
            }

            double Fps = 0.0;
            if (Obj->TryGetNumberField(TEXT("fps"), Fps))
            {
                OutMessage.StatusFps = static_cast<float>(Fps);
            }

            bool Rendering = false;
            if (Obj->TryGetBoolField(TEXT("rendering"), Rendering))
            {
                OutMessage.bStatusRendering = Rendering;
            }
        }
        else if (Ty == TEXT("scene_update"))
        {
            OutMessage.PrimaryVerb = TEXT("scene_update");
            Obj->TryGetStringField(TEXT("scene"), OutMessage.StatusScene);
            Obj->TryGetStringField(TEXT("update_type"), OutMessage.SceneUpdateType);
        }

        return true;
    }

    TArray<FString> Parts;
    T.ParseIntoArrayWS(Parts);
    if (Parts.Num() == 0)
    {
        return false;
    }

    OutMessage.bParsedOk = true;
    OutMessage.bWasJson = false;
    OutMessage.MessageType = TEXT("plain");
    OutMessage.PrimaryVerb = Parts[0];
    for (int32 i = 1; i < Parts.Num(); ++i)
    {
        OutMessage.PlainArgs.Add(Parts[i]);
    }

    return true;
}
