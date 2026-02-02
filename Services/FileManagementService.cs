using System;
using System.Collections;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace SQLScripter.Services
{
    /// <summary>
    /// Service for file management operations including ZIP compression
    /// </summary>
    public interface IFileManagementService
    {
        bool ZipFolder(string sourcePath, string zipFilePath, string password);
        void DeleteFolder(string path);
        void CleanupOldFiles(string basePath, int daysToKeep);
    }

    public class FileManagementService : IFileManagementService
    {
        private readonly ILoggerService _logger;

        public FileManagementService(ILoggerService logger)
        {
            _logger = logger;
        }

        public bool ZipFolder(string sourcePath, string zipFilePath, string password)
        {
            try
            {
                var fileList = GenerateFileList(sourcePath);
                int trimLength = Directory.GetParent(sourcePath)?.ToString().Length ?? 0;

                using (var zipStream = new ZipOutputStream(File.Create(zipFilePath)))
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        zipStream.Password = password;
                    }

                    zipStream.SetLevel(9); // Maximum compression

                    foreach (string filePath in fileList)
                    {
                        var zipEntry = new ZipEntry(filePath.Remove(0, trimLength));
                        zipStream.PutNextEntry(zipEntry);

                        if (!filePath.EndsWith(@"/")) // Not a directory
                        {
                            using (var fileStream = File.OpenRead(filePath))
                            {
                                byte[] buffer = new byte[fileStream.Length];
                                fileStream.Read(buffer, 0, buffer.Length);
                                zipStream.Write(buffer, 0, buffer.Length);
                            }
                        }
                    }

                    zipStream.Finish();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.WriteToLog("", "", "Error", ex);
                return false;
            }
        }

        public void DeleteFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog("", "", "Error", ex);
            }
        }

        public void CleanupOldFiles(string basePath, int daysToKeep)
        {
            try
            {
                if (!Directory.Exists(basePath))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                
                // Cleanup directories
                var directories = Directory.GetDirectories(basePath);
                foreach (var directory in directories)
                {
                    var dirInfo = new DirectoryInfo(directory);
                    if (dirInfo.LastWriteTime < cutoffDate)
                    {
                        DeleteFolder(directory);
                        _logger.Info("", "", $"Deleted old folder: {directory} (Last modified: {dirInfo.LastWriteTime})");
                    }
                }

                // Cleanup files (like ZIP files)
                var files = Directory.GetFiles(basePath);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.Info("", "", $"Deleted old file: {file} (Last modified: {fileInfo.LastWriteTime})");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("", "", $"Failed to delete old file: {file}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog("", "", "Error", ex);
            }
        }

        private ArrayList GenerateFileList(string directory)
        {
            ArrayList fileList = new ArrayList();
            bool isEmpty = true;

            foreach (string file in Directory.GetFiles(directory))
            {
                fileList.Add(file);
                isEmpty = false;
            }

            if (isEmpty)
            {
                if (Directory.GetDirectories(directory).Length == 0)
                {
                    fileList.Add(directory + @"/");
                }
            }

            foreach (string subdirectory in Directory.GetDirectories(directory))
            {
                foreach (object obj in GenerateFileList(subdirectory))
                {
                    fileList.Add(obj);
                }
            }

            return fileList;
        }
    }
}
