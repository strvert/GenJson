#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "GenJsonSettings.generated.h"

UCLASS(config = GenJson, defaultconfig)
class UGenJsonSettings : public UDeveloperSettings
{
public:
	GENERATED_BODY()
};
