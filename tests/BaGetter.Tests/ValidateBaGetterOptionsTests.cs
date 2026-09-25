using System.Linq;
using BaGetter.Core.Configuration;
using Xunit;

namespace BaGetter.Tests;

public class ValidateBaGetterOptionsTests
{
    public class ValidateEmail
    {
        private static bool HasEmailTypeFailure(BaGetterOptions options)
        {
            var result = new ValidateBaGetterOptions().Validate(null, options);
            return result.Failed
                && result.Failures.Any(f => f.Contains($"{nameof(BaGetterOptions.Email)}:{nameof(EmailOptions.Type)}"));
        }

        [Theory]
        [InlineData("Smtp")]
        [InlineData("Graph")]
        [InlineData("Null")]
        [InlineData("smtp")] // case-insensitive
        public void AcceptsValidEmailType(string type)
        {
            Assert.False(HasEmailTypeFailure(new BaGetterOptions { Email = new EmailOptions { Type = type } }));
        }

        [Fact]
        public void AcceptsMissingEmailSection()
        {
            Assert.False(HasEmailTypeFailure(new BaGetterOptions()));
        }

        [Fact]
        public void AcceptsEmptyEmailType()
        {
            Assert.False(HasEmailTypeFailure(new BaGetterOptions { Email = new EmailOptions { Type = "" } }));
        }

        [Theory]
        [InlineData("smpt")]
        [InlineData("sendgrid")]
        public void RejectsInvalidEmailType(string type)
        {
            Assert.True(HasEmailTypeFailure(new BaGetterOptions { Email = new EmailOptions { Type = type } }));
        }
    }

    public class ValidateHttp
    {
        private static bool HasFailure(BaGetterOptions options, string key)
        {
            var result = new ValidateBaGetterOptions().Validate(null, options);
            return result.Failed && result.Failures.Any(f => f.Contains(key));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void RejectsRegistrationPageSizeBelowOne(int pageSize)
        {
            Assert.True(HasFailure(new BaGetterOptions { RegistrationPageSize = pageSize }, nameof(BaGetterOptions.RegistrationPageSize)));
        }

        [Fact]
        public void AcceptsDefaultRegistrationPageSize()
        {
            Assert.False(HasFailure(new BaGetterOptions(), nameof(BaGetterOptions.RegistrationPageSize)));
        }

        [Fact]
        public void RejectsCorsCredentialsWithoutOrigins()
        {
            var options = new BaGetterOptions { Cors = new CorsPolicyOptions { AllowCredentials = true } };

            Assert.True(HasFailure(options, $"{nameof(BaGetterOptions.Cors)}:{nameof(CorsPolicyOptions.AllowCredentials)}"));
        }

        [Fact]
        public void AcceptsCorsCredentialsWithOrigins()
        {
            var options = new BaGetterOptions
            {
                Cors = new CorsPolicyOptions { AllowCredentials = true, AllowedOrigins = ["https://portal.example.com"] },
            };

            Assert.False(HasFailure(options, $"{nameof(BaGetterOptions.Cors)}:{nameof(CorsPolicyOptions.AllowCredentials)}"));
        }

        [Fact]
        public void RejectsHstsWithNonPositiveMaxAge()
        {
            var options = new BaGetterOptions { SecurityHeaders = new SecurityHeadersOptions { EnableHsts = true, HstsMaxAgeDays = 0 } };

            Assert.True(HasFailure(options, nameof(SecurityHeadersOptions.HstsMaxAgeDays)));
        }
    }

    public class ValidateRequestRateLimit
    {
        private static bool HasFailure(RequestRateLimitOptions rateLimit, string key)
        {
            var result = new ValidateBaGetterOptions().Validate(null, new BaGetterOptions { RequestRateLimit = rateLimit });
            return result.Failed && result.Failures.Any(f => f.Contains($"{nameof(BaGetterOptions.RequestRateLimit)}:{key}"));
        }

