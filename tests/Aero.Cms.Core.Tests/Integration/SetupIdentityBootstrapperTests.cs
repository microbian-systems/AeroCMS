using Aero.Cms.Modules.Setup;
using Aero.Core.Identity;
using Aero.Models.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Core.Tests.Integration;

public class SetupIdentityBootstrapperTests
{
    [Test]
    public async Task Bootstrap_creates_required_roles_and_first_admin_with_hashed_password()
    {
        var harness = new IdentityHarness();

        var result = await harness.Bootstrapper.BootstrapAsync(new SetupIdentityBootstrapRequest(
            "admin.user",
            "admin@example.com",
            "CorrectHorseBattery1!"));

        result.Succeeded.Should().BeTrue();
        result.CreatedAdmin.Should().BeTrue();
        result.CreatedRoles.Should().BeTrue();
        harness.RoleStore.Roles.Select(role => role.Name).Should().BeEquivalentTo(AeroCmsRoles.All);

        var admin = harness.UserStore.Users.Should().ContainSingle().Subject;
        admin.PasswordHash.Should().NotBeNullOrWhiteSpace();
        admin.PasswordHash.Should().NotBe("CorrectHorseBattery1!");
        harness.PasswordHasher.VerifyHashedPassword(admin, admin.PasswordHash!, "CorrectHorseBattery1!")
            .Should().NotBe(PasswordVerificationResult.Failed);
        (await harness.UserManager.IsInRoleAsync(admin, AeroCmsRoles.Admin)).Should().BeTrue();
    }

    [Test]
    public async Task Bootstrap_is_idempotent_for_roles_and_admin_user()
    {
        var harness = new IdentityHarness();

        var firstResult = await harness.Bootstrapper.BootstrapAsync(new SetupIdentityBootstrapRequest(
            "admin.user",
            "admin@example.com",
            "CorrectHorseBattery1!"));

        var secondResult = await harness.Bootstrapper.BootstrapAsync(new SetupIdentityBootstrapRequest(
            "different.admin",
            "different@example.com",
            "CorrectHorseBattery1!"));

        firstResult.Succeeded.Should().BeTrue();
        secondResult.Succeeded.Should().BeTrue();
        secondResult.CreatedAdmin.Should().BeFalse();
        secondResult.CreatedRoles.Should().BeFalse();
        harness.RoleStore.Roles.Should().HaveCount(AeroCmsRoles.All.Count);
        harness.UserStore.Users.Should().ContainSingle();
        (await harness.UserManager.GetUsersInRoleAsync(AeroCmsRoles.Admin)).Should().ContainSingle();
    }

    private sealed class IdentityHarness
    {
        public IdentityHarness()
        {
            UserStore = new InMemoryUserStore();
            RoleStore = new InMemoryRoleStore();
            PasswordHasher = new PasswordHasher<AeroUser>();

            var options = Options.Create(new IdentityOptions
            {
                User = { RequireUniqueEmail = true },
                Password =
                {
                    RequiredLength = 12,
                    RequireDigit = false,
                    RequireLowercase = false,
                    RequireUppercase = false,
                    RequireNonAlphanumeric = false,
                    RequiredUniqueChars = 1
                }
            });

            var lookupNormalizer = new UpperInvariantLookupNormalizer();
            var identityErrorDescriber = new IdentityErrorDescriber();

            UserManager = new UserManager<AeroUser>(
                UserStore,
                options,
                PasswordHasher,
                [new UserValidator<AeroUser>()],
                [new PasswordValidator<AeroUser>()],
                lookupNormalizer,
                identityErrorDescriber,
                new ServiceCollection().BuildServiceProvider(),
                NullLogger<UserManager<AeroUser>>.Instance);

            RoleManager = new RoleManager<AeroRole>(
                RoleStore,
                [new RoleValidator<AeroRole>()],
                lookupNormalizer,
                identityErrorDescriber,
                NullLogger<RoleManager<AeroRole>>.Instance);

            // Bootstrapper = new SetupIdentityBootstrapper(UserManager, RoleManager);
            Bootstrapper = new SetupIdentityBootstrapper(UserManager);
        }

        public SetupIdentityBootstrapper Bootstrapper { get; }
        public InMemoryUserStore UserStore { get; }
        public InMemoryRoleStore RoleStore { get; }
        public PasswordHasher<AeroUser> PasswordHasher { get; }
        public UserManager<AeroUser> UserManager { get; }
        public RoleManager<AeroRole> RoleManager { get; }
    }

