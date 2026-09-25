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
}
