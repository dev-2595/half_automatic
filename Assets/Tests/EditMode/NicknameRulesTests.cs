using HalfAutomatic.Core;
using NUnit.Framework;

namespace HalfAutomatic.Tests
{
    public class NicknameRulesTests
    {
        [TestCase(null, ErrorCode.NicknameEmpty)]
        [TestCase("", ErrorCode.NicknameEmpty)]
        [TestCase("   ", ErrorCode.NicknameEmpty)]
        [TestCase("a", ErrorCode.NicknameTooShort)]
        [TestCase("가", ErrorCode.NicknameTooShort)]
        [TestCase("1234567890123", ErrorCode.NicknameTooLong)]
        [TestCase("ab cd", ErrorCode.NicknameInvalidChar)]
        [TestCase("ab!", ErrorCode.NicknameInvalidChar)]
        [TestCase("ab😀", ErrorCode.NicknameInvalidChar)]
        public void Rejects_invalid(string raw, ErrorCode expected)
        {
            Result<string> result = NicknameRules.Validate(raw);
            Assert.IsFalse(result.IsOk);
            Assert.AreEqual(expected, result.Error);
        }

        [TestCase("홍길동", "홍길동")]
        [TestCase("  Tom_1  ", "Tom_1")]
        [TestCase("12", "12")]
        [TestCase("123456789012", "123456789012")]
        public void Accepts_and_trims(string raw, string expected)
        {
            Result<string> result = NicknameRules.Validate(raw);
            Assert.IsTrue(result.IsOk);
            Assert.AreEqual(expected, result.Value);
        }
    }
}
