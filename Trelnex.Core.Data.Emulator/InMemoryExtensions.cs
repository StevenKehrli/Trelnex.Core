using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Trelnex.Core.Data;

/// <summary>
/// Extension method to add the necessary command providers to the <see cref="IServiceCollection"/>.
/// </summary>
public static class InMemoryExtensions
{
    /// <summary>
    /// Add the necessary command providers as a <see cref="ICommandProvider{TInterface}"/> to the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">Represents a set of key/value application configuration properties.</param>
    /// <param name="bootstrapLogger">The <see cref="ILogger"/> to write the CommandProvider bootstrap logs.</param>
    /// <param name="configureCommandProviders">The action to configure the command providers.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddInMemoryCommandProviders(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger bootstrapLogger,
        Action<ICommandProviderOptions> configureCommandProviders)
    {
        var inMemoryConfiguration = configuration.GetSection("InMemory").Get<InMemoryConfiguration>();

        // get the container, create the command provider, and inject
        var commandProviderOptions = new CommandProviderOptions(
            services: services,
            bootstrapLogger: bootstrapLogger,
            persistPath: inMemoryConfiguration?.PersistPath);

        // inject any needed command providers
        configureCommandProviders(commandProviderOptions);

        return services;
    }

    /// <summary>
    /// Represents the configuration properties for in memory command providers.
    /// </summary>
    private record InMemoryConfiguration(
        string PersistPath);

    private class CommandProviderOptions(
        IServiceCollection services,
        ILogger bootstrapLogger,
        string? persistPath)
        : ICommandProviderOptions
    {
        public ICommandProviderOptions Add<TInterface, TItem>(
            string typeName,
            AbstractValidator<TItem>? itemValidator = null,
            CommandOperations? commandOperations = null)
            where TInterface : class, IBaseItem
            where TItem : BaseItem, TInterface, new()
        {
            // create the command provider and inject it
            var commandProvider = (persistPath is not null)
                ? InMemoryCommandProvider.Create<TInterface, TItem>(
                    persistPath,
                    typeName,
                    itemValidator,
                    commandOperations)
                : InMemoryCommandProvider.Create<TInterface, TItem>(
                    typeName,
                    itemValidator,
                    commandOperations);

            object[] args =
            [
                typeof(TInterface), // TInterface,
                typeof(TItem), // TItem,
                persistPath ?? null!, // persistPath
            ];

            // log - the :l format parameter (l = literal) to avoid the quotes
            bootstrapLogger.LogInformation(
                message: "Added CommandProvider<{TInterface:l}, {TItem:l}> with persistPath '{persistPath:l}'.",
                args: args);

            services.AddSingleton(commandProvider);

            return this;
        }
    }
}
