#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Data/DeityRow.h"
#include "DeityRegistrySubsystem.generated.h"

/**
 * 게임 시작 시 신 테이블을 읽고 검증한 뒤, 읽기 전용 조회 API를 제공한다.
 * 검증에 실패하면 아무 데이터도 노출하지 않는다 (IsReady() == false).
 */
UCLASS()
class JISIK_API UDeityRegistrySubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	UFUNCTION(BlueprintPure, Category = "Jisik|Deity")
	bool IsReady() const { return bIsReady; }

	/** 찾으면 true와 함께 OutDeity를 채운다. */
	UFUNCTION(BlueprintCallable, Category = "Jisik|Deity")
	bool FindDeity(FName DeityId, FDeityRow& OutDeity) const;

	/** 교과를 맡은 권속신 ID. 없거나 None이면 NAME_None. */
	UFUNCTION(BlueprintPure, Category = "Jisik|Deity")
	FName FindDeityIdBySubject(ESubject Subject) const;

	/** 해당 위계의 신 ID 목록 (이름순 정렬). */
	UFUNCTION(BlueprintPure, Category = "Jisik|Deity")
	TArray<FName> GetDeityIdsByRank(EDeityRank Rank) const;

private:
	/** 성공 시에만 OutDeities를 채운다. */
	static bool LoadAndValidate(TMap<FName, FDeityRow>& OutDeities);

	TMap<FName, FDeityRow> Deities;
	bool bIsReady = false;
};
