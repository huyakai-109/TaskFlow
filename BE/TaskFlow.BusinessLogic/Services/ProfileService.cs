using TaskFlow.BusinessLogic.Dtos.Profile;
using TaskFlow.BusinessLogic.Storage;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Enums;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TaskFlow.BusinessLogic.Services
{
    public interface IProfileService
    {
        Task<(string ErrorCode, MeResDto? Me)> GetMe(long userId);

        Task<(string ErrorCode, MeResDto? Me)> UpdateDisplayName(long userId, string? displayName);

        Task<string> ChangePassword(long userId, string currentPassword, string newPassword);

        Task<(string ErrorCode, string? AvatarUrl)> PutAvatar(long userId, Stream content, long size, CancellationToken cancellationToken);

        Task<string> DeleteAvatar(long userId);

        Task<MeSummaryResDto> GetSummary(long userId);
    }

    // R2-3 (f08-accounts-profile.md "Self API"), route api/v1/me: every
    // action acts on the caller's own row only - the user id always comes
    // from claims (read in the controller), never a route parameter.
    public class ProfileService(
        IUnitOfWork unitOfWork,
        UserManager<User> userManager,
        IStorageService storageService,
        IConfiguration configuration) : IProfileService
    {
        private readonly string _timeZone = configuration.GetSection(ConfigKeys.Workflow.TimeZone).Get<string>()!;
        public async Task<(string ErrorCode, MeResDto? Me)> GetMe(long userId)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(userId);
            if (user == null)
            {
                return (ProfileControllerMS.Get.Exception, null);
            }

            var role = await CurrentRoleName(userId);

            return (string.Empty, new MeResDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                DisplayName = user.DisplayName,
                Role = role ?? string.Empty,
                AvatarUrl = AvatarKeyHelper.BuildUrl(user.Id, user.AvatarObjectKey),
            });
        }

        // Null/blank clears the display name (f08-accounts-profile.md "Self
        // API" PUT /me) - falls back to UserName everywhere it renders.
        public async Task<(string ErrorCode, MeResDto? Me)> UpdateDisplayName(long userId, string? displayName)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(userId);
            if (user == null)
            {
                return (ProfileControllerMS.Update.Exception, null);
            }

            // UnitOfWork.SaveChanges() trims strings and turns a blank one
            // into null (CLAUDE.md "Unit of work") - null/blank clears the
            // display name, per spec.
            user.DisplayName = displayName!;

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return (ProfileControllerMS.Update.Conflict, null);
            }

            return await GetMe(userId);
        }

        public async Task<string> ChangePassword(long userId, string currentPassword, string newPassword)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(userId);
            if (user == null)
            {
                return ProfileControllerMS.ChangePassword.Exception;
            }

            var passwordCorrect = await userManager.CheckPasswordAsync(user, currentPassword);
            if (!passwordCorrect)
            {
                return ProfileControllerMS.ChangePassword.WrongPassword;
            }

            var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                return ProfileControllerMS.ChangePassword.PolicyViolation;
            }

            // Revokes other sessions' refresh tokens (f08-accounts-profile.md
            // "Self API" PUT /me/password), same as an admin-initiated reset.
            var tokenRepo = await unitOfWork.GetRepository<UserToken>().QueryCondition(t => t.UserId == userId, disableTracking: false);
            var tokens = await tokenRepo.ToListAsync();
            if (tokens.Count > 0)
            {
                await unitOfWork.GetRepository<UserToken>().Delete(tokens);
                await unitOfWork.SaveChanges();
            }

            return string.Empty;
        }

        public async Task<(string ErrorCode, string? AvatarUrl)> PutAvatar(long userId, Stream content, long size, CancellationToken cancellationToken)
        {
            if (size <= 0)
            {
                return (ProfileControllerMS.Avatar.Empty, null);
            }

            if (size > GlobalConstants.Avatar.MaxSizeBytes)
            {
                return (ProfileControllerMS.Avatar.TooLarge, null);
            }

            // review-change (R2 batch): Stream.ReadAsync is not guaranteed to
            // fill the buffer in one call - a short first read would sniff
            // fewer header bytes than intended (e.g. accepting a 3-byte
            // partial read as "JPEG" without ever having looked at bytes
            // 4-12, or wrongly rejecting a genuine WebP whose 12-byte
            // signature arrived split across reads). Filled in a loop
            // instead, bounded by the already-validated `size` so a
            // legitimately short file can't throw.
            var headerLength = (int)Math.Min(12, size);
            var header = new byte[headerLength];
            var totalRead = 0;
            while (totalRead < headerLength)
            {
                var read = await content.ReadAsync(header.AsMemory(totalRead, headerLength - totalRead), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (!ImageContentSniffer.TryDetect(header[..totalRead], out var extension, out var contentType))
            {
                return (ProfileControllerMS.Avatar.UnsupportedType, null);
            }

            content.Position = 0;

            var user = await unitOfWork.GetRepository<User>().FindById(userId);
            if (user == null)
            {
                return (ProfileControllerMS.Avatar.Exception, null);
            }

            var key = AvatarKeyHelper.BuildKey(userId, Guid.NewGuid(), extension);

            var (putSucceeded, _) = await storageService.PutObject(key, content, size, contentType, cancellationToken);
            if (!putSucceeded)
            {
                return (ProfileControllerMS.Avatar.StorageUnavailable, null);
            }

            var oldKey = user.AvatarObjectKey;
            user.AvatarObjectKey = key;

            try
            {
                await unitOfWork.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                await storageService.Remove(key);
                return (ProfileControllerMS.Avatar.Conflict, null);
            }

            if (!string.IsNullOrEmpty(oldKey))
            {
                await storageService.Remove(oldKey);
            }

            return (string.Empty, AvatarKeyHelper.BuildUrl(userId, key));
        }

        public async Task<string> DeleteAvatar(long userId)
        {
            var user = await unitOfWork.GetRepository<User>().FindById(userId);
            if (user == null)
            {
                return ProfileControllerMS.Avatar.Exception;
            }

            if (string.IsNullOrEmpty(user.AvatarObjectKey))
            {
                return string.Empty;
            }

            var oldKey = user.AvatarObjectKey;
            user.AvatarObjectKey = null!;
            await unitOfWork.SaveChanges();

            await storageService.Remove(oldKey);

            return string.Empty;
        }

        // f10-read-models-and-sort.md Section 6: "this week" = Monday 00:00
        // to the next Monday 00:00 in the configured zone (B7). Correct
        // because assignment is locked in Review/Done (F01 D12) - the
        // assignee recorded on a Done task is the one who finished it.
        public async Task<MeSummaryResDto> GetSummary(long userId)
        {
            var today = DateTimeHelper.GetDtOffset().ToDateInZone(_timeZone);
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-daysSinceMonday);
            var weekEnd = weekStart.AddDays(7);

            var weekStartUtc = weekStart.LocalStartOfDayToUtc(_timeZone);
            var weekEndUtc = weekEnd.LocalStartOfDayToUtc(_timeZone);

            var tasks = await unitOfWork.GetRepository<TaskItem>().QueryAll();

            var inProgress = await tasks.CountAsync(t => t.AssigneeId == userId && t.Status == TaskWorkflowStatus.InProgress);
            var completedThisWeek = await tasks.CountAsync(t =>
                t.AssigneeId == userId
                && t.Status == TaskWorkflowStatus.Done
                && t.CompletedAt >= weekStartUtc
                && t.CompletedAt < weekEndUtc);

            return new MeSummaryResDto
            {
                InProgress = inProgress,
                CompletedThisWeek = completedThisWeek,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
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
    }
}
