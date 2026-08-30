// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.ObjectStorage;

public sealed class S3ObjectStore : IObjectStore, IDisposable
{
    private readonly AmazonS3Client _s3Client;
    private const string SmallFileBucketName = "ferrite-small-files";
    private const string BigFileBucketName = "ferrite-big-files";
    private readonly Task _createBuckets;
    public S3ObjectStore(string serviceUrl, string accessKey, string secretKey)
    {
        var config = new AmazonS3Config
        {
            AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
            ServiceURL = serviceUrl,
            ForcePathStyle = true
        };
        _s3Client = new AmazonS3Client(
            accessKey,
            secretKey,
            config
        );
        
        _createBuckets = CreateBuckets();
    }

    private async Task CreateBuckets()
    {
        var buckets = await _s3Client.ListBucketsAsync();
        if (!(buckets.Buckets ?? []).Any(bucket =>
                bucket.BucketName == SmallFileBucketName))
        {
            PutBucketRequest request = new PutBucketRequest();
            request.BucketName = SmallFileBucketName;
            var res = await _s3Client.PutBucketAsync(request);
        }
        if (!(buckets.Buckets ?? []).Any(bucket =>
                bucket.BucketName == BigFileBucketName))
        {
            PutBucketRequest request = new PutBucketRequest();
            request.BucketName = BigFileBucketName;
            var res = await _s3Client.PutBucketAsync(request);
        }
    }

    public async ValueTask<bool> SaveFilePart(long fileId, int filePart, Stream data)
    {
        await _createBuckets;
        PutObjectRequest putObjectRequest = new PutObjectRequest
        {
            InputStream = data,
            AutoCloseStream = false,
            Key = fileId.ToString("X") + "-" + filePart.ToString("X"),
            BucketName = SmallFileBucketName
        };
        await _s3Client.PutObjectAsync(putObjectRequest);
        return true;
    }

    public async ValueTask<bool> SaveBigFilePart(long fileId, int filePart, int fileTotalParts, Stream data)
    {
        await _createBuckets;
        PutObjectRequest putObjectRequest = new PutObjectRequest
        {
            InputStream = data,
            AutoCloseStream = false,
            Key = fileId.ToString("X") + "-" + filePart.ToString("X"),
            BucketName = BigFileBucketName
        };
        await _s3Client.PutObjectAsync(putObjectRequest);
        return true;
    }

    public async ValueTask<Stream> GetFilePart(long fileId, int filePart)
    {
        GetObjectRequest getObjectRequest = new GetObjectRequest();
        getObjectRequest.Key = fileId.ToString("X")+"-"+filePart.ToString("X");
        getObjectRequest.BucketName = SmallFileBucketName;
        var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest);
        return getObjectResponse.ResponseStream;
    }

    public async ValueTask<Stream> GetBigFilePart(long fileId, int filePart)
    {
        GetObjectRequest getObjectRequest = new GetObjectRequest();
        getObjectRequest.Key = fileId.ToString("X")+"-"+filePart.ToString("X");
        getObjectRequest.BucketName = BigFileBucketName;
        var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest);
        return getObjectResponse.ResponseStream;
    }

    public IFileOwner GetFileOwner(TLUploadedFileInfo fileInfo, long offset, int limit,
        long reqMsgId, byte[] headerBytes)
    {
        return new S3FileOwner(fileInfo, this, offset, limit, reqMsgId, headerBytes);
    }

    public void Dispose() => _s3Client.Dispose();
}
