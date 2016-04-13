﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Lime.Protocol;
using Takenet.MessagingHub.Client.Connection;
using Takenet.MessagingHub.Client.Sender;

namespace Takenet.MessagingHub.Client.Listener
{
    /// <summary>
    /// Listen to envelopes sent to the account you are connected as
    /// </summary>
    public interface IMessagingHubListener : IStartable, IStoppable
    {
        /// <summary>
        /// The connection used to listen through
        /// </summary>
        IMessagingHubConnection Connection { get; }

        /// <summary>
        /// Indicates whether or not the listener is active
        /// </summary>
        bool Listening { get; }

        /// <summary>
        /// A sender used to send responses to the envelopes received
        /// </summary>
        IMessagingHubSender Sender { get; }

        /// <summary>
        /// Add a message receiver for messages that satisfy the given filter criteria
        /// </summary>
        /// <param name="messageReceiver">The message receiver that will be invoked when a message that satisfy the given criteria is received</param>
        /// <param name="messageFilter">The criteria to filter the messages</param>
        void AddMessageReceiver(IMessageReceiver messageReceiver, Predicate<Message> messageFilter);

        /// <summary>
        /// Add a notification receiver for messages that satisfy the given filter criteria
        /// </summary>
        /// <param name="notificationReceiver">The notification receiver that will be invoked when a notification that satisfy the given criteria is received</param>
        /// <param name="notificationFilter">The criteria to filter the notifications</param>
        void AddNotificationReceiver(INotificationReceiver notificationReceiver, Predicate<Notification> notificationFilter);
    }
}