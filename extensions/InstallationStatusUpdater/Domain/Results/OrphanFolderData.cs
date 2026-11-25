using System;

namespace InstallationStatusUpdater.Domain.Results
{
    public class OrphanFolderData
    {
        public string FolderName { get; }
        public string FullPath { get; }
        public long SizeBytes { get; }
        public DateTime LastModified { get; }
        public string ParentDetectionDir { get; }

        public OrphanFolderData(
            string folderName,
            string fullPath,
            long sizeBytes,
            DateTime lastModified,
            string parentDetectionDir)
        {
            FolderName = folderName;
            FullPath = fullPath;
            SizeBytes = sizeBytes;
            LastModified = lastModified;
            ParentDetectionDir = parentDetectionDir;
        }
    }
}
