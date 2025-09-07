using FocusAssistant.Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Repository_Implementations
{
    public class FileBasedActivityRepository : IActivityRepository
    {
        private readonly IFileSystemWrapper _fileSystem;
        private readonly ILoggingService _loggingService;
        private readonly string _dataDirectory;

        public FileBasedActivityRepository(
            IFileSystemWrapper fileSystem,
            ILoggingService loggingService)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));

            _dataDirectory = _fileSystem.CombinePath(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAssistant", "Data");
            _fileSystem.CreateDirectory(_dataDirectory);
        }
        public void DeleteOldActivities(DateTime cutoffDate)
        {
            try
            {
                for (int i = 0; i < 90; i++)
                {
                    var checkDate = cutoffDate.AddDays(-i);
                    string dateKey = checkDate.ToString("yyyy-MM-dd");
                    string filePath = _fileSystem.CombinePath(_dataDirectory, $"activities_{dateKey}.json");

                    if (_fileSystem.FileExists(filePath) && checkDate < cutoffDate)
                    {
                        File.Delete(filePath);
                        _loggingService.LogInformation($"Deleted old activity file: {dateKey}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to delete old activities", ex);
                throw;
            }
        }

        public async Task<IEnumerable<AppUsage>> GetActivitiesAsync(DateTime from, DateTime to)
        {
            var allActivities = new List<AppUsage>();

            try
            {
                for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
                {
                    var dailyActivities = await GetActivitiesByDateAsync(date);
                    allActivities.AddRange(dailyActivities.Where(a => a.StartTime >= from && a.StartTime <= to));
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to get activities from {from} to {to}", ex);
                throw;
            }

            return allActivities;
        }

        public async Task<IEnumerable<AppUsage>> GetActivitiesByDateAsync(DateTime date)
        {
            try
            {
                string dateKey = date.ToString("yyyy-MM-dd");
                string filePath = _fileSystem.CombinePath(_dataDirectory, $"activities_{dateKey}.json");

                if (!_fileSystem.FileExists(filePath))
                    return Enumerable.Empty<AppUsage>();

                string json = await _fileSystem.ReadAllTextAsync(filePath);
                return JsonConvert.DeserializeObject<List<AppUsage>>(json) ?? Enumerable.Empty<AppUsage>();
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to get activities for date: {date:yyyy-MM-dd}", ex);
                return Enumerable.Empty<AppUsage>();
            }
        }

        public async Task SaveActivitiesAsync(IEnumerable<AppUsage> activities)
        {
            if (activities == null || !activities.Any()) return;

            try
            {
                var groupedByDate = activities.GroupBy(a => a.StartTime.Date);

                foreach (var group in groupedByDate)
                {
                    string dateKey = group.Key.ToString("yyyy-MM-dd");
                    string filePath = _fileSystem.CombinePath(_dataDirectory, $"activities_{dateKey}.json");

                    List<AppUsage> existingActivities = new List<AppUsage>();

                    if (_fileSystem.FileExists(filePath))
                    {
                        string existingJson = await _fileSystem.ReadAllTextAsync(filePath);
                        existingActivities = JsonConvert.DeserializeObject<List<AppUsage>>(existingJson) ?? new List<AppUsage>();
                    }

                    existingActivities.AddRange(group);
                    string json = JsonConvert.SerializeObject(existingActivities, Formatting.Indented);
                    await _fileSystem.WriteAllTextAsync(filePath, json);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to save batch activities", ex);
                throw;
            }
        }

        public async Task SaveActivityAsync(AppUsage activity)
        {
            if (activity == null) return;

            try
            {
                string dateKey = activity.StartTime.ToString("yyyy-MM-dd");
                string filePath = _fileSystem.CombinePath(_dataDirectory, $"activities_{dateKey}.json");

                List<AppUsage> existingActivities = new List<AppUsage>();

                if (_fileSystem.FileExists(filePath))
                {
                    string existingJson = await _fileSystem.ReadAllTextAsync(filePath);
                    existingActivities = JsonConvert.DeserializeObject<List<AppUsage>>(existingJson) ?? new List<AppUsage>();
                }

                existingActivities.Add(activity);
                string json = JsonConvert.SerializeObject(existingActivities, Formatting.Indented);
                await _fileSystem.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to save activity", ex);
                throw;
            }
        }
    }
}
