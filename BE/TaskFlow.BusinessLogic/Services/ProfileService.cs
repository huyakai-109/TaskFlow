using TaskFlow.BusinessLogic.Dtos.Profile;
using TaskFlow.BusinessLogic.Storage;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Constants.Messages;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.BusinessLogic.Services
{
    public interface IProfileService
    {
        Task<(string ErrorCode, MeResDto? Me)> GetMe(long userId);

        Task<(string ErrorCode, MeResDto? Me)> UpdateDisplayName(long userId, string? displayName);

        Task<string> ChangePassword(long userId, string currentPassword, string newPassword);

        Task<(string ErrorCode, string? AvatarUrl)> PutAvatar(long userId, Stream content, long size, CancellationToken cancellationToken);

        Task<string> DeleteAvatar(long userId);
    }

    // R2-3 (f08-accounts-profile.md "Self API"), route api/v1/me: every
    // action acts on the caller's own row only - the user id always comes
    // from claims (read in the controller), never a route parameter.
    public class ProfileService(IUnitOfWork unitOfWork, UserManager<User> userManager, IStorageService storageService) : IProfileService
    {
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

            var header = new byte[12];
            var read = await content.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            if (!ImageContentSniffer.TryDetect(header[..read], out var extension, out var contentType))
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
