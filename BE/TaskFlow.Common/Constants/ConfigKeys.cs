namespace TaskFlow.Common.Constants
{
    public static class ConfigKeys
    {
        public const string EnableSwagger = "EnableSwagger";
        public const string AutoMigration = "AutoMigration";

        public struct Databases
        {
            public const string MainDb = "ConnectionStrings:MyDatabase";
            public const string Redis = "ConnectionStrings:RedisDatabase";
            public const string Quartz = "ConnectionStrings:QuartzDatabase";
        }

        public struct Security
        {
            public struct Lockout
            {
                public const string MaxFailedAccessAttempts = "Security:Lockout:MaxFailedAccessAttempts";
                public const string DefaultLockoutMinutes = "Security:Lockout:DefaultLockoutMinutes";
            }

            public struct Jwt
            {
                public const string Secret = "Security:Jwt:Secret";
                public const string RefreshSecret = "Security:Jwt:RefreshSecret";
                public const string ExpirationMinutes = "Security:Jwt:ExpirationMinutes";
                public const string RefreshExpirationDays = "Security:Jwt:RefreshExpirationDays";
                public const string Issuer = "Security:Jwt:Issuer";
                public const string Audience = "Security:Jwt:Audience";
            }
        }

        public struct Cors
        {
            public const string AllowedOrigins = "Cors:AllowedOrigins";
        }

        public struct DevSeed
        {
            public const string Password = "DevSeed:Password";
        }

        public struct MinIO
        {
            public const string Endpoint = "MinIO:Endpoint";
            public const string AccessKey = "MinIO:AccessKey";
            public const string SecretKey = "MinIO:SecretKey";
            public const string Secure = "MinIO:Secure";
            public const string Bucket = "MinIO:Bucket";
            public const string Region = "MinIO:Region";
        }
    }
}
