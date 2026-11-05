#pragma once

#include "CoreMinimal.h"
#include "JisikTypes.generated.h"

/** 교과. 8 권속신이 하나씩 맡는다. */
UENUM(BlueprintType)
enum class ESubject : uint8
{
	None,
	Korean,
	English,
	Math,
	Science,
	Music,
	Informatics,
	History,
	Society,

	Count UMETA(Hidden)
};

/** 신의 위계. */
UENUM(BlueprintType)
enum class EDeityRank : uint8
{
	/** 최고신 (지식의 신) */
	Supreme,
	/** 권속신 (8신) */
	Kin
};
