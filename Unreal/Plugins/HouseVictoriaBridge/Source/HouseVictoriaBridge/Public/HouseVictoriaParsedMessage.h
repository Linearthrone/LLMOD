#pragma once

#include "CoreMinimal.h"
#include "HouseVictoriaParsedMessage.generated.h"

/** Result of parsing one WebSocket text frame from House Victoria. */
USTRUCT(BlueprintType)
struct HOUSEVICTORIABRIDGE_API FHouseVictoriaParsedMessage
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    bool bParsedOk = false;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    bool bWasJson = false;

    /** JSON `type` field, or the word `plain` for line-protocol frames. */
    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    FString MessageType;

    /** Plain: first token. JSON command: payload.name. JSON status: \"status\". */
    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    FString PrimaryVerb;

    /** Plain text: arguments after the verb (split on whitespace). */
    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    TArray<FString> PlainArgs;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    FString CompanionUser;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    FString CompanionAssistant;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    FString CorrelationId;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    FString StatusScene;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    int32 StatusAvatarCount = -1;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    float StatusFps = -1.f;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    bool bStatusRendering = false;

    UPROPERTY(BlueprintReadOnly, Category = "HouseVictoria")
    FString SceneUpdateType;
};
