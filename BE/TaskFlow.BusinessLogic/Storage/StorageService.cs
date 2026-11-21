using TaskFlow.BusinessLogic.Dtos.Storage;
using TaskFlow.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace TaskFlow.BusinessLogic.Storage
{
    public interface IStorageService
    {
        Task<(bool, string)> Upload(UploadFileReqDto requestDto);

        Task<(bool, string)> Remove(string fileName);

        // R2-3/R2-6 (f08-accounts-profile.md "Avatars on the existing
        // StorageService"): a server-generated key, a sniffed content type
        // and a known size, into the private bucket - never the public
        // "Bucket" above, never the client's filename or Content-Type.
        Task<(bool Success, string Error)> PutObject(string key, Stream content, long size, string contentType, CancellationToken cancellationToken);

        Task<(bool Success, string Error, Stream? Content, string? ContentType, long Length)> GetObject(string key, CancellationToken cancellationToken);
    }

    public class StorageService(
        IMinioClient minioClient,
        IConfiguration configuration,
        ILogger<StorageService> logger) : IStorageService
    {
        private readonly string _bucket = configuration[ConfigKeys.MinIO.Bucket] ?? throw new ArgumentNullException(nameof(_bucket), GlobalConstants.StorageErrorMessage.BucketIsNull);
        private readonly string _privateBucket = configuration[ConfigKeys.MinIO.PrivateBucket] ?? throw new ArgumentNullException(nameof(_privateBucket), GlobalConstants.StorageErrorMessage.BucketIsNull);

        public async Task<(bool, string)> Remove(string fileName)
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(_bucket);
            var isExistBucket = await minioClient.BucketExistsAsync(bucketExistsArgs).ConfigureAwait(false);
            if (!isExistBucket)
            {
                return (false, GlobalConstants.StorageErrorMessage.BucketNotFound);
            }

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(fileName);
            await minioClient.RemoveObjectAsync(removeObjectArgs).ConfigureAwait(false);

            return (true, string.Empty);
        }

        public async Task<(bool Success, string Error)> PutObject(string key, Stream content, long size, string contentType, CancellationToken cancellationToken)
        {
            try
            {
                var bucketExistsArgs = new BucketExistsArgs().WithBucket(_privateBucket);
                var isExistBucket = await minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken).ConfigureAwait(false);
                if (!isExistBucket)
                {
                    return (false, GlobalConstants.StorageErrorMessage.BucketNotFound);
                }

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_privateBucket)
                    .WithObject(key)
                    .WithStreamData(content)
                    .WithObjectSize(size)
                    .WithContentType(contentType);

                var result = await minioClient.PutObjectAsync(putObjectArgs, cancellationToken).ConfigureAwait(false);
                if (result.Etag == null)
                {
                    logger.LogError("Put object {key} failed", key);
                    return (false, GlobalConstants.StorageErrorMessage.UploadFailed);
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Put object {key} failed", key);
                return (false, GlobalConstants.StorageErrorMessage.UploadFailed);
            }
        }

        public async Task<(bool Success, string Error, Stream? Content, string? ContentType, long Length)> GetObject(string key, CancellationToken cancellationToken)
        {
            try
            {
                var bucketExistsArgs = new BucketExistsArgs().WithBucket(_privateBucket);
                var isExistBucket = await minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken).ConfigureAwait(false);
                if (!isExistBucket)
                {
                    return (false, GlobalConstants.StorageErrorMessage.BucketNotFound, null, null, 0);
                }

                var memoryStream = new MemoryStream();
                string? contentType = null;
                long length = 0;

                var statArgs = new StatObjectArgs().WithBucket(_privateBucket).WithObject(key);
                var stat = await minioClient.StatObjectAsync(statArgs, cancellationToken).ConfigureAwait(false);
                contentType = stat.ContentType;
                length = stat.Size;

                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_privateBucket)
                    .WithObject(key)
                    .WithCallbackStream(stream => stream.CopyTo(memoryStream));

                await minioClient.GetObjectAsync(getObjectArgs, cancellationToken).ConfigureAwait(false);
                memoryStream.Position = 0;

                return (true, string.Empty, memoryStream, contentType, length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Get object {key} failed", key);
                return (false, GlobalConstants.StorageErrorMessage.UploadFailed, null, null, 0);
            }
        }

        public async Task<(bool, string)> Upload(UploadFileReqDto requestDto)
        {
            var fileExtension = Path.GetExtension(requestDto.File.FileName);
            var fileName = Path.GetFileNameWithoutExtension(requestDto.File.FileName);
            using var fileStream = requestDto.File.OpenReadStream();

            var bucketExistsArgs = new BucketExistsArgs().WithBucket(_bucket);
            var isExistBucket = await minioClient.BucketExistsAsync(bucketExistsArgs).ConfigureAwait(false);
            if (!isExistBucket)
            {
                return (false, GlobalConstants.StorageErrorMessage.BucketNotFound);
            }

            fileName = fileName.Replace(GlobalConstants.Symbol.Space, string.Empty);
            if (requestDto.SkipRandomFileName)
            {
                fileName = string.Concat(fileName, fileExtension);
            }
            else
            {
                fileName = string.Concat(Guid.NewGuid().ToString().Replace(GlobalConstants.Symbol.Dash, string.Empty),
                    GlobalConstants.Symbol.UnderScore,
                    fileName,
                    fileExtension);
            }

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucket)
                .WithStreamData(fileStream)
                .WithObject(fileName)
                .WithObjectSize(fileStream.Length)
                .WithContentType(GetContentType(fileName))
                .WithHeaders(requestDto.Metadata);

            var result = await minioClient.PutObjectAsync(putObjectArgs).ConfigureAwait(false);
            if (result.Etag == null)
            {
                logger.LogError("Upload file {fileName} failed", requestDto.File.FileName);
                return (false, GlobalConstants.StorageErrorMessage.UploadFailed);
            }

            return (true, fileName);
        }

        private static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            var found = GlobalConstants.ImageExtToType.TryGetValue(ext, out var conteType);
            return found ? conteType! : "application/octet-stream";
        }
    }
}
