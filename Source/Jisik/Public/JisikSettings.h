#pragma once

#include "CoreMinimal.h"
#include "Engine/DataTable.h"
#include "Engine/DeveloperSettings.h"
#include "JisikSettings.generated.h"

/** 프로젝트 설정 > Game > Jisik. 값은 Config/DefaultGame.ini에 저장된다. */
UCLASS(Config = Game, DefaultConfig, meta = (DisplayName = "Jisik"))
class JISIK_API UJisikSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	/** Row Struct가 FDeityRow인 DataTable. */
	UPROPERTY(Config, EditAnywhere, Category = "Data", meta = (RequiredAssetDataTags = "RowStructure=/Script/Jisik.DeityRow"))
	TSoftObjectPtr<UDataTable> DeityTable;
};
