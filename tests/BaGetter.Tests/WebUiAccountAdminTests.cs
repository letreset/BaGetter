using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core.Authentication;
using BaGetter.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BaGetter.Tests;

/// <summary>
/// Administrator actions on Admin > Accounts.
/// </summary>
public class WebUiAccountAdminTests
{
    public abstract class FactsBase : IDisposable
    {
        protected const string Password = "LocalPassword123!";
        protected const string NewPassword = "BrandNewPassword456!";

        protected readonly BaGetterApplication _app;

        protected FactsBase(ITestOutputHelper output)
        {
            _app = new BaGetterApplication(output, null, dict =>
            {
                dict["Authentication:Mode"] = "Local";
            });
        }

        public void Dispose()
        {
            _app.Dispose();
        }
    }

    public class ResetPassword : FactsBase
    {
        public ResetPassword(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task SetsNewPasswordAndEndsLockout()
        {
            await WebUiSession.SeedLocalUserAsync(_app, "admin", Password, isAdmin: true);
            var bob = await WebUiSession.SeedLocalUserAsync(_app, "bob", Password);
            using (var scope = _app.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<IUserService>();
                for (var i = 0; i < 5; i++)
                {
                    await users.RecordFailedLoginAsync(bob.Id, CancellationToken.None);
                }
            }

            using var admin = await WebUiSession.SignInAsync(_app, "admin", Password);
            Assert.Contains("handler=ResetPassword", await admin.GetStringAsync("/Admin/Accounts"));

            using var response = await admin.PostFormAsync("/Admin/Accounts", "ResetPassword", new Dictionary<string, string>
            {
                { "userId", bob.Id.ToString() },
                { "newPassword", NewPassword },
            });
            Assert.Contains("reset successfully", await response.Content.ReadAsStringAsync());

            using var bobSession = await WebUiSession.SignInAsync(_app, "bob", NewPassword);
        }

        [Fact]
        public async Task RejectsShortPassword()
        {
            await WebUiSession.SeedLocalUserAsync(_app, "admin", Password, isAdmin: true);
            var bob = await WebUiSession.SeedLocalUserAsync(_app, "bob", Password);

            using var admin = await WebUiSession.SignInAsync(_app, "admin", Password);
            using var response = await admin.PostFormAsync("/Admin/Accounts", "ResetPassword", new Dictionary<string, string>
            {
                { "userId", bob.Id.ToString() },
                { "newPassword", "short" },
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("at least 12 characters", await response.Content.ReadAsStringAsync());
            using var bobSession = await WebUiSession.SignInAsync(_app, "bob", Password);
        }
    }
}
