using System.Security.Cryptography;
using TaskFlow.BusinessLogic.Dtos.Users;
using TaskFlow.BusinessLogic.Storage;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.BusinessLogic.Services
{
    public interface IUserService
    {
        Task<List<UserResDto>> GetList();

        Task<(string ErrorCode, UserAdminResDto? User)> GetDetail(long id);

        Task<(string ErrorCode, UserCreatedResDto? Result)> Create(UserCreateReqDto requestDto);

        Task<(string ErrorCode, UserAdminResDto? User)> ChangeRole(long id, AccountRole role);

        Task<(string ErrorCode, UserAdminResDto? User)> Deactivate(long id, long actorUserId);

        Task<(string ErrorCode, UserAdminResDto? User)> Activate(long id);

        Task<(string ErrorCode, string? TemporaryPassword)> ResetPassword(long id);

        Task<string> RemoveAvatar(long id);

        Task<(string ErrorCode, string? AvatarObjectKey)> GetAvatarObjectKey(long id);

        Task<List<WorkloadResDto>> GetWorkload();
    }

    // f02-capacity.md T4 (sign-off #6): GET /users, the assignee picker's
    // source list. R2-3 (f08-accounts-profile.md "Admin API") adds account
    // administration, policy ManageUsers.
    public class UserService(
        IUnitOfWork unitOfWork,
        UserManager<User> userManager,
        IStorageService storageService,
        IUserPresenceNotifier presenceNotifier) : IUserService
    {
        // Soft-deleted users are excluded by User's existing query filter
        // (!IsDeleted, UserConfiguration) - no IgnoreQueryFilters() here,
        // unlike TaskHistoryService's actor lookups which deliberately read
        // through it. Every user today carries exactly one Role row
        // (DevelopmentSeedConfiguration, the Admin migration seed); the left
        // join below reads that one role per user and, if a user ever had
        // more than one, picks the lowest RoleId deterministically rather
        // than throwing or silently picking an unordered one.
        public async Task<List<UserResDto>> GetList()
        {
            var users = await unitOfWork.GetRepository<User>().QueryAll();
            var userRoles = await unitOfWork.GetRepository<UserRole>().QueryAll();
            var roles = await unitOfWork.GetRepository<Role>().QueryAll();

            var query =
                from u in users
                join ur in userRoles on u.Id equals ur.UserId into urg
                from ur in urg.DefaultIfEmpty()
                join r in roles on ur.RoleId equals r.Id into rg
                from r in rg.DefaultIfEmpty()
                select new UserRoleRow
                {
                    UserId = u.Id,
                    UserName = u.UserName,
                    RoleId = (long?)r.Id,
                    RoleName = r.Name,
                    DisplayName = u.DisplayName,
                    AvatarObjectKey = u.AvatarObjectKey,
                    IsActive = u.DeactivatedAt == null,
                };

            var rows = await query.ToListAsync();

            return rows
                .GroupBy(r => new { r.UserId, r.UserName })
                .Select(g =>
                {
                    var first = g.OrderBy(x => x.RoleId).First();
                    return new UserResDto
                    {
                        Id = g.Key.UserId,
                        UserName = g.Key.UserName ?? string.Empty,
                        Role = first.RoleName ?? string.Empty,
                        DisplayName = first.DisplayName,
                        AvatarUrl = AvatarKeyHelper.BuildUrl(g.Key.UserId, first.AvatarObjectKey),
                        IsActive = first.IsActive,
                    };
                })
                .OrderBy(dto => dto.UserName, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<(string ErrorCode, UserAdminResDto? User)> GetDetail(long id)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(id);
            if (user == null)
            {
                return (UserControllerMS.Get.NotFound, null);
            }

            var dto = await BuildAdminDto(user);
            return (string.Empty, dto);
        }

        // Validate before mutating: uniqueness, then create. A temporary
        // password is server-generated (CSPRNG), returned once, never
        // logged; MustChangePassword forces the holder through the
        // anonymous change-required endpoint before normal login works
        // (f08-accounts-profile.md "Password policy").
        public async Task<(string ErrorCode, UserCreatedResDto? Result)> Create(UserCreateReqDto requestDto)
        {
            var existing = await userManager.FindByNameAsync(requestDto.UserName);
            if (existing != null)
            {
                return (UserControllerMS.Create.UserNameTaken, null);
            }

            var temporaryPassword = GenerateTemporaryPassword();

            var user = new User
            {
                UserName = requestDto.UserName,
                DisplayName = requestDto.DisplayName,
                EmailConfirmed = false,
                LockoutEnabled = true,
                MustChangePassword = true,
            };

            var createResult = await userManager.CreateAsync(user, temporaryPassword);
            if (!createResult.Succeeded)
            {
                return (UserControllerMS.Create.Exception, null);
            }

            var roleRepo = await unitOfWork.GetRepository<Role>().QueryAll();
            var role = await roleRepo.SingleAsync(r => r.Name == requestDto.Role);

            await unitOfWork.GetRepository<UserRole>().Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await unitOfWork.SaveChanges();

            var dto = await BuildAdminDto(user, roleName: requestDto.Role);
            return (string.Empty, new UserCreatedResDto { User = dto, TemporaryPassword = temporaryPassword });
        }

        public async Task<(string ErrorCode, UserAdminResDto? User)> ChangeRole(long id, AccountRole role)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(id);
            if (user == null)
            {
                return (UserControllerMS.ChangeRole.NotFound, null);
            }

            var newRoleName = RoleName(role);

            var userRoleRepo = await unitOfWork.GetRepository<UserRole>().QueryCondition(ur => ur.UserId == id);
            var userRole = await userRoleRepo.SingleOrDefaultAsync();
            if (userRole == null)
            {
                return (UserControllerMS.ChangeRole.NotFound, null);
            }

            var currentRoleName = await CurrentRoleName(id);

            if (currentRoleName == RolePolicies.Admin.Name
                && newRoleName != RolePolicies.Admin.Name
                && await IsLastActiveAdmin(id))
            {
                return (UserControllerMS.ChangeRole.LastAdmin, null);
            }

            var roleRepo = await unitOfWork.GetRepository<Role>().QueryAll();
            var targetRole = await roleRepo.SingleAsync(r => r.Name == newRoleName);

            // UserRole.RoleId is part of its composite primary key
            // (UserId, RoleId) - EF refuses to modify a key property in
            // place, so the old row is deleted and a new one added rather
            // than reassigning RoleId on the tracked entity.
            await unitOfWork.GetRepository<UserRole>().Delete(new List<UserRole> { userRole });
            await unitOfWork.GetRepository<UserRole>().Add(new UserRole { UserId = id, RoleId = targetRole.Id });

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (UserControllerMS.ChangeRole.Conflict, null);
            }

            var dto = await BuildAdminDto(user, roleName: newRoleName);
            return (string.Empty, dto);
        }

        // Idempotent: a second call on an already-deactivated user returns
        // success without touching anything further. Revokes refresh
        // tokens and rotates SecurityStamp - quick revocation of a token
        // already in hand is bounded by its own ≤15 min expiry this round
        // (the Redis deny-list in f08/ADR 0006 was dropped for R2, see
        // handoff); login and refresh are blocked immediately either way.
        public async Task<(string ErrorCode, UserAdminResDto? User)> Deactivate(long id, long actorUserId)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(id);
            if (user == null)
            {
                return (UserControllerMS.Deactivate.NotFound, null);
            }

            if (actorUserId == id)
            {
                return (UserControllerMS.Deactivate.Self, null);
            }

            if (user.DeactivatedAt.HasValue)
            {
                // Idempotent, but still aborts: closes the tiny race window
                // where a connection was established between an earlier
                // deactivation's abort and this one's check.
                presenceNotifier.NotifyDeactivated(id);
                var alreadyDto = await BuildAdminDto(user);
                return (string.Empty, alreadyDto);
            }

            if (await CurrentRoleName(id) == RolePolicies.Admin.Name && await IsLastActiveAdmin(id))
            {
                return (UserControllerMS.Deactivate.LastAdmin, null);
            }

            user.DeactivatedAt = DateTimeHelper.GetDtOffset();

            try
            {
                await userManager.UpdateSecurityStampAsync(user);

                var tokenRepo = await unitOfWork.GetRepository<UserToken>().QueryCondition(t => t.UserId == id, disableTracking: false);
                var tokens = await tokenRepo.ToListAsync();
                if (tokens.Count > 0)
                {
                    await unitOfWork.GetRepository<UserToken>().Delete(tokens);
                }

                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (UserControllerMS.Deactivate.Conflict, null);
            }

            // f08-accounts-profile.md "Quick revocation on deactivation" /
            // f07-presence.md AC9: called here, not by the controller, so
            // the invariant holds for every caller of Deactivate, not only
            // the one HTTP action that exists today.
            presenceNotifier.NotifyDeactivated(id);

            var dto = await BuildAdminDto(user);
            return (string.Empty, dto);
        }

        public async Task<(string ErrorCode, UserAdminResDto? User)> Activate(long id)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(id);
            if (user == null)
            {
                return (UserControllerMS.Activate.NotFound, null);
            }

            if (!user.DeactivatedAt.HasValue)
            {
                var alreadyDto = await BuildAdminDto(user);
                return (string.Empty, alreadyDto);
            }

            user.DeactivatedAt = null;

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (UserControllerMS.Activate.Conflict, null);
            }

            var dto = await BuildAdminDto(user);
            return (string.Empty, dto);
        }

        public async Task<(string ErrorCode, string? TemporaryPassword)> ResetPassword(long id)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(id);
            if (user == null)
            {
                return (UserControllerMS.ResetPassword.NotFound, null);
            }

            var temporaryPassword = GenerateTemporaryPassword();

            var removeResult = await userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                return (UserControllerMS.ResetPassword.Conflict, null);
            }

            var addResult = await userManager.AddPasswordAsync(user, temporaryPassword);
            if (!addResult.Succeeded)
            {
                return (UserControllerMS.ResetPassword.Conflict, null);
            }

            user.MustChangePassword = true;
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);

            var tokenRepo = await unitOfWork.GetRepository<UserToken>().QueryCondition(t => t.UserId == id, disableTracking: false);
            var tokens = await tokenRepo.ToListAsync();
            if (tokens.Count > 0)
            {
                await unitOfWork.GetRepository<UserToken>().Delete(tokens);
            }

            await unitOfWork.SaveChanges();

            return (string.Empty, temporaryPassword);
        }

        public async Task<string> RemoveAvatar(long id)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(id);
            if (user == null)
            {
                return UserControllerMS.RemoveAvatar.NotFound;
            }

            if (string.IsNullOrEmpty(user.AvatarObjectKey))
            {
                return string.Empty;
            }

            var oldKey = user.AvatarObjectKey;
            user.AvatarObjectKey = null;
            await unitOfWork.SaveChanges();

            await storageService.Remove(oldKey);

            return string.Empty;
        }

        public async Task<(string ErrorCode, string? AvatarObjectKey)> GetAvatarObjectKey(long id)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(id);
            if (user == null)
            {
                return (UserControllerMS.Avatar.NotFound, null);
            }

            return (string.Empty, user.AvatarObjectKey);
        }

        // f10-read-models-and-sort.md Section 5: one GROUP BY over
        // non-deleted tasks with an assignee (F02's own capacity count has
        // no "is this user active" condition - neither does this).
        public async Task<List<WorkloadResDto>> GetWorkload()
        {
            var tasks = await unitOfWork.GetRepository<TaskItem>().QueryAll();

            return await tasks
                .Where(t => t.AssigneeId.HasValue
                    && (t.Status == TaskWorkflowStatus.Todo
                        || t.Status == TaskWorkflowStatus.InProgress
                        || t.Status == TaskWorkflowStatus.Review))
                .GroupBy(t => t.AssigneeId!.Value)
                .Select(g => new WorkloadResDto
                {
                    UserId = g.Key,
                    Todo = g.Count(t => t.Status == TaskWorkflowStatus.Todo),
                    InProgress = g.Count(t => t.Status == TaskWorkflowStatus.InProgress),
                    Review = g.Count(t => t.Status == TaskWorkflowStatus.Review),
                })
                .ToListAsync();
        }

        private async Task<UserAdminResDto> BuildAdminDto(User user, string? roleName = null)
        {
            roleName ??= await CurrentRoleName(user.Id);
            var isLockedOut = await userManager.IsLockedOutAsync(user);

            return new UserAdminResDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                DisplayName = user.DisplayName,
                Role = roleName ?? string.Empty,
                IsActive = user.DeactivatedAt == null,
                DeactivatedAt = user.DeactivatedAt,
                MustChangePassword = user.MustChangePassword,
                IsLockedOut = isLockedOut,
                CreatedAt = user.CreatedAt,
                AvatarUrl = AvatarKeyHelper.BuildUrl(user.Id, user.AvatarObjectKey),
            };
        }

        private async Task<string?> CurrentRoleName(long userId)
        {
            var userRoleRepo = await unitOfWork.GetRepository<UserRole>().QueryAll();
            var roleRepo = await unitOfWork.GetRepository<Role>().QueryAll();

            var query =
                from ur in userRoleRepo
                join r in roleRepo on ur.RoleId equals r.Id
                where ur.UserId == userId
                orderby r.Id
                select r.Name;

            return await query.FirstOrDefaultAsync();
        }

        // Last-active-admin guard (f08-accounts-profile.md "Acceptance
        // criteria"): counts every other active Admin, excluding the target
        // row itself.
        private async Task<bool> IsLastActiveAdmin(long excludingUserId)
        {
            var userRoleRepo = await unitOfWork.GetRepository<UserRole>().QueryAll();
            var users = await unitOfWork.GetRepository<User>().QueryAll();

            var query =
                from ur in userRoleRepo
                join u in users on ur.UserId equals u.Id
                where ur.RoleId == RolePolicies.Admin.Id
                    && u.Id != excludingUserId
                    && u.DeactivatedAt == null
                select u.Id;

            return !await query.AnyAsync();
        }

        private static string RoleName(AccountRole role) => role switch
        {
            AccountRole.Admin => RolePolicies.Admin.Name,
            AccountRole.Manager => RolePolicies.Manager.Name,
            AccountRole.Member => RolePolicies.Member.Name,
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

        // CSPRNG, not a fixed-string "generator" (backend.md known defect):
        // 16 characters from a charset that guarantees the policy's
        // digit/upper/lower/non-alphanumeric requirements are satisfiable.
        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string special = "!@#$%^&*-_";
            const string all = upper + lower + digits + special;

            Span<char> chars = stackalloc char[16];
            chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
            chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
            chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
            chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

            for (var i = 4; i < chars.Length; i++)
            {
                chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
            }

            // Shuffle so the fixed-category positions above aren't always
            // first (Fisher-Yates with a CSPRNG index).
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }

        // Join projection shape only - not returned past this service.
        private class UserRoleRow
        {
            public long UserId { get; set; }

            public string? UserName { get; set; }

            public long? RoleId { get; set; }

            public string? RoleName { get; set; }

            public string? DisplayName { get; set; }

            public string? AvatarObjectKey { get; set; }

            public bool IsActive { get; set; }
        }
    }
}
