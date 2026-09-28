using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace JobBoard.Cms.Composing;

/// <summary>
/// Registers the content seeder that runs once Umbraco is up and running.
/// See docs/umbraco-content-api.md for the resulting content model and Delivery API contract.
/// </summary>
public sealed class ContentSeedComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ContentSeedNotificationHandler>();
}