    private sealed class InMemoryUserStore :
        IUserEmailStore<AeroUser>,
        IUserPasswordStore<AeroUser>,
        IUserRoleStore<AeroUser>
    {
        private readonly Dictionary<long, AeroUser> _users = [];
        private readonly Dictionary<long, HashSet<string>> _rolesByUser = [];
        private long _nextId = 1;

        public IReadOnlyCollection<AeroUser> Users => _users.Values;

        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(AeroUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(AeroUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(AeroUser user, CancellationToken cancellationToken)
        {
            if (user.Id == 0)
            {
                user.Id = _nextId++;
            }

            _users[user.Id] = user;
            _rolesByUser.TryAdd(user.Id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(AeroUser user, CancellationToken cancellationToken)
        {
            _users[user.Id] = user;
            _rolesByUser.TryAdd(user.Id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(AeroUser user, CancellationToken cancellationToken)
        {
            _users.Remove(user.Id);
            _rolesByUser.Remove(user.Id);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<AeroUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult(long.TryParse(userId, out var id) && _users.TryGetValue(id, out var user) ? user : null);

        public Task<AeroUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult(_users.Values.FirstOrDefault(user => user.NormalizedUserName == normalizedUserName));

        public Task SetEmailAsync(AeroUser user, string? email, CancellationToken cancellationToken)
        {
            user.Email = email;
            return Task.CompletedTask;
        }

        public Task<string?> GetEmailAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Email);

        public Task<bool> GetEmailConfirmedAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.EmailConfirmed);

        public Task SetEmailConfirmedAsync(AeroUser user, bool confirmed, CancellationToken cancellationToken)
        {
            user.EmailConfirmed = confirmed;
            return Task.CompletedTask;
        }

        public Task<AeroUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(_users.Values.FirstOrDefault(user => user.NormalizedEmail == normalizedEmail));

        public Task<string?> GetNormalizedEmailAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedEmail);

        public Task SetNormalizedEmailAsync(AeroUser user, string? normalizedEmail, CancellationToken cancellationToken)
        {
            user.NormalizedEmail = normalizedEmail;
            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(AeroUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.PasswordHash);

        public Task<bool> HasPasswordAsync(AeroUser user, CancellationToken cancellationToken)
            => Task.FromResult(!string.IsNullOrWhiteSpace(user.PasswordHash));

        public Task AddToRoleAsync(AeroUser user, string roleName, CancellationToken cancellationToken)
        {
            var roles = _rolesByUser.TryGetValue(user.Id, out var assignedRoles)
                ? assignedRoles
                : _rolesByUser[user.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            roles.Add(roleName);
            return Task.CompletedTask;
        }

        public Task RemoveFromRoleAsync(AeroUser user, string roleName, CancellationToken cancellationToken)
        {
            if (_rolesByUser.TryGetValue(user.Id, out var assignedRoles))
            {
                assignedRoles.Remove(roleName);
            }

            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(AeroUser user, CancellationToken cancellationToken)
        {
            IList<string> roles = _rolesByUser.TryGetValue(user.Id, out var assignedRoles)
                ? assignedRoles.ToList()
                : [];

            return Task.FromResult(roles);
        }

        public Task<bool> IsInRoleAsync(AeroUser user, string roleName, CancellationToken cancellationToken)
            => Task.FromResult(_rolesByUser.TryGetValue(user.Id, out var assignedRoles) && assignedRoles.Contains(roleName));

        public Task<IList<AeroUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            IList<AeroUser> users = _users.Values
                .Where(user => _rolesByUser.TryGetValue(user.Id, out var assignedRoles) && assignedRoles.Contains(roleName))
                .ToList();

            return Task.FromResult(users);
        }
    }

    private sealed class InMemoryRoleStore : IRoleStore<AeroRole>
    {
        private readonly Dictionary<long, AeroRole> _roles = [];

        public IReadOnlyCollection<AeroRole> Roles => _roles.Values;

        public void Dispose()
        {
        }

        public Task<IdentityResult> CreateAsync(AeroRole role, CancellationToken cancellationToken)
        {
            _roles[role.Id] = role;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(AeroRole role, CancellationToken cancellationToken)
        {
            _roles[role.Id] = role;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(AeroRole role, CancellationToken cancellationToken)
        {
            _roles.Remove(role.Id);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<string> GetRoleIdAsync(AeroRole role, CancellationToken cancellationToken)
            => Task.FromResult(role.Id.ToString());

        public Task<string?> GetRoleNameAsync(AeroRole role, CancellationToken cancellationToken)
            => Task.FromResult(role.Name);

        public Task SetRoleNameAsync(AeroRole role, string? roleName, CancellationToken cancellationToken)
        {
            role.Name = roleName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedRoleNameAsync(AeroRole role, CancellationToken cancellationToken)
            => Task.FromResult(role.NormalizedName);

        public Task SetNormalizedRoleNameAsync(AeroRole role, string? normalizedName, CancellationToken cancellationToken)
        {
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<AeroRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
            => Task.FromResult(long.TryParse(roleId, out var id) && _roles.TryGetValue(id, out var role) ? role : null);

        public Task<AeroRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
            => Task.FromResult(_roles.Values.FirstOrDefault(role => role.NormalizedName == normalizedRoleName));
    }
}
