#pragma once

#include "CoreMinimal.h"
#include "Data/DeityRow.h"

/**
 * 신 테이블의 세계관 규칙 검사만 담당한다.
 *  - 최고신은 정확히 1명이며 담당 교과가 없다.
 *  - 모든 교과는 정확히 1명의 권속신이 맡는다.
 *  - 모든 신은 표시 이름이 있다.
 */
struct JISIK_API FDeityTableValidator
{
	/** 규칙을 모두 만족하면 true. 위반 사항은 OutErrors 뒤에 추가된다. */
	static bool Validate(const TMap<FName, FDeityRow>& Rows, TArray<FString>& OutErrors);
};
