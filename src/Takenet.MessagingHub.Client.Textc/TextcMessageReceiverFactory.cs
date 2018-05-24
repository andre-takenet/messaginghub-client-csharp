﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Lime.Protocol;
using Takenet.MessagingHub.Client.Host;
using Takenet.MessagingHub.Client.Listener;
using Takenet.Textc;
using Takenet.Textc.Csdl;
using Takenet.Textc.PreProcessors;
using Takenet.Textc.Processors;
using Takenet.Textc.Scorers;

namespace Takenet.MessagingHub.Client.Textc
{
    public class TextcMessageReceiverFactory : IFactory<IMessageReceiver>
    {
        public static IDictionary<Type, object> ProcessorInstancesDictionary = new Dictionary<Type, object>();

        public async Task<IMessageReceiver> CreateAsync(IServiceProvider serviceProvider, IDictionary<string, object> settings)
        {
            string outState = null;
            var builder = new TextcMessageReceiverBuilder(serviceProvider.GetService<Sender.IMessagingHubSender>());
            if (settings != null)
            {
                var textcMessageReceiverSettings = TextcMessageReceiverSettings.ParseFromSettings(settings);
                if (textcMessageReceiverSettings.Commands != null)
                {
                    builder = SetupCommands(
                        serviceProvider, settings, textcMessageReceiverSettings.Commands, builder);
                }

                if (textcMessageReceiverSettings.ScorerType != null)
                {
                    builder = await SetupScorerAsync(
                        serviceProvider, settings, textcMessageReceiverSettings.ScorerType, builder).ConfigureAwait(false);
                }

                if (textcMessageReceiverSettings.TextSplitterType != null)
                {
                    builder = await SetupTextSplitterAsync(
                        serviceProvider, settings, textcMessageReceiverSettings.TextSplitterType, builder).ConfigureAwait(false);
                }

                if (textcMessageReceiverSettings.Context != null)
                {
                    builder = await SetupContextProviderAsync(
                        serviceProvider, settings, textcMessageReceiverSettings.Context, builder).ConfigureAwait(false);
                }

                if (textcMessageReceiverSettings.MatchNotFoundReturnText != null)
                {
                    builder = builder.WithMatchNotFoundReturn(textcMessageReceiverSettings.MatchNotFoundReturnText);
                }

                if (textcMessageReceiverSettings.MatchNotFoundReturn != null)
                {
                    builder = builder.WithMatchNotFoundReturn(textcMessageReceiverSettings.MatchNotFoundReturn.ToDocument());
                }

                if (textcMessageReceiverSettings.ExceptionHandlerType != null)
                {
                    builder = await SetupExceptionHandlerAsync(
                        serviceProvider, settings, textcMessageReceiverSettings.ExceptionHandlerType, builder).ConfigureAwait(false);
                }

                if (textcMessageReceiverSettings.PreProcessorTypes != null)
                {
                    builder = await SetupTextPreprocessorsAsync(
                        serviceProvider, settings, textcMessageReceiverSettings, builder).ConfigureAwait(false);
                }

                outState = textcMessageReceiverSettings.OutState;
            }

            var stateManager = serviceProvider.GetService<IStateManager>();
            return new SetStateIfDefinedMessageReceiver(builder.Build(), stateManager, outState);
        }

        private static readonly Regex ReturnVariablesRegex = new Regex("{[a-zA-Z0-9]+}", RegexOptions.Compiled);

        private static TextcMessageReceiverBuilder SetupCommands(IServiceProvider serviceProvider, IDictionary<string, object> settings, TextcMessageReceiverCommandSettings[] commandSettings, TextcMessageReceiverBuilder builder)
        {
            foreach (var commandSetting in commandSettings)
            {
                var syntaxes = commandSetting.Syntaxes.Select(CsdlParser.Parse).ToArray();
                if (syntaxes.Length > 0)
                {
                    var syntaxBuilder = builder.ForSyntaxes(syntaxes);

                    if (!string.IsNullOrEmpty(commandSetting.ReturnText))
                    {
                        var returnVariables = new List<string>();

                        var returnTextVariableMatches = ReturnVariablesRegex.Matches(commandSetting.ReturnText);
                        if (returnTextVariableMatches.Count > 0)
                        {
                            returnVariables.AddRange(returnTextVariableMatches.Cast<Match>().Select(m => m.Value));
                        }

                        builder = syntaxBuilder.Return((IRequestContext context) =>
                        {
                            var returnText = commandSetting.ReturnText;
                            foreach (var returnVariable in returnVariables)
                            {
                                var returnVariableValue = context
                                    .GetVariable(returnVariable.TrimStart('{').TrimEnd('}'))?.ToString() ?? "";
                                returnText = returnText.Replace(returnVariable, returnVariableValue);
                            }

                            return returnText.AsCompletedTask();
                        });
                    }
                    else if (commandSetting.ReturnJson != null)
                    {
                        var mediaType = MediaType.Parse(commandSetting.ReturnJsonMediaType ?? "application/json");
                        var document = new JsonDocument(commandSetting.ReturnJson, mediaType);
                        builder = syntaxBuilder.Return(() => document.AsCompletedTask());
                    }
                    else if (!string.IsNullOrEmpty(commandSetting.ProcessorType) && !string.IsNullOrEmpty(commandSetting.Method))
                    {
                        builder = syntaxBuilder
                            .ProcessWith(o =>
                            {
                                var processorTypeName = commandSetting.ProcessorType;
                                var methodName = commandSetting.Method;
                                var assembly = typeof(TextcMessageReceiverBuilder).Assembly;
                                var path = new FileInfo(assembly.Location).DirectoryName;
                                ReferencesUtil.LoadAssembliesAndReferences(path, assemblyFilter: ReferencesUtil.IgnoreSystemAndMicrosoftAssembliesFilter,
                                    ignoreExceptionLoadingReferencedAssembly: true);
                                var processorType = new TypeResolver().Resolve(processorTypeName);
                                object processor;
                                if (!ProcessorInstancesDictionary.TryGetValue(processorType, out processor))
                                {
                                    processor =
                                        Bootstrapper.CreateAsync<object>(processorType, serviceProvider, settings)
                                            .Result;
                                    ProcessorInstancesDictionary.Add(processorType, processor);
                                }

                                var method = processorType.GetMethod(methodName);
                                if (method == null || method.ReturnType != typeof(Task))
                                {
                                    return new ReflectionCommandProcessor(processor, methodName, true, o, syntaxes);
                                }

                                return new ReflectionCommandProcessor(processor, methodName, true, syntaxes: syntaxes);
                            });
                    }
                }
            }
            return builder;
        }

