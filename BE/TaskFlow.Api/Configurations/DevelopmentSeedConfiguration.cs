using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Api.Configurations
{
    // Development-only dev users (CLAUDE.md -> Decided before F01 -> Q-U Users).
    // Never seeded by migration, never run outside the Development environment.
    public static class DevelopmentSeedConfiguration
    {
        private static readonly (string UserName, string Email, string RoleName)[] DevUsers =
        [
            ("member1", "member1@taskflow.local", RolePolicies.Member.Name),
            ("member2", "member2@taskflow.local", RolePolicies.Member.Name),
            ("manager1", "manager1@taskflow.local", RolePolicies.Manager.Name),
        ];

        public static async Task SeedDevelopmentUsersAsync(this WebApplication app)
        {
            var password = app.Configuration.GetSection(ConfigKeys.DevSeed.Password).Get<string>();
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    $"Development user seeding requires '{ConfigKeys.DevSeed.Password}' to be configured.");
            }

            using var scope = app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            foreach (var devUser in DevUsers)
            {
                var existingUser = await userManager.FindByNameAsync(devUser.UserName);
                if (existingUser != null)
                {
                    continue;
                }

                var user = new User
                {
                    UserName = devUser.UserName,
                    Email = devUser.Email,
                    EmailConfirmed = true,
                    LockoutEnabled = true,
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to seed development user '{devUser.UserName}': {errors}");
                }

                var roleRepo = await unitOfWork.GetRepository<Role>().QueryAll();
                var role = await roleRepo.SingleAsync(r => r.Name == devUser.RoleName);

                await unitOfWork.GetRepository<UserRole>().Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                });

                await unitOfWork.SaveChanges();
            }
        }
    }
}
