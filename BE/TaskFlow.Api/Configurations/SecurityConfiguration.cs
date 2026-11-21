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

                    // f08-accounts-profile.md "Password policy": Identity's
                    // digit/upper/lower/non-alphanumeric defaults are kept;
                    // only the minimum length is raised, from configuration.
                    options.Password.RequiredLength = configuration.GetSection(ConfigKeys.Security.Password.RequiredLength).Get<int>();
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

                    // f07-presence.md "Authentication" / ADR 0004 condition 2:
                    // browsers cannot set headers on a WebSocket upgrade, so
                    // the token travels as ?access_token= - but only on hub
                    // paths. Every ordinary HTTP route keeps requiring the
                    // Authorization header only; this never relaxes that.
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        },
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
