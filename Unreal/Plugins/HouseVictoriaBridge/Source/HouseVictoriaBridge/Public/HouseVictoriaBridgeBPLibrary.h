#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HouseVictoriaParsedMessage.h"
#include "HouseVictoriaBridgeBPLibrary.generated.h"

UCLASS()
class HOUSEVICTORIABRIDGE_API UHouseVictoriaBridgeBPLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    /**
     * Parse one inbound UTF-8 text frame from House Victoria.
     * Supports JSON (companion_remote_exchange, status, scene_update) and plain control lines (status, move_avatar, …).
     */
    UFUNCTION(BlueprintCallable, Category = "HouseVictoria|Bridge", meta = (DisplayName = "Parse Web Socket Message"))
    static bool ParseWebSocketMessage(const FString& RawText, FHouseVictoriaParsedMessage& OutMessage);
};
