using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Data_layer
{
    public class FileSystemWrapper : IFileSystemWrapper
    {
        public async Task AppendAllTextAsync(string path, string content) => await File.AppendAllTextAsync(path, content);

        public string CombinePath(params string[] paths) => Path.Combine(paths);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        public bool FileExists(string path) => File.Exists(path);

        public async Task<string> ReadAllTextAsync(string path) => await File.ReadAllTextAsync(path);

        public async Task WriteAllTextAsync(string path, string content) => await File.WriteAllTextAsync(path, content);

    }
}
