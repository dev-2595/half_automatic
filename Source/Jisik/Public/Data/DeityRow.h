#pragma once

#include "CoreMinimal.h"
#include "Engine/DataTable.h"
#include "Data/JisikTypes.h"
#include "DeityRow.generated.h"

/**
 * 신 정적 데이터 한 행. Row Name이 신의 고유 ID다 (예: GOD_MATH).
 * 원본 데이터: Data/Deities.csv
 */
USTRUCT(BlueprintType)
struct JISIK_API FDeityRow : public FTableRowBase
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Deity")
	FText DisplayName;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Deity")
	EDeityRank Rank = EDeityRank::Kin;

	/**
	 * 담당 교과. 내부 데이터이며 플레이어에게 노출하지 않는다.
	 * 최고신은 None, 권속신은 담당 교과.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Deity")
	ESubject Subject = ESubject::None;

	/** true면 외형을 실루엣으로만 보여준다 (기본값). 스토리상 정체가 드러나는 시점에 false로 바꾼다. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Deity")
	bool bSilhouetteOnly = true;
};
