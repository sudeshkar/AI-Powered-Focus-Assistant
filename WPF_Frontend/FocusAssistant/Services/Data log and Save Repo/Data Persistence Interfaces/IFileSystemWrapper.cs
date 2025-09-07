using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces
{
    public interface IFileSystemWrapper
    {
        bool FileExists(string path);
        Task<string> ReadAllTextAsync(string path);
        Task WriteAllTextAsync(string path, string content);
        Task AppendAllTextAsync(string path, string content);
        void CreateDirectory(string path);
        string CombinePath(params string[] paths);
        void EnsureDirectoryExists(string filePath);
    }
}
