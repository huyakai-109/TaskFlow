using TaskFlow.BusinessLogic.Dtos.Users;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.BusinessLogic.Services
{
    public interface IUserService
    {
        Task<List<UserResDto>> GetList();
    }

    // f02-capacity.md T4 (sign-off #6): GET /users, the assignee picker's
    // source list.
    public class UserService(IUnitOfWork unitOfWork) : IUserService
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
                select new UserRoleRow { UserId = u.Id, UserName = u.UserName, RoleId = (long?)r.Id, RoleName = r.Name };

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
                    };
                })
                .OrderBy(dto => dto.UserName, StringComparer.Ordinal)
                .ToList();
        }

        // Join projection shape only - not returned past this service.
        private class UserRoleRow
        {
            public long UserId { get; set; }

            public string? UserName { get; set; }

            public long? RoleId { get; set; }

            public string? RoleName { get; set; }
        }
    }
}
