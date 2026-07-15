using Microsoft.Extensions.Options;
using Nightfall.Api.Auth;

namespace Nightfall.Tests;

// Fixtures generated independently with Node.js `crypto` (not this codebase) implementing
// Telegram's documented algorithm from scratch, to avoid a from-memory transcription bug in the
// C# port going undetected by a test written from the same (possibly wrong) understanding.
public class TelegramInitDataValidatorTests
{
    private const string BotToken = "123456789:AAFakeTestTokenForGoldenFixtureOnly1234";

    private const string ValidInitData =
        "query_id=AAHdF6IQAAAAAN0XohDhrOrc&user=%7B%22id%22%3A987654321%2C%22first_name%22%3A%22Ada%22%2C%22last_name%22%3A%22Lovelace%22%2C%22username%22%3A%22ada_lovelace%22%2C%22language_code%22%3A%22en%22%7D&auth_date=1735689600&hash=d8b434cfb4f4cf0bcc9b05d67fa496b5d8199b4b05a169a006319b5f47b008ec";

    // Same hash as ValidInitData but with the user id changed -> hash no longer matches.
    private const string TamperedInitData =
        "query_id=AAHdF6IQAAAAAN0XohDhrOrc&user=%7B%22id%22%3A987654322%2C%22first_name%22%3A%22Ada%22%2C%22last_name%22%3A%22Lovelace%22%2C%22username%22%3A%22ada_lovelace%22%2C%22language_code%22%3A%22en%22%7D&auth_date=1735689600&hash=d8b434cfb4f4cf0bcc9b05d67fa496b5d8199b4b05a169a006319b5f47b008ec";

    // Correctly signed, but auth_date is from 2020 -> rejected on freshness.
    private const string ExpiredInitData =
        "query_id=AAHdF6IQAAAAAN0XohDhrOrc&user=%7B%22id%22%3A987654321%2C%22first_name%22%3A%22Ada%22%2C%22last_name%22%3A%22Lovelace%22%2C%22username%22%3A%22ada_lovelace%22%2C%22language_code%22%3A%22en%22%7D&auth_date=1600000000&hash=6d3af949bec925c8eb68772b90d21f7e1ca2017cb57f13d29a0e25b8a612ed10";

    private static TelegramInitDataValidator CreateValidator(TimeSpan? maxAge = null)
    {
        var options = new TelegramAuthOptions
        {
            BotToken = BotToken,
            MaxInitDataAge = maxAge ?? TimeSpan.FromDays(999_999) // effectively "ignore freshness" for the base golden test
        };
        return new TelegramInitDataValidator(Options.Create(options));
    }

    [Fact]
    public void TryValidate_CorrectlySignedInitData_AcceptsAndParsesUser()
    {
        var validator = CreateValidator();

        bool isValid = validator.TryValidate(ValidInitData, out var user);

        Assert.True(isValid);
        Assert.NotNull(user);
        Assert.Equal(987654321, user!.Id);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.Equal("ada_lovelace", user.Username);
        Assert.Equal("en", user.LanguageCode);
    }

    [Fact]
    public void TryValidate_TamperedPayload_Rejected()
    {
        var validator = CreateValidator();

        bool isValid = validator.TryValidate(TamperedInitData, out var user);

        Assert.False(isValid);
        Assert.Null(user);
    }

    [Fact]
    public void TryValidate_WrongBotToken_Rejected()
    {
        var options = new TelegramAuthOptions { BotToken = "999:wrong-token", MaxInitDataAge = TimeSpan.FromDays(999_999) };
        var validator = new TelegramInitDataValidator(Options.Create(options));

        bool isValid = validator.TryValidate(ValidInitData, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void TryValidate_ExpiredAuthDate_RejectedOnFreshness()
    {
        // Correctly signed for its own (old) auth_date, but outside the configured max age.
        var validator = CreateValidator(maxAge: TimeSpan.FromHours(24));

        bool isValid = validator.TryValidate(ExpiredInitData, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void TryValidate_WithinFreshnessWindow_Accepted()
    {
        // Same expired-by-default fixture, but with a max age generous enough to cover its auth_date.
        var validator = CreateValidator(maxAge: TimeSpan.FromDays(999_999));

        bool isValid = validator.TryValidate(ExpiredInitData, out var user);

        Assert.True(isValid);
        Assert.NotNull(user);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not=a&valid=initdata")]
    [InlineData("hash=deadbeef")]
    public void TryValidate_MalformedInput_RejectedWithoutThrowing(string malformed)
    {
        var validator = CreateValidator();

        var exception = Record.Exception(() => validator.TryValidate(malformed, out _));

        Assert.Null(exception);
        Assert.False(validator.TryValidate(malformed, out var user));
        Assert.Null(user);
    }
}
