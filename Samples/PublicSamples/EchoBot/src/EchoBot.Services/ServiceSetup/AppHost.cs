// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 02-28-2022
// ***********************************************************************
// <copyright file="AppHost.cs" company="Microsoft">
//     Copyright ©  2020
// </copyright>
// <summary></summary>
// ***********************************************************************
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Owin.Hosting;
using EchoBot.Services.Contract;
using EchoBot.Services.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.ApplicationInsights.Channel;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Communications.Common.Telemetry.Obfuscation;

namespace EchoBot.Services.ServiceSetup
{
    /// <summary>
    /// Class AppHost.
    /// </summary>
    public class AppHost
    {
        /// <summary>
        /// Gets or sets the service provider.
        /// </summary>
        /// <value>The service provider.</value>
        private IServiceProvider ServiceProvider { get; set; }
        /// <summary>
        /// Gets or sets the service collection.
        /// </summary>
        /// <value>The service collection.</value>
        private IServiceCollection ServiceCollection { get; set; }
        /// <summary>
        /// Gets the application host instance.
        /// </summary>
        /// <value>The application host instance.</value>
        public static AppHost AppHostInstance { get; private set; }

        /// <summary>
        /// The call HTTP server
        /// </summary>
        private IDisposable _callHttpServer;

        /// <summary>
        /// The settings
        /// </summary>
        private IAzureSettings _settings;
        /// <summary>
        /// The bot service
        /// </summary>
        private IBotService _botService;
        /// <summary>
        /// The logger
        /// </summary>
        private IGraphLogger _graphLogger;
        /// <summary>
        /// The logger
        /// </summary>
        private ILogger<AppHost> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppHost" /> class.

        /// </summary>
        public AppHost()
        {
            AppHostInstance = this;
        }

        /// <summary>
        /// Boots this instance.
        /// </summary>
        public void Boot(ConfigurationBuilder builder, ITelemetryChannel channel)
        {
            var configuration = builder.Build();

            ServiceCollection = new ServiceCollection();
            ServiceCollection.AddCoreServices(configuration, channel);
            ServiceProvider = ServiceCollection.BuildServiceProvider();

            _logger = ServiceProvider.GetRequiredService<ILogger<AppHost>>();
            _logger.LogInformation("EchoBot: booting");
            try
            {
                _settings = Resolve<IAzureSettings>();
                _settings.Initialize();
                _botService = Resolve<IBotService>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception in Boot()");
            }
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        public void StartServer()
        {
            try
            {
                _graphLogger = Resolve<IGraphLogger>();
                _botService.Initialize();
                var callStartOptions = new StartOptions();

                foreach (var url in ((AzureSettings)_settings).CallControlListeningUrls)
                {
                    callStartOptions.Urls.Add(url);
                    _logger.LogInformation("Listening on: {url}", url);
                }
                _callHttpServer = WebApp.Start(
                    callStartOptions,
                    (appBuilder) =>
                    {
                        var startup = new HttpConfigurationInitializer();

                        // TODO graph logger to console?\
                        startup.ConfigureSettings(appBuilder, new CustomLogger(this._logger), _logger);
                    });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception in StartServer()");
            }
            _logger.LogInformation("EchoBot: running");
        }

        public void StopServer()
        {
            _botService.Dispose();
            _callHttpServer.Dispose();
        }

        /// <summary>
        /// Resolves this instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>T.</returns>
        public T Resolve<T>()
        {
            return ServiceProvider.GetService<T>();
        }

        private class CustomLogger : IGraphLogger
        {
            private readonly ILogger<AppHost> logger;

            public CustomLogger(ILogger<AppHost> logger)
            {
                this.logger = logger;
            }

            public IDisposable Subscribe(IObserver<LogEvent> observer)
            {
                throw new NotImplementedException();
            }

            public LogEvent Log(TraceLevel level, string message, string component = null, Guid correlationId = new Guid(), Guid requestId = new Guid(),
                LogEventType eventType = LogEventType.Trace,
                IEnumerable<object> properties = null, string memberName = null, string filePath = null, int lineNumber = 0)
            {
                this.logger.Log(level.ToLogLevel(), message);
                return new LogEvent();
            }

            public TraceLevel DiagnosticLevel { get; set; }
            public ObfuscationConfiguration ObfuscationConfiguration { get; }
            public Guid CorrelationId { get; set; }
            public string RequestId { get; set; }
            public uint LogicalThreadId { get; set; }
            public IReadOnlyDictionary<Type, object> Properties { get; }
        }
    }
}
