using System;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Abstraction over file-system logging so consumers are not coupled to the static
    /// LogManager.  The concrete LogService is registered as a singleton in the DI
    /// container (Phase 1).
    /// </summary>
    public interface ILogService
    {
        /// <summary>Write a general-purpose log entry.</summary>
        void Log(string message);

        /// <summary>Write an informational message.</summary>
        void Info(string message);

        /// <summary>Write an error with its associated exception.</summary>
        void Error(string message, Exception ex);
    }
}
