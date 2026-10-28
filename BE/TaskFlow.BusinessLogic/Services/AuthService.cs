using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TaskFlow.BusinessLogic.Dtos.Auth;
using TaskFlow.Common.Constants;
using TaskFlow.Common.Helpers;
using TaskFlow.DataAccess.Entities;
using TaskFlow.Repositories.UoW;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace TaskFlow.BusinessLogic.Services
{
    public interface IAuthService
    {
        Task<LoginResultDto> Login(Guid deviceGuid, string username, string password);

        Task<RefreshTokenDto> RefreshToken(Guid deviceGuid, string token, string refreshToken);
    }

    public class AuthService(
        SignInManager<User> signInManager,
        UserManager<User> userManager,
        IConfiguration configuration,
        IUnitOfWork unitOfWork,
        ILogger<AuthService> logger) : IAuthService
    {
        private readonly string _secret = configuration.GetSection(ConfigKeys.Security.Jwt.Secret).Get<string>()!;
        private readonly string _refreshSecret = configuration.GetSection(ConfigKeys.Security.Jwt.RefreshSecret).Get<string>()!;
        private readonly int _expirationMinutes = configuration.GetSection(ConfigKeys.Security.Jwt.ExpirationMinutes).Get<int>();
        private readonly int _refreshExpirationDays = configuration.GetSection(ConfigKeys.Security.Jwt.RefreshExpirationDays).Get<int>();
        private readonly int _maxFailedAccessAttempts = configuration.GetSection(ConfigKeys.Security.Lockout.MaxFailedAccessAttempts).Get<int>();
        private readonly int _defaultLockoutMinutes = configuration.GetSection(ConfigKeys.Security.Lockout.DefaultLockoutMinutes).Get<int>();

        public async Task<LoginResultDto> Login(Guid deviceGuid, string username, string password)
        {
            var loginResult = new LoginResultDto();

            var user = await userManager.FindByNameAsync(username);
            if (user == null)
            {
                loginResult.InvalidCredential = true;
                return loginResult;
            }

            var result = await signInManager.PasswordSignInAsync(username, password, true, true);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    loginResult.IsLockedOut = true;
                    return loginResult;
                }

                if (user.AccessFailedCount + 1 == _maxFailedAccessAttempts)
                {
                    loginResult.WillBeLockedOut = true;
                    return loginResult;
                }

                if (user.AccessFailedCount == _maxFailedAccessAttempts)
                {
                    loginResult.IsLockedOut = true;
                    await userManager.SetLockoutEndDateAsync(user,
                        DateTimeHelper.GetDtOffset().AddMinutes(_defaultLockoutMinutes));
                    return loginResult;
                }

                loginResult.InvalidCredential = true;

                return loginResult;
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            var userClaims = principal.Claims.ToList();

            var authorizationClaims = await GetRolePolicyPermissions(user.Id);

            userClaims.AddRange(authorizationClaims.Select(ac => new Claim(RolePolicies.ClaimType, ac)));

            //var additionalClaims = new[]
            //{
            //    // Add your claims here
            //};

            //userClaims.AddRange(additionalClaims);

            var token = GenerateToken(
                userClaims,
                _secret,
                DateTimeHelper.GetDt().AddMinutes(_expirationMinutes));

            var refreshExpiration = DateTimeHelper.GetDt()
                .AddDays(_refreshExpirationDays);

            var refreshToken = GenerateToken(
                userClaims,
                _refreshSecret,
                refreshExpiration);

            loginResult.Token = token;
            loginResult.RefreshToken = refreshToken;
            loginResult.RefreshTokenExpiresAt = refreshExpiration;
            loginResult.Permissions = authorizationClaims;

            var userTokenRepo = await unitOfWork.GetRepository<UserToken>().QueryAll();
            var userToken = await userTokenRepo.FirstOrDefaultAsync(x => x.DeviceGuid == deviceGuid);
            if (userToken == null)
            {
                userToken = new UserToken
                {
                    LoginProvider = GlobalConstants.UserToken.LoginProvider,
                    Name = deviceGuid.ToString(),
                    DeviceGuid = deviceGuid
                };

                await unitOfWork.GetRepository<UserToken>().Add(userToken);
            }
            else
            {
                await unitOfWork.GetRepository<UserToken>().Update(userToken);
            }

            userToken.UserId = user.Id;
            userToken.LastLogin = DateTimeHelper.GetDt();
            userToken.RefreshToken = refreshToken;
            userToken.RefreshTokenExpiryTime = refreshExpiration;

            await unitOfWork.SaveChanges();

            loginResult.Success = true;
            return loginResult;
        }

        public async Task<RefreshTokenDto> RefreshToken(Guid deviceGuid, string token, string refreshToken)
        {
            var result = new RefreshTokenDto();

            var oldTokenPrincipal = ValidateIgnoringLifetime(token, _secret);
            if (oldTokenPrincipal == null)
            {
                logger.LogWarning("Refresh rejected: the old access token failed signature/issuer/audience validation");
                return result;
            }

            var userId = oldTokenPrincipal.Claims.GetUserId();

            var oldTokenExpiry = oldTokenPrincipal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (oldTokenExpiry != null
                && long.TryParse(oldTokenExpiry, out var oldTokenExpiryUnix)
                && DateTimeOffset.FromUnixTimeSeconds(oldTokenExpiryUnix) > DateTimeHelper.GetDtOffset())
            {
                logger.LogWarning("User {userId} tries to get new token while it is still valid", userId);
                return result;
            }

            var refreshTokenContent = ReadJwtToken(refreshToken);
            var refreshTokenUserId = refreshTokenContent.Claims.GetUserId();

            if (userId != refreshTokenUserId)
            {
                logger.LogWarning("User of both token aren't matched: {userId} - {refreshTokenUserId}", userId, refreshTokenUserId);
                return result;
            }

            var user = await VerifyToken(refreshToken, _refreshSecret);
            if (user == null)
            {
                return result;
            }

            var userTokenRepo = await unitOfWork.GetRepository<UserToken>().QueryAll();
            var userToken = await userTokenRepo.FirstOrDefaultAsync(x => x.DeviceGuid == deviceGuid && x.UserId == userId);

            if (userToken == null
                || userToken.RefreshToken != refreshToken
                || userToken.RefreshTokenExpiryTime < DateTimeHelper.GetDtOffset())
            {
                return result;
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            var userClaims = principal.Claims.ToList();

            var authorizationClaims = await GetRolePolicyPermissions(user.Id);

            userClaims.AddRange(authorizationClaims.Select(ac => new Claim(RolePolicies.ClaimType, ac)));

            var newToken = GenerateToken(
                userClaims,
                _secret,
                DateTimeHelper.GetDt().AddMinutes(_expirationMinutes));

            var refreshExpiration = DateTimeHelper.GetDt()
                .AddDays(_refreshExpirationDays);

            var newRefreshToken = GenerateToken(
                userClaims,
                _refreshSecret,
                refreshExpiration);

            result.Success = true;
            result.Token = newToken;
            result.RefreshToken = newRefreshToken;
            result.RefreshTokenExpiresAt = refreshExpiration;
            result.Permissions = authorizationClaims;

            userToken.RefreshToken = newRefreshToken;
            userToken.RefreshTokenExpiryTime = refreshExpiration;

            await unitOfWork.GetRepository<UserToken>().Update(userToken);
            await unitOfWork.SaveChanges();

            return result;
        }

        private async Task<string[]> GetRolePolicyPermissions(long userId)
        {
            var roleRepo = await unitOfWork.GetRepository<Role>().QueryAll();
            var userRoleRepo = await unitOfWork.GetRepository<UserRole>().QueryAll();
            var roleClaims = await unitOfWork.GetRepository<RoleClaim>().QueryAll();

            return (from ur in userRoleRepo
                    join r in roleRepo on ur.RoleId equals r.Id
                    join rc in roleClaims on r.Id equals rc.RoleId
                    where ur.UserId == userId
                    select rc.ClaimValue).Distinct().ToArray();
        }

        private string GenerateToken(IEnumerable<Claim> claims, string secret, DateTime expireDateTime)
        {
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expireDateTime,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)), SecurityAlgorithms.HmacSha512Signature),
                Audience = configuration.GetSection(ConfigKeys.Security.Jwt.Audience).Get<string>(),
                Issuer = configuration.GetSection(ConfigKeys.Security.Jwt.Issuer).Get<string>()
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        private async Task<User?> VerifyToken(string token, string secret)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var jwtToken = ReadJwtToken(token);

            var userId = jwtToken.Claims.GetUserId();

            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user == null) return null;

            var securityKey = secret.ToByteArray();

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(securityKey),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return user;
        }

        // Validates signature, issuer and audience of an access token that is
        // expected to be expired by the time a refresh is requested - lifetime
        // is deliberately not checked. Fails closed (returns null) on a bad
        // signature, wrong issuer/audience or a malformed token, instead of
        // letting the exception reach the controller's generic catch.
        private ClaimsPrincipal? ValidateIgnoringLifetime(string token, string secret)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(secret.ToByteArray()),
                    ValidateIssuer = true,
                    ValidIssuer = configuration.GetSection(ConfigKeys.Security.Jwt.Issuer).Get<string>(),
                    ValidateAudience = true,
                    ValidAudience = configuration.GetSection(ConfigKeys.Security.Jwt.Audience).Get<string>(),
                    ValidateLifetime = false,
                    ClockSkew = TimeSpan.Zero
                }, out _);

                return principal;
            }
            catch (SecurityTokenException)
            {
                return null;
            }
        }

        private static JwtSecurityToken ReadJwtToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            return jwtToken;
        }
    }
}
