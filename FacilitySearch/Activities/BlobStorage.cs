using System;
using System.Collections.Generic;
using System.Text;
using Azure.Storage.Blobs;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace ProviderSearch.Activities
{
    internal class BlobStorage
    {
        private BlobContainerClient _container;
        private ILogger _log;

        public BlobStorage(string connectionString, string containerName, ILogger log)
        {
            _log = log;
            try
            {
                _container = new BlobContainerClient(connectionString, containerName);
                _log.LogInformation($"Established connection to blob container {_container.Name} at URI {_container.Uri}");
            }
            catch (Exception e)
            {
                _log.LogError($"Failure establishing connection to Azure Blob Storage with message: {e.Message}");
                throw;
            }

        }

        public List<string> GetBlobStorageUrlByRecordId(string recordId)
        {
            List<string> blobUrls = new List<string>();
            BlobHierarchyItem recordFolder = null;
            List<BlobItem> recordBlobs;

            try
            {
                _log.LogInformation("Getting folders from blob storage");
                var blobFolders = _container.GetBlobsByHierarchy(BlobTraits.None, BlobStates.None, "/");

                foreach(var folder in blobFolders)
                {
                    _log.LogInformation($"Folder: {folder.Prefix}");
                    if(folder.Prefix.ToLower().Replace("/", "") == recordId.ToLower())
                    {
                        recordFolder = folder;
                        break;
                    }
                }

                if(recordFolder != null)
                    _log.LogInformation($"Folder found");

            }
            catch (Exception ex)
            {
                _log.LogError("An error occurred retrieving folder from blob storage", ex.Message);
                throw;
            }

            if(recordFolder == null)
            {
                _log.LogError($"Folder not found for Record {recordId}");
                return blobUrls;
            }

            try
            {
                _log.LogInformation("Retrieving blobs from folder");
                recordBlobs = _container.GetBlobs(BlobTraits.None, BlobStates.None, recordId).ToList();


                //recordBlobs = _container.GetBlobsByHierarchy(BlobTraits.None, BlobStates.None, "/", recordId).ToList();
                _log.LogInformation($"{recordBlobs.Count} blobs contained in folder");
            }
            catch (Exception ex)
            {
                _log.LogError($"An error occurred retrieving blobs from folder.", ex.Message);
                throw;
            }

            foreach (var blob in recordBlobs)
            {
                try
                {
                    var blobClient = _container.GetBlobClient(blob.Name);
                    string blobUri = blobClient.Uri.ToString();
                    blobUrls.Add(blobUri);
                }
                catch (Exception)
                {

                    throw;
                }

            }

            return blobUrls;
        }

    }
}
