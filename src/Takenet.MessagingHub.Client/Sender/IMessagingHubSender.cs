﻿using System.Threading;
using System.Threading.Tasks;
using Lime.Protocol;

namespace Takenet.MessagingHub.Client.Sender
{
    public interface IMessagingHubSender
    {
        /// <summary>
        /// Send a command through the Messaging Hub
        /// </summary>
        /// <param name="command">Command to be sent</param>
        /// <param name="token">A cancellation token to allow the task to be canceled</param>
        /// <returns>A task representing the sending operation. When completed, it will contain the command response</returns>
        Task<Command> SendCommandAsync(Command command, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Send a message through the Messaging Hub
        /// </summary>
        /// <param name="message">Message to be sent</param>
        /// <param name="token">A cancellation token to allow the task to be canceled</param>
        /// <returns>A task representing the sending operation</returns>
        Task SendMessageAsync(Message message, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Send a notification through the Messaging Hub
        /// </summary>
        /// <param name="notification">Notification to be sent</param>
        /// <param name="token">A cancellation token to allow the task to be canceled</param>
        /// <returns>A task representing the sending operation</returns>
        Task SendNotificationAsync(Notification notification, CancellationToken token = default(CancellationToken));
    }
}
