using System.Text;
using TaskFlow.Common.Constants;
using TaskFlow.DataAccess.DbContexts;
using TaskFlow.DataAccess.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace TaskFlow.Api.Configurations
{
    public static class SecurityConfiguration
    {
        public static void AddSecurity(this IServiceCollection collection, IConfiguration configuration)
        {
            collection.AddDefaultIdentity<User>(options =>
                {
                    options.Lockout.MaxFailedAccessAttempts = configuration.GetSection(ConfigKeys.Security.Lockout.MaxFailedAccessAttempts).Get<int>();
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(configuration.GetSection(ConfigKeys.Security.Lockout.DefaultLockoutMinutes).Get<int>());
                })
                .AddEntityFrameworkStores<MyDbContext>();

            collection.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidAudience = configuration.GetSection(ConfigKeys.Security.Jwt.Audience).Get<string>(),
                        ValidIssuer = configuration.GetSection(ConfigKeys.Security.Jwt.Issuer).Get<string>(),
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration[ConfigKeys.Security.Jwt.Secret]!)),
                    };
                });

            collection.AddAuthorization(options =>
            {
                foreach (var permission in Permissions.All)
                {
                    options.AddPolicy(permission, policy =>
                        policy.RequireClaim(RolePolicies.ClaimType, permission));
                }
            });
        }
    }
}