        private static async Task<TextcMessageReceiverBuilder> SetupScorerAsync(IServiceProvider serviceProvider, IDictionary<string, object> settings,
            string scorerType, TextcMessageReceiverBuilder builder)
        {
            IExpressionScorer scorer;
            if (scorerType.Equals(nameof(MatchCountExpressionScorer)))
            {
                scorer = new MatchCountExpressionScorer();
            }
            else if (scorerType.Equals(nameof(RatioExpressionScorer)))
            {
                scorer = new RatioExpressionScorer();
            }
            else
            {
                scorer = await Bootstrapper.CreateAsync<IExpressionScorer>(scorerType, serviceProvider, settings, new TypeResolver()).ConfigureAwait(false);
            }
            builder = builder.WithExpressionScorer(scorer);
            return builder;
        }

        private static async Task<TextcMessageReceiverBuilder> SetupTextSplitterAsync(IServiceProvider serviceProvider, IDictionary<string, object> settings,
            string textSplitterType, TextcMessageReceiverBuilder builder)
        {            
            var textSplitter = await Bootstrapper.CreateAsync<ITextSplitter>(textSplitterType, serviceProvider, settings, new TypeResolver()).ConfigureAwait(false);            
            builder = builder.WithTextSplitter(textSplitter);
            return builder;
        }

        private static async Task<TextcMessageReceiverBuilder> SetupContextProviderAsync(IServiceProvider serviceProvider, IDictionary<string, object> settings,
            TextcMessageReceiverContextCommandSettings context, TextcMessageReceiverBuilder builder)
        {
            var contextProvider = await Bootstrapper.CreateAsync<IContextProvider>(context.Type, serviceProvider, settings, new TypeResolver()).ConfigureAwait(false);
            builder = builder.WithContextProvider(contextProvider);
            return builder;
        }

        private static async Task<TextcMessageReceiverBuilder> SetupExceptionHandlerAsync(IServiceProvider serviceProvider, IDictionary<string, object> settings,
            string matchNotFoundHandlerType, TextcMessageReceiverBuilder builder)
        {
            var exceptionHandler = await Bootstrapper.CreateAsync<IExceptionHandler>(matchNotFoundHandlerType, serviceProvider, settings, new TypeResolver()).ConfigureAwait(false);
            builder = builder.WithExceptionHandler(exceptionHandler);
            return builder;
        }

        private static async Task<TextcMessageReceiverBuilder> SetupTextPreprocessorsAsync(IServiceProvider serviceProvider, IDictionary<string, object> settings,
            TextcMessageReceiverSettings textcMessageReceiverSettings, TextcMessageReceiverBuilder builder)
        {
            foreach (var textPreprocessorType in textcMessageReceiverSettings.PreProcessorTypes)
            {
                var textPreprocessor = await Bootstrapper.CreateAsync<ITextPreprocessor>(
                    textPreprocessorType, serviceProvider, settings, new TypeResolver()).ConfigureAwait(false);
                builder = builder.AddTextPreprocessor(textPreprocessor);
            }
            return builder;
        }

        private class SetStateIfDefinedMessageReceiver : IMessageReceiver
        {
            private readonly IMessageReceiver _receiver;
            private readonly string _state;
            private readonly IStateManager _stateManager;

            public SetStateIfDefinedMessageReceiver(IMessageReceiver receiver, IStateManager stateManager, string state)
            {
                _receiver = receiver;
                _state = state;
                _stateManager = stateManager;
            }

            public async Task ReceiveAsync(Message envelope, CancellationToken cancellationToken = default(CancellationToken))
            {
                await _receiver.ReceiveAsync(envelope, cancellationToken).ConfigureAwait(false);
                if (_state != null)
                {
                    await _stateManager.SetStateAsync(envelope.From.ToIdentity(), _state);
                }
            }
        }
    }
}
