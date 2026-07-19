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

    /** Find the first actor whose name starts with BP_MHC_Victoria in the current world. */
    UFUNCTION(BlueprintCallable, Category = "HouseVictoria|Bridge")
    static AActor* FindVictoriaAvatar();

    /** Teleport the Victoria avatar to an absolute world location. */
    UFUNCTION(BlueprintCallable, Category = "HouseVictoria|Bridge")
    static bool SetVictoriaAvatarLocation(AActor* Avatar, FVector NewLocation);

    /** Set the Victoria avatar's rotation. */
    UFUNCTION(BlueprintCallable, Category = "HouseVictoria|Bridge")
    static bool SetVictoriaAvatarRotation(AActor* Avatar, FRotator NewRotation);

    /** Relative offset move. */
    UFUNCTION(BlueprintCallable, Category = "HouseVictoria|Bridge")
    static bool MoveVictoriaAvatarBy(AActor* Avatar, FVector Offset);
};