        [Fact]
        public void AcceptsDefaultsWhenEnabled()
        {
            Assert.False(HasFailure(new RequestRateLimitOptions { Enabled = true }, string.Empty));
        }

        [Fact]
        public void IgnoresInvalidValuesWhenDisabled()
        {
            var rateLimit = new RequestRateLimitOptions { PermitLimit = 0, WindowSeconds = 0, QueueLimit = -1 };

            Assert.False(HasFailure(rateLimit, string.Empty));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void RejectsPermitLimitBelowOne(int permitLimit)
        {
            var rateLimit = new RequestRateLimitOptions { Enabled = true, PermitLimit = permitLimit };

            Assert.True(HasFailure(rateLimit, nameof(RequestRateLimitOptions.PermitLimit)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void RejectsWindowSecondsBelowOne(int windowSeconds)
        {
            var rateLimit = new RequestRateLimitOptions { Enabled = true, WindowSeconds = windowSeconds };

            Assert.True(HasFailure(rateLimit, nameof(RequestRateLimitOptions.WindowSeconds)));
        }

        [Fact]
        public void RejectsNegativeQueueLimit()
        {
            var rateLimit = new RequestRateLimitOptions { Enabled = true, QueueLimit = -1 };

            Assert.True(HasFailure(rateLimit, nameof(RequestRateLimitOptions.QueueLimit)));
        }
    }

    public class ValidateInitialAdmin
    {
        private const string ValidPassword = "InitialPassword123!";

        private static bool HasFailure(AuthenticationMode mode, string username, string password)
        {
            var options = new BaGetterOptions
            {
                Authentication = new NugetAuthenticationOptions
                {
                    Mode = mode,
                    InitialAdmin = new InitialAdminOptions { Username = username, Password = password },
                },
            };

            var result = new ValidateBaGetterOptions().Validate(null, options);
            return result.Failed && result.Failures.Any(f => f.Contains(nameof(NugetAuthenticationOptions.InitialAdmin)));
        }

        [Theory]
        [InlineData(AuthenticationMode.Local)]
        [InlineData(AuthenticationMode.Hybrid)]
        public void AcceptsValidSettings(AuthenticationMode mode)
        {
            Assert.False(HasFailure(mode, "root", ValidPassword));
        }

        [Fact]
        public void AcceptsMissingSettings()
        {
            Assert.False(HasFailure(AuthenticationMode.Local, null, null));
        }

        [Fact]
        public void AcceptsMissingSection()
        {
            var options = new BaGetterOptions
            {
                Authentication = new NugetAuthenticationOptions { Mode = AuthenticationMode.Local },
            };

            var result = new ValidateBaGetterOptions().Validate(null, options);

            Assert.False(result.Failed && result.Failures.Any(f => f.Contains(nameof(NugetAuthenticationOptions.InitialAdmin))));
        }

        [Fact]
        public void RejectsUsernameWithoutPassword()
        {
            Assert.True(HasFailure(AuthenticationMode.Local, "root", null));
        }

        [Fact]
        public void RejectsPasswordWithoutUsername()
        {
            Assert.True(HasFailure(AuthenticationMode.Local, null, ValidPassword));
        }

        [Fact]
        public void RejectsShortPassword()
        {
            Assert.True(HasFailure(AuthenticationMode.Local, "root", new string('x', InitialAdminOptions.MinPasswordLength - 1)));
        }

        [Fact]
        public void RejectsLongUsername()
        {
            Assert.True(HasFailure(AuthenticationMode.Local, new string('u', InitialAdminOptions.MaxUsernameLength + 1), ValidPassword));
        }

        [Theory]
        [InlineData(AuthenticationMode.Config)]
        [InlineData(AuthenticationMode.Entra)]
        public void IgnoresSettingsInModesWithoutLocalAccounts(AuthenticationMode mode)
        {
            Assert.False(HasFailure(mode, "root", "short"));
        }
    }
}
