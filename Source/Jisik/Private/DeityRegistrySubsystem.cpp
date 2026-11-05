#include "DeityRegistrySubsystem.h"

#include "Jisik.h"
#include "JisikSettings.h"
#include "Data/DeityTableValidator.h"

void UDeityRegistrySubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);

	TMap<FName, FDeityRow> Loaded;
	bIsReady = LoadAndValidate(Loaded);
	if (bIsReady)
	{
		Deities = MoveTemp(Loaded);
		UE_LOG(LogJisik, Log, TEXT("Deity registry ready: %d deities."), Deities.Num());
	}
}

void UDeityRegistrySubsystem::Deinitialize()
{
	Deities.Reset();
	bIsReady = false;

	Super::Deinitialize();
}

bool UDeityRegistrySubsystem::FindDeity(FName DeityId, FDeityRow& OutDeity) const
{
	if (const FDeityRow* Found = Deities.Find(DeityId))
	{
		OutDeity = *Found;
		return true;
	}
	return false;
}

FName UDeityRegistrySubsystem::FindDeityIdBySubject(ESubject Subject) const
{
	if (Subject == ESubject::None)
	{
		return NAME_None;
	}

	for (const TPair<FName, FDeityRow>& Pair : Deities)
	{
		if (Pair.Value.Rank == EDeityRank::Kin && Pair.Value.Subject == Subject)
		{
			return Pair.Key;
		}
	}
	return NAME_None;
}

TArray<FName> UDeityRegistrySubsystem::GetDeityIdsByRank(EDeityRank Rank) const
{
	TArray<FName> Ids;
	for (const TPair<FName, FDeityRow>& Pair : Deities)
	{
		if (Pair.Value.Rank == Rank)
		{
			Ids.Add(Pair.Key);
		}
	}
	Ids.Sort(FNameLexicalLess());
	return Ids;
}

bool UDeityRegistrySubsystem::LoadAndValidate(TMap<FName, FDeityRow>& OutDeities)
{
	const UJisikSettings* Settings = GetDefault<UJisikSettings>();
	if (!Settings || Settings->DeityTable.IsNull())
	{
		UE_LOG(LogJisik, Error, TEXT("DeityTable is not set in Project Settings > Game > Jisik."));
		return false;
	}

	const UDataTable* Table = Settings->DeityTable.LoadSynchronous();
	if (!Table)
	{
		UE_LOG(LogJisik, Error, TEXT("Failed to load DeityTable: %s"), *Settings->DeityTable.ToString());
		return false;
	}

	const UScriptStruct* RowStruct = Table->GetRowStruct();
	if (!RowStruct || !RowStruct->IsChildOf(FDeityRow::StaticStruct()))
	{
		UE_LOG(LogJisik, Error, TEXT("DeityTable %s must use row struct FDeityRow."), *Table->GetPathName());
		return false;
	}

	TMap<FName, FDeityRow> Loaded;
	Table->ForeachRow<FDeityRow>(TEXT("UDeityRegistrySubsystem::LoadAndValidate"),
		[&Loaded](const FName& Key, const FDeityRow& Row)
		{
			Loaded.Add(Key, Row);
		});

	TArray<FString> Errors;
	if (!FDeityTableValidator::Validate(Loaded, Errors))
	{
		for (const FString& Error : Errors)
		{
			UE_LOG(LogJisik, Error, TEXT("DeityTable %s: %s"), *Table->GetPathName(), *Error);
		}
		return false;
	}

	OutDeities = MoveTemp(Loaded);
	return true;
}
