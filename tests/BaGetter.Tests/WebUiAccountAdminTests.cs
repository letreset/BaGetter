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

    public class Create : FactsBase
    {
        public Create(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task RejectsUsernameThatDiffersOnlyInCase()
        {
            await WebUiSession.SeedLocalUserAsync(_app, "admin", Password, isAdmin: true);
            await WebUiSession.SeedLocalUserAsync(_app, "alice", Password);

            using var admin = await WebUiSession.SignInAsync(_app, "admin", Password);
            using var response = await admin.PostFormAsync("/Admin/Accounts", "Create", new Dictionary<string, string>
            {
                { "NewUsername", "ALICE" },
                { "NewPassword", NewPassword },
            });

            Assert.Contains("already exists", await response.Content.ReadAsStringAsync());
            using var scope = _app.Services.CreateScope();
            var users = await scope.ServiceProvider.GetRequiredService<IUserService>().GetAllUsersAsync(CancellationToken.None);
            Assert.DoesNotContain(users, u => u.Username == "ALICE");
        }

        [Fact]
        public async Task SignInIgnoresUsernameCase()
        {
            await WebUiSession.SeedLocalUserAsync(_app, "alice", Password);

            using var session = await WebUiSession.SignInAsync(_app, "Alice", Password);
        }
    }

    public class DeleteConfirmation : FactsBase
    {
        public DeleteConfirmation(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task PutsUsernameIntoAnAttributeInsteadOfScript()
        {
            const string username = "x'+(document.title='pwned')+'\"<\\";
            await WebUiSession.SeedLocalUserAsync(_app, "admin", Password, isAdmin: true);
            var target = await WebUiSession.SeedLocalUserAsync(_app, username, Password);
            using (var scope = _app.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<IUserService>()
                    .SetEnabledAsync(target.Id, false, CancellationToken.None);
            }

            using var admin = await WebUiSession.SignInAsync(_app, "admin", Password);
            var body = await admin.GetStringAsync("/Admin/Accounts");

            Assert.DoesNotContain("onsubmit=", body);
            Assert.Contains(
                "data-confirm=\"Are you sure you want to permanently delete the account &#x27;x&#x27;&#x2B;(document.title=&#x27;pwned&#x27;)&#x2B;&#x27;&quot;&lt;\\&#x27;?",
                body);
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
