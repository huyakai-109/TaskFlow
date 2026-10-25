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
    }

    public class StorageService(
        IMinioClient minioClient,
        IConfiguration configuration,
        ILogger<StorageService> logger) : IStorageService
    {
        private readonly string _bucket = configuration[ConfigKeys.MinIO.Bucket] ?? throw new ArgumentNullException(nameof(_bucket), GlobalConstants.StorageErrorMessage.BucketIsNull);

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
