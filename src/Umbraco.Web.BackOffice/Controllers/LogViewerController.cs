using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog.Core;
using Serilog.Events;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Logging.Viewer;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Constants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Cms.Web.BackOffice.Controllers
{
    /// <summary>
    /// Backoffice controller supporting the dashboard for viewing logs with some simple graphs & filtering
    /// </summary>
    [PluginController(Constants.Web.Mvc.BackOfficeApiArea)]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]

    public class LogViewerController : BackOfficeNotificationsController
    {
        private readonly ILogViewer _logViewer;
        private readonly LoggingLevelSwitch _levelSwitch;

        public LogViewerController(ILogViewer logViewer, LoggingLevelSwitch levelSwitch)
        {
            _logViewer = logViewer ?? throw new ArgumentNullException(nameof(logViewer));
            _levelSwitch = levelSwitch;
        }

        private bool CanViewLogs(LogTimePeriod logTimePeriod)
        {
            //Can the interface deal with Large Files
            if (_logViewer.CanHandleLargeLogs)
                return true;

            //Interface CheckCanOpenLogs
            return _logViewer.CheckCanOpenLogs(logTimePeriod);
        }

        [HttpGet]
        public bool GetCanViewLogs([FromQuery] DateTime? startDate = null,[FromQuery] DateTime? endDate = null)
        {
            var logTimePeriod = GetTimePeriod(startDate, endDate);
            return CanViewLogs(logTimePeriod);
        }

        [HttpGet]
        public ActionResult<int> GetNumberOfErrors([FromQuery] DateTime? startDate = null,[FromQuery] DateTime? endDate = null)
        {
            var logTimePeriod = GetTimePeriod(startDate, endDate);
            //We will need to stop the request if trying to do this on a 1GB file
            if (CanViewLogs(logTimePeriod) == false)
            {
                return ValidationProblem("Unable to view logs, due to size");
            }

            return _logViewer.GetNumberOfErrors(logTimePeriod);
        }

        [HttpGet]
        public ActionResult<LogLevelCounts> GetLogLevelCounts([FromQuery] DateTime? startDate = null,[FromQuery] DateTime? endDate = null)
        {
            var logTimePeriod = GetTimePeriod(startDate, endDate);
            //We will need to stop the request if trying to do this on a 1GB file
            if (CanViewLogs(logTimePeriod) == false)
            {
                return ValidationProblem("Unable to view logs, due to size");
            }

            return _logViewer.GetLogLevelCounts(logTimePeriod);
        }

        [HttpGet]
        public ActionResult<IEnumerable<LogTemplate>> GetMessageTemplates([FromQuery] DateTime? startDate = null,[FromQuery] DateTime? endDate = null)
        {
            var logTimePeriod = GetTimePeriod(startDate, endDate);
            //We will need to stop the request if trying to do this on a 1GB file
            if (CanViewLogs(logTimePeriod) == false)
            {
                return ValidationProblem("Unable to view logs, due to size");
            }

            return new ActionResult<IEnumerable<LogTemplate>>(_logViewer.GetMessageTemplates(logTimePeriod));
        }

        [HttpGet]
        public ActionResult<PagedResult<LogMessage>> GetLogs(string orderDirection = "Descending", int pageNumber = 1, string filterExpression = null, [FromQuery(Name = "logLevels[]")]string[] logLevels = null, [FromQuery]DateTime? startDate = null, [FromQuery]DateTime? endDate = null)
        {
            var logTimePeriod = GetTimePeriod(startDate, endDate);

            //We will need to stop the request if trying to do this on a 1GB file
            if (CanViewLogs(logTimePeriod) == false)
            {
                return ValidationProblem("Unable to view logs, due to size");
            }

            var direction = orderDirection == "Descending" ? Direction.Descending : Direction.Ascending;

            return _logViewer.GetLogs(logTimePeriod, filterExpression: filterExpression, pageNumber: pageNumber, orderDirection: direction, logLevels: logLevels);
        }

        private static LogTimePeriod GetTimePeriod(DateTime? startDate, DateTime? endDate)
        {
            if (startDate == null || endDate == null)
            {
                var now = DateTime.Now;
                if (startDate == null)
                {
                    startDate = now.AddDays(-1);
                }

                if (endDate == null)
                {
                    endDate = now;
                }
            }

            return new LogTimePeriod(startDate.Value, endDate.Value);
        }

        [HttpGet]
        public IEnumerable<SavedLogSearch> GetSavedSearches()
        {
            return _logViewer.GetSavedSearches();
        }

        [HttpPost]
        public IEnumerable<SavedLogSearch> PostSavedSearch(SavedLogSearch item)
        {
            return _logViewer.AddSavedSearch(item.Name, item.Query);
        }

        [HttpPost]
        public IEnumerable<SavedLogSearch> DeleteSavedSearch(SavedLogSearch item)
        {
            return _logViewer.DeleteSavedSearch(item.Name, item.Query);
        }

        [HttpGet]
        public string GetLogLevel()
        {
            return _logViewer.GetLogLevel();
        }

        [HttpPost]
        public ActionResult SetLogLevel(LogEventLevel eventLevel)
        {
            // change the injected logging level
            // Next time app restarts it will read from config again
            // Used as a temp way to change the LogLevel when needed to do investigation work
            _levelSwitch.MinimumLevel = eventLevel;

            return Ok();
        }
    }
}
