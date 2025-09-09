using FocusAssistant.Enums;
using FocusAssistant.Models;
using FocusAssistant.Services.Data_log_and_Save_Repo.Data_Persistence_Interfaces;
using FocusAssistant.Services.Data_log_and_Save_Repo.Interfaces;
using FocusAssistant.Services.Export_Services;
using FocusAssistant.Services.Export_Services.Interfaces;
using FocusAssistant.Services.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocusAssistant.Services.Data_log_and_Save_Repo.Service_Layer
{
    public class ActivityManagementService : IActivityManagementService
    {
        private readonly IActivityLogger _activityLogger;
        private readonly ISessionRepository _sessionRepository;
        private readonly IActivityRepository _activityRepository;
        private readonly IExportService _exportService;
        private readonly ILoggingService _loggingService;

        public ActivityManagementService(
            IActivityLogger activityLogger,
            ISessionRepository sessionRepository,
            IActivityRepository activityRepository,
            IExportService exportService,
            ILoggingService loggingService)
        {
            _activityLogger = activityLogger ?? throw new ArgumentNullException(nameof(activityLogger));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _activityRepository = activityRepository ?? throw new ArgumentNullException(nameof(activityRepository));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public async Task ExportDataAsync(ExportType exportType, ExportFormat format, string filePath, int days = 30)
        {
            try
            {
                await _exportService.ExportAsync(new ExportRequest
                {
                    ExportType = exportType,
                    Format = format,
                    FilePath = filePath,
                    Days = days
                });

                _loggingService.LogInformation($"Data exported: {exportType} as {format} to {filePath}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to export data: {exportType} as {format}", ex);
                throw;
            }
        }

        public async Task<IEnumerable<WorkSession>> GetRecentSessionsAsync(int days = 7)
        {
            try
            {
                var sessions = await _sessionRepository.GetSessionsAsync(days);
                _loggingService.LogInformation($"Retrieved {sessions.Count()} sessions from last {days} days");
                return sessions;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to retrieve recent sessions", ex);
                throw;
            }
        }

        public async Task<IEnumerable<WorkSession>> GetSessionsByDateAsync(DateTime date)
        {
            try
            {
                var sessions = await _sessionRepository.GetSessionsByDateAsync(date);
                _loggingService.LogInformation($"Retrieved {sessions.Count()} sessions for {date:yyyy-MM-dd}");
                return sessions;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to retrieve sessions for date: {date:yyyy-MM-dd}", ex);
                throw;
            }
        }

        public async Task LogActivitiesAsync(IEnumerable<AppUsage> activities)
        {
            if (activities == null || !activities.Any())
            {
                _loggingService.LogWarning("Attempted to log null or empty activities collection");
                return;
            }

            try
            {
                await _activityLogger.LogActivitiesAsync(activities);
                await _activityRepository.SaveActivitiesAsync(activities);
                _loggingService.LogInformation($"Successfully logged {activities.Count()} activities");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to log batch activities", ex);
                throw;
            }
        }

        public async Task LogActivityAsync(AppUsage activity)
        {
            if (activity == null)
            {
                _loggingService.LogWarning("Attempted to log null activity");
                return;
            }

            try
            {
                await _activityLogger.LogActivityAsync(activity);
                await _activityRepository.SaveActivityAsync(activity);
                _loggingService.LogDebug($"Activity logged successfully: {activity.AppName}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to log activity: {activity?.AppName}", ex);
                throw;
            }
        }

        public async Task LogIdleStateAsync(IdleStateChangedEventArgs idleEvent)
        {
            if (idleEvent == null)
            {
                _loggingService.LogWarning("Attempted to log null idle event");
                return;
            }

            try
            {
                await _activityLogger.LogIdleStateAsync(idleEvent);
                _loggingService.LogDebug($"Idle state logged: {(idleEvent.IsIdle ? "IDLE" : "ACTIVE")}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to log idle state", ex);
                throw;
            }
        }

        public async Task SaveSessionAsync(WorkSession session)
        {
            if (session == null)
            {
                _loggingService.LogWarning("Attempted to save null session");
                return;
            }

            try
            {
                await _sessionRepository.SaveSessionAsync(session);
                _loggingService.LogInformation($"Work session saved successfully: {session.wID}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to save work session: {session?.wID}", ex);
                throw;
            }
        }

        public async Task SaveSessionFromActivitiesAsync(IEnumerable<AppUsage> activities)
        {
            if (activities == null || !activities.Any())
            {
                _loggingService.LogWarning("Cannot create session from null or empty activities");
                return;
            }

            try
            {
                var session = CreateWorkSessionFromActivities(activities);
                await Task.Run(async () =>
                {
                    await _sessionRepository.SaveSessionAsync(session);
                    await _activityLogger.LogActivitiesAsync(activities);
                    await _activityRepository.SaveActivitiesAsync(activities);
                });
                _loggingService.LogInformation($"Session saved with {activities.Count()} activities");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to save session from activities", ex);
                throw;
            }
        }
        private WorkSession CreateWorkSessionFromActivities(IEnumerable<AppUsage> activities)
        {
            var activitiesList = activities.ToList();
            var startTime = activitiesList.Min(a => a.StartTime);
            var endTime = activitiesList.Max(a => a.EndTime);

            return new WorkSession
            {
                wID = Guid.NewGuid().ToString(),
                StartTime = startTime,
                EndTime = endTime,
                Duration = endTime - startTime,
                ProductiveTime = TimeSpan.FromMinutes(activitiesList.Where(a => a.IsProductive).Sum(a => a.Duration.TotalMinutes)),
                DistractedTime = TimeSpan.FromMinutes(activitiesList.Where(a => !a.IsProductive).Sum(a => a.Duration.TotalMinutes)),
                AppSwitches = activitiesList.Count,
                TopApps = activitiesList.GroupBy(a => a.AppName)
                                      .OrderByDescending(g => g.Sum(a => a.Duration.TotalMinutes))
                                      .Take(5)
                                      .Select(g => g.Key)
                                      .ToList(),
                AppUsages = activitiesList
            };
        }
    }
}
