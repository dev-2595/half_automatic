#include "Misc/AutomationTest.h"
#include "Data/DeityTableValidator.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace JisikDeityTest
{
	static FDeityRow MakeRow(const TCHAR* DisplayName, EDeityRank Rank, ESubject Subject)
	{
		FDeityRow Row;
		Row.DisplayName = FText::FromString(DisplayName);
		Row.Rank = Rank;
		Row.Subject = Subject;
		return Row;
	}

	/** Data/Deities.csv 와 같은 구성 (표시 이름은 영문 자리표시자). */
	static TMap<FName, FDeityRow> MakeValidRows()
	{
		TMap<FName, FDeityRow> Rows;
		Rows.Add(TEXT("GOD_KNOWLEDGE"),   MakeRow(TEXT("Knowledge"),   EDeityRank::Supreme, ESubject::None));
		Rows.Add(TEXT("GOD_LIFE"),      MakeRow(TEXT("Life"),      EDeityRank::Kin, ESubject::Korean));
		Rows.Add(TEXT("GOD_HARMONY"),     MakeRow(TEXT("Harmony"),     EDeityRank::Kin, ESubject::English));
		Rows.Add(TEXT("GOD_SPACE"),        MakeRow(TEXT("Space"),        EDeityRank::Kin, ESubject::Math));
		Rows.Add(TEXT("GOD_REASON"),     MakeRow(TEXT("Reason"),     EDeityRank::Kin, ESubject::Science));
		Rows.Add(TEXT("GOD_JUSTICE"),      MakeRow(TEXT("Justice"),      EDeityRank::Kin, ESubject::Society));
		Rows.Add(TEXT("GOD_DREAM"), MakeRow(TEXT("Dream"), EDeityRank::Kin, ESubject::Informatics));
		Rows.Add(TEXT("GOD_TIME"),     MakeRow(TEXT("Time"),     EDeityRank::Kin, ESubject::History));
		Rows.Add(TEXT("GOD_EMOTION"),   MakeRow(TEXT("Emotion"),   EDeityRank::Kin, ESubject::Music));
		return Rows;
	}
}

// UE 5.4 (namespace enum)와 5.5+ (enum class) 모두에서 컴파일되도록 매크로로 둔다.
#define JisikTestFlags (EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorAcceptsValid, "Jisik.Data.DeityTableValidator.AcceptsValidTable", JisikTestFlags)
bool FDeityValidatorAcceptsValid::RunTest(const FString& Parameters)
{
	TArray<FString> Errors;
	TestTrue(TEXT("Valid table passes"), FDeityTableValidator::Validate(JisikDeityTest::MakeValidRows(), Errors));
	TestEqual(TEXT("No errors"), Errors.Num(), 0);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorRejectsEmpty, "Jisik.Data.DeityTableValidator.RejectsEmptyTable", JisikTestFlags)
bool FDeityValidatorRejectsEmpty::RunTest(const FString& Parameters)
{
	TArray<FString> Errors;
	TestFalse(TEXT("Empty table fails"), FDeityTableValidator::Validate({}, Errors));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorRejectsMissingSubject, "Jisik.Data.DeityTableValidator.RejectsMissingSubject", JisikTestFlags)
bool FDeityValidatorRejectsMissingSubject::RunTest(const FString& Parameters)
{
	TMap<FName, FDeityRow> Rows = JisikDeityTest::MakeValidRows();
	Rows.Remove(TEXT("GOD_SPACE"));

	TArray<FString> Errors;
	TestFalse(TEXT("Missing Math deity fails"), FDeityTableValidator::Validate(Rows, Errors));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorRejectsDuplicateSubject, "Jisik.Data.DeityTableValidator.RejectsDuplicateSubject", JisikTestFlags)
bool FDeityValidatorRejectsDuplicateSubject::RunTest(const FString& Parameters)
{
	TMap<FName, FDeityRow> Rows = JisikDeityTest::MakeValidRows();
	Rows.Add(TEXT("GOD_SPACE_2"), JisikDeityTest::MakeRow(TEXT("Math 2"), EDeityRank::Kin, ESubject::Math));

	TArray<FString> Errors;
	TestFalse(TEXT("Two Math deities fail"), FDeityTableValidator::Validate(Rows, Errors));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorRejectsSecondSupreme, "Jisik.Data.DeityTableValidator.RejectsSecondSupreme", JisikTestFlags)
bool FDeityValidatorRejectsSecondSupreme::RunTest(const FString& Parameters)
{
	TMap<FName, FDeityRow> Rows = JisikDeityTest::MakeValidRows();
	Rows.Add(TEXT("GOD_OTHER"), JisikDeityTest::MakeRow(TEXT("Other"), EDeityRank::Supreme, ESubject::None));

	TArray<FString> Errors;
	TestFalse(TEXT("Two supreme deities fail"), FDeityTableValidator::Validate(Rows, Errors));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorRejectsSupremeWithSubject, "Jisik.Data.DeityTableValidator.RejectsSupremeWithSubject", JisikTestFlags)
bool FDeityValidatorRejectsSupremeWithSubject::RunTest(const FString& Parameters)
{
	TMap<FName, FDeityRow> Rows = JisikDeityTest::MakeValidRows();
	Rows[TEXT("GOD_KNOWLEDGE")].Subject = ESubject::Math;

	TArray<FString> Errors;
	TestFalse(TEXT("Supreme deity with subject fails"), FDeityTableValidator::Validate(Rows, Errors));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorRejectsEmptyName, "Jisik.Data.DeityTableValidator.RejectsEmptyDisplayName", JisikTestFlags)
bool FDeityValidatorRejectsEmptyName::RunTest(const FString& Parameters)
{
	TMap<FName, FDeityRow> Rows = JisikDeityTest::MakeValidRows();
	Rows[TEXT("GOD_TIME")].DisplayName = FText::GetEmpty();

	TArray<FString> Errors;
	TestFalse(TEXT("Empty display name fails"), FDeityTableValidator::Validate(Rows, Errors));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FDeityValidatorKeepsPriorErrors, "Jisik.Data.DeityTableValidator.KeepsPriorErrors", JisikTestFlags)
bool FDeityValidatorKeepsPriorErrors::RunTest(const FString& Parameters)
{
	TArray<FString> Errors = { TEXT("unrelated earlier error") };
	TestTrue(TEXT("Valid table passes even with prior errors"), FDeityTableValidator::Validate(JisikDeityTest::MakeValidRows(), Errors));
	TestEqual(TEXT("Prior errors untouched"), Errors.Num(), 1);
	return true;
}

#undef JisikTestFlags

#endif // WITH_DEV_AUTOMATION_TESTS
