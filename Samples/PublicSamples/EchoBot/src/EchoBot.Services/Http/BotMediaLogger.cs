using EchoBot.Services.Contract;
using Microsoft.Extensions.Logging;
using MediaLogLevel = Microsoft.Skype.Bots.Media.LogLevel;

namespace EchoBot.Services.Http
{
    /// <summary>
    /// The MediaPlatformLogger.
    /// </summary>
    public class BotMediaLogger: IBotMediaLogger
    {
        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionLogger" /> class.
        /// </summary>
        /// <param name="logger">Graph logger.</param>
        public BotMediaLogger(ILogger<BotMediaLogger> logger)
        {
            _logger = logger;
        }

       public void WriteLog(MediaLogLevel level, string logStatement)
       {
           LogLevel logLevel = level switch
           {
               MediaLogLevel.Error => LogLevel.Error,
               MediaLogLevel.Warning => LogLevel.Warning,
               MediaLogLevel.Information => LogLevel.Information,
               MediaLogLevel.Verbose => LogLevel.Trace,
               _ => LogLevel.Trace
           };

           // TODO fix here
           // if (logLevel is LogLevel.Critical or LogLevel.Error or LogLevel.Warning)
           // {
               // this._logger.Log(logLevel, "------> " + logStatement);
           // }
           this._logger.Log(logLevel, logStatement);
       }
    }
}
