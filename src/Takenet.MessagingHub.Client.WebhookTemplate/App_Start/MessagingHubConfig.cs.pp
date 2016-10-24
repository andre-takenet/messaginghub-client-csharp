﻿using Lime.Protocol.Serialization.Newtonsoft;
using System;
using System.Configuration;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Takenet.MessagingHub.Client.Host;

namespace $rootnamespace$
{
    public static class MessagingHubConfig
    {
        public static async Task<IServiceContainer> StartAsync()
        {
            var applicationFileName = Bootstrapper.DefaultApplicationFileName;
            var application = Application.ParseFromJsonFile(Path.Combine(GetAssemblyRoot(), applicationFileName));
            ApplyConfigurationOverrides(application);

            var localServiceProvider = Bootstrapper.BuildServiceProvider(application);

            localServiceProvider.RegisterService(typeof(IServiceProvider), localServiceProvider);
            localServiceProvider.RegisterService(typeof(IServiceContainer), localServiceProvider);
            localServiceProvider.RegisterService(typeof(Application), application);
            Bootstrapper.RegisterSettingsContainer(application, localServiceProvider);

            var envelopeBuffer = new EnvelopeBuffer();
            localServiceProvider.RegisterService(typeof(IEnvelopeBuffer), envelopeBuffer);
            var client = await Bootstrapper.BuildMessagingHubClientAsync(application, () => new MessagingHubClient(new HttpMessagingHubConnection(envelopeBuffer, new JsonNetSerializer(), application)), localServiceProvider);

            await client.StartAsync().ConfigureAwait(false);

            await Bootstrapper.BuildStartupAsync(application, localServiceProvider);

            return localServiceProvider;
        }

        private static void ApplyConfigurationOverrides(Application application)
        {
            application.Identifier = GetOverride(() => application.Identifier);
            application.AccessKey = GetOverride(() => application.AccessKey);
        }

        private static string GetOverride(Expression<Func<string>> propertyLambda)
        {
            var memberExpression = propertyLambda.Body as MemberExpression;

            if (memberExpression == null) return propertyLambda.Compile().Invoke();
            return ConfigurationManager.AppSettings[$"MessagingHub.{memberExpression.Member.Name}"] ?? propertyLambda.Compile().Invoke();
        }

        private static string GetAssemblyRoot()
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }
    }
}