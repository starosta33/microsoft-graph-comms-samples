// ***********************************************************************
// Assembly         : EchoBot.Services
// Author           : JasonTheDeveloper
// Created          : 09-07-2020
//
// Last Modified By : bcage29
// Last Modified On : 02-28-2022
// ***********************************************************************
// <copyright file="PlatformCallController.cs" company="Microsoft">
//     Copyright ©  2020
// </copyright>
// <summary></summary>
// ***********************************************************************>

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

using EchoBot.Model.Constants;
using EchoBot.Services.Contract;
using EchoBot.Services.Extensions;
using EchoBot.Services.ServiceSetup;

using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Client;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EchoBot.Services.Http.Controllers
{
    /// <summary>
    /// Entry point for handling call-related web hook requests from Skype Platform.
    /// </summary>
    [RoutePrefix(HttpRouteConstants.CallSignalingRoutePrefix)]
    public class PlatformCallController : ApiController
    {
        private readonly ILogger<PlatformCallController> logger;

        /// <summary>
        /// The bot service
        /// </summary>
        private readonly IBotService _botService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatformCallController" /> class.

        /// </summary>
        public PlatformCallController()
        {
            _botService = AppHost.AppHostInstance.Resolve<IBotService>();
            logger = AppHost.AppHostInstance.Resolve<ILogger<PlatformCallController>>();
        }

        /// <summary>
        /// Handle a callback for an incoming call.
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessage" />.</returns>
        [HttpPost]
        [Route(HttpRouteConstants.OnIncomingRequestRoute)]
        public async Task<HttpResponseMessage> OnIncomingRequestAsync()
        {
            var log = $"Received HTTP {this.Request.Method}, {this.Request.RequestUri}";
            this.logger.LogInformation(log);

            var response = await _botService.Client.ProcessNotificationAsync(this.Request).ConfigureAwait(false);

            return await ControllerExtensions.GetActionResultAsync(this.Request, response).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle a callback for an existing call
        /// </summary>
        /// <returns>The <see cref="HttpResponseMessage" />.</returns>
        [HttpPost]
        [Route(HttpRouteConstants.OnNotificationRequestRoute)]
        public async Task<HttpResponseMessage> OnNotificationRequestAsync()
        {
            var log = $"Received HTTP {this.Request.Method}, {this.Request.RequestUri}";
            this.logger.LogInformation(log);

            // Pass the incoming notification to the sdk. The sdk takes care of what to do with it.
            var payload = await this.Request.Content.ReadAsStringAsync();
            var notification = JsonConvert.DeserializeObject<CommsNotifications>(payload);

            var resourceData = notification.Value.First().AdditionalData["resourceData"];
            if (resourceData is JObject jObject)
            {
                this.logger.LogInformation($"=====> Received notification: {jObject["state"]}");
            }
            // else if (resourceData is JArray jArray)
            // {
            //     this.logger.LogInformation($"=====> Received notification: {jArray.Select(x => x["state"])}");
            // }

            var response = await _botService.Client.ProcessNotificationAsync(this.Request).ConfigureAwait(false);

            return await ControllerExtensions.GetActionResultAsync(this.Request, response).ConfigureAwait(false);
        }
    }
}
