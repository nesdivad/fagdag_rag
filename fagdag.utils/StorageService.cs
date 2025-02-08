using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Fagdag.Utils;

public interface IStorageService
{
    IAsyncEnumerable<BlobItem> DownloadBlobsAsync();
}

public class StorageService : IStorageService
{
    private const string BlobContainerName = "fagdag";
    private BlobServiceClient _client { get; init; }

    public StorageService(string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        _client = new(connectionString);
    }

    public async IAsyncEnumerable<BlobItem> DownloadBlobsAsync()
    {
        var blobClient = _client.GetBlobContainerClient(BlobContainerName);
        if (blobClient is null)
            blobClient = await CreateBlobContainerAsync();

        await foreach (var blob in blobClient.GetBlobsAsync())
        {
            if (blob is null)
                continue;

            yield return blob;
        }
    }

    private async Task<BlobContainerClient> CreateBlobContainerAsync()
    {
        var res = await _client.CreateBlobContainerAsync(BlobContainerName);
        return res.Value;
    }
}
