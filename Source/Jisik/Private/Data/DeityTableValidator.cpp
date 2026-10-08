#include "Data/DeityTableValidator.h"

bool FDeityTableValidator::Validate(const TMap<FName, FDeityRow>& Rows, TArray<FString>& OutErrors)
{
	const int32 ErrorCountBefore = OutErrors.Num();

	if (Rows.IsEmpty())
	{
		OutErrors.Add(TEXT("Deity table is empty."));
		return false;
	}

	int32 SupremeCount = 0;
	TMap<ESubject, int32> SubjectCounts;

	for (const TPair<FName, FDeityRow>& Pair : Rows)
	{
		const FName& Id = Pair.Key;
		const FDeityRow& Row = Pair.Value;

		if (Row.DisplayName.IsEmptyOrWhitespace())
		{
			OutErrors.Add(FString::Printf(TEXT("[%s] DisplayName is empty."), *Id.ToString()));
		}

		switch (Row.Rank)
		{
		case EDeityRank::Supreme:
			++SupremeCount;
			if (Row.Subject != ESubject::None)
			{
				OutErrors.Add(FString::Printf(TEXT("[%s] Supreme deity must not have a subject."), *Id.ToString()));
			}
			break;

		case EDeityRank::Kin:
			if (Row.Subject == ESubject::None || Row.Subject == ESubject::Count)
			{
				OutErrors.Add(FString::Printf(TEXT("[%s] Kin deity must have a valid subject."), *Id.ToString()));
			}
			else
			{
				++SubjectCounts.FindOrAdd(Row.Subject);
			}
			break;

		default:
			OutErrors.Add(FString::Printf(TEXT("[%s] Unknown rank."), *Id.ToString()));
			break;
		}
	}

	if (SupremeCount != 1)
	{
		OutErrors.Add(FString::Printf(TEXT("Expected exactly 1 supreme deity, found %d."), SupremeCount));
	}

	const UEnum* SubjectEnum = StaticEnum<ESubject>();
	for (uint8 Value = static_cast<uint8>(ESubject::None) + 1; Value < static_cast<uint8>(ESubject::Count); ++Value)
	{
		const ESubject Subject = static_cast<ESubject>(Value);
		const int32 Count = SubjectCounts.FindRef(Subject);
		if (Count != 1)
		{
			OutErrors.Add(FString::Printf(TEXT("Subject %s must have exactly 1 kin deity, found %d."),
				*SubjectEnum->GetNameStringByValue(Value), Count));
		}
	}

	return OutErrors.Num() == ErrorCountBefore;
}
