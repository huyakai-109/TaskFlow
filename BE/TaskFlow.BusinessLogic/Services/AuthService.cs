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

            var roleRepo = await unitOfWork.GetRepository<Role>().QueryAll();
            var userRoleRepo = await unitOfWork.GetRepository<UserRole>().QueryAll();
            var roleClaims = await unitOfWork.GetRepository<RoleClaim>().QueryAll();

            var authorizationClaims = (from ur in userRoleRepo
                                       join r in roleRepo on ur.RoleId equals r.Id
                                       join rc in roleClaims on r.Id equals rc.RoleId
                                       where ur.UserId == user.Id
                                       select rc.ClaimValue).Distinct().ToArray();

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

            try
            {
                var user = await VerifyToken(token, _secret);
                if (user == null)
                {
                    return result;
                }

                logger.LogWarning("User {id} - {email} tries to get new token while it is still valid", user.Id, user.Email);
                return result;
            }
            catch (SecurityTokenExpiredException)
            {
                var tokenContent = ReadJwtToken(token);
                var refreshTokenContent = ReadJwtToken(refreshToken);

                var userId = tokenContent.Claims.GetUserId();
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
                var userToken = await userTokenRepo.FirstOrDefaultAsync(x => x.DeviceGuid == deviceGuid);

                if (userToken == null
                    || userToken.RefreshToken != refreshToken
                    || userToken.RefreshTokenExpiryTime < DateTimeHelper.GetDtOffset())
                {
                    return result;
                }

                var principal = await signInManager.CreateUserPrincipalAsync(user);

                var newToken = GenerateToken(
                    principal.Claims,
                    _secret,
                    DateTimeHelper.GetDt().AddMinutes(_expirationMinutes));

                var refreshExpiration = DateTimeHelper.GetDt()
                    .AddDays(_refreshExpirationDays);

                var newRefreshToken = GenerateToken(
                    principal.Claims,
                    _refreshSecret,
                    refreshExpiration);

                result.Success = true;
                result.Token = newToken;
                result.RefreshToken = newRefreshToken;

                userToken.RefreshToken = newRefreshToken;
                userToken.RefreshTokenExpiryTime = refreshExpiration;

                await unitOfWork.GetRepository<UserToken>().Update(userToken);
                await unitOfWork.SaveChanges();

                return result;
            }
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

        private static JwtSecurityToken ReadJwtToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            return jwtToken;
        }
    }
}
