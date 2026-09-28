using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Examine;

namespace JobBoard.Cms.Composing;

/// <summary>
/// Idempotent seeder for the marketing/editorial content model of the job marketplace.
/// Runs on <see cref="UmbracoApplicationStartedNotification"/> and creates the document
/// types and sample content only when they do not exist yet (looked up by alias/name),
/// so it is safe to re-run on every boot. New content is always published so the
/// Content Delivery API can serve it, and the DeliveryApiContentIndex is rebuilt afterwards.
/// </summary>
public sealed class ContentSeedNotificationHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    // ---- document type aliases ----
    private const string SeoAlias = "seoFields";       // composition: metaTitle, metaDescription
    private const string HomeAlias = "homepage";       // root page
    private const string BlogListAlias = "blogList";   // /blog
    private const string BlogPostAlias = "blogPost";   // /blog/{slug}
    private const string LandingAlias = "landingPage"; // SEO landing template
    private const string FaqAlias = "faq";             // /faq
    private const string FaqItemAlias = "faqItem";     // element type used by the faqItems block list

    // ---- property groups ----
    private const string GroupContent = "content";
    private const string GroupSeo = "seo";

    private const string FaqBlocksDataTypeName = "FAQ Items (Block List)";

    private static readonly string[] PublishedCultures = ["*"];

    /// <summary>
    /// Key of the user the seed runs as. Umbraco's async service APIs require a
    /// real user key (<see cref="Guid.Empty"/> is rejected); the built-in admin
    /// is resolved on boot.
    /// </summary>
    private Guid _actorKey;

    private readonly ILogger<ContentSeedNotificationHandler> _logger;
    private readonly IRuntimeState _runtimeState;
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IDataTypeService _dataTypeService;
    private readonly PropertyEditorCollection _propertyEditorCollection;
    private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IIndexRebuilder _indexRebuilder;

    public ContentSeedNotificationHandler(
        ILogger<ContentSeedNotificationHandler> logger,
        IRuntimeState runtimeState,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditorCollection,
        IConfigurationEditorJsonSerializer configurationEditorJsonSerializer,
        IJsonSerializer jsonSerializer,
        IShortStringHelper shortStringHelper,
        IIndexRebuilder indexRebuilder)
    {
        _logger = logger;
        _runtimeState = runtimeState;
        _contentTypeService = contentTypeService;
        _contentService = contentService;
        _dataTypeService = dataTypeService;
        _propertyEditorCollection = propertyEditorCollection;
        _configurationEditorJsonSerializer = configurationEditorJsonSerializer;
        _jsonSerializer = jsonSerializer;
        _shortStringHelper = shortStringHelper;
        _indexRebuilder = indexRebuilder;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        // Do not run while the app is still installing or upgrading (e.g. first boot).
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            _logger.LogInformation("Content seed skipped: runtime level is {Level}.", _runtimeState.Level);
            return;
        }

        bool createdAnything;
        try
        {
            _actorKey = ResolveActorKey();
            bool typesCreated = await EnsureContentTypesAsync();
            bool contentCreated = EnsureContent();
            createdAnything = typesCreated || contentCreated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding of document types / sample content failed. Fix the error and restart; the seeder is idempotent.");
            return;
        }

        if (createdAnything)
        {
            _logger.LogInformation("Content seed created new items.");
        }

        // Always schedule a Delivery API index rebuild (tiny index) so the very first
        // published items are guaranteed to be queryable, even if the index was not yet
        // running when they were published during boot.
        try
        {
            _ = _indexRebuilder.RebuildIndexAsync(Constants.UmbracoIndexes.DeliveryApiContentIndexName, TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not schedule a rebuild of {IndexName}.", Constants.UmbracoIndexes.DeliveryApiContentIndexName);
        }
    }

    // ------------------------------------------------------------------
    // Document types
    // ------------------------------------------------------------------

    /// <summary>
    /// Umbraco rejects an empty performing-user key. Document types and content
    /// are seeded as the built-in administrator (<c>Constants.Security.SuperUserId</c>),
    /// which is the only user that exists right after an unattended install.
    /// </summary>
    private static Guid ResolveActorKey() => Constants.Security.SuperUserKey;

    private async Task<bool> EnsureContentTypesAsync()
    {
        bool changed = false;

        IDataType textbox = RequireDataType(Constants.DataTypes.Textbox, "Textbox");
        IDataType textarea = RequireDataType(Constants.DataTypes.Textarea, "Textarea");
        IDataType richtext = RequireDataType(Constants.DataTypes.RichtextEditor, "Rich Text editor");
        IDataType dateTime = RequireDataType(Constants.DataTypes.DateTime, "DateTime");

        // --- seoFields (composition only, never placed in the tree) ---
        IContentType? seo = _contentTypeService.Get(SeoAlias);
        if (seo is null)
        {
            seo = new ContentType(_shortStringHelper, -1) { Alias = SeoAlias, Name = "SEO", AllowedAsRoot = false };
            seo.AddPropertyGroup(GroupSeo, "SEO");
            seo.AddPropertyType(NewPropertyType(textbox, "metaTitle", "Meta title"), GroupSeo);
            seo.AddPropertyType(NewPropertyType(textarea, "metaDescription", "Meta description"), GroupSeo);
            await _contentTypeService.CreateAsync(seo, _actorKey);
            changed = true;
            _logger.LogInformation("Created document type {Alias}.", SeoAlias);
        }

        // --- homepage (root) ---
        IContentType? homeType = _contentTypeService.Get(HomeAlias);
        if (homeType is null)
        {
            homeType = new ContentType(_shortStringHelper, -1) { Alias = HomeAlias, Name = "Home", AllowedAsRoot = true };
            homeType.AddPropertyGroup(GroupContent, "Content");
            homeType.AddPropertyType(NewPropertyType(textbox, "heroHeadline", "Hero headline"), GroupContent);
            homeType.AddPropertyType(NewPropertyType(textarea, "heroSubtext", "Hero subtext"), GroupContent);
            homeType.AddPropertyType(NewPropertyType(textbox, "ctaLabel", "CTA label"), GroupContent);
            homeType.AddPropertyType(NewPropertyType(textbox, "ctaLink", "CTA link"), GroupContent);
            homeType.AddPropertyType(NewPropertyType(richtext, "introBody", "Intro body"), GroupContent);
            await _contentTypeService.CreateAsync(homeType, _actorKey);
            changed = true;
            _logger.LogInformation("Created document type {Alias}.", HomeAlias);
        }

        // --- blogList (/blog) ---
        IContentType? blogListType = _contentTypeService.Get(BlogListAlias);
        if (blogListType is null)
        {
            blogListType = new ContentType(_shortStringHelper, -1) { Alias = BlogListAlias, Name = "Blog list", AllowedAsRoot = false };
            blogListType.AddPropertyGroup(GroupContent, "Content");
            blogListType.AddPropertyType(NewPropertyType(textbox, "pageTitle", "Page title"), GroupContent);
            blogListType.AddPropertyType(NewPropertyType(textarea, "intro", "Intro"), GroupContent);
            await _contentTypeService.CreateAsync(blogListType, _actorKey);
            changed = true;
            _logger.LogInformation("Created document type {Alias}.", BlogListAlias);
        }

        // --- blogPost ---
        IContentType? blogPostType = _contentTypeService.Get(BlogPostAlias);
        if (blogPostType is null)
        {
            blogPostType = new ContentType(_shortStringHelper, -1) { Alias = BlogPostAlias, Name = "Blog post", AllowedAsRoot = false };
            blogPostType.AddPropertyGroup(GroupContent, "Content");
            blogPostType.AddPropertyType(NewPropertyType(textbox, "title", "Title"), GroupContent);
            blogPostType.AddPropertyType(NewPropertyType(textarea, "heroSubtext", "Hero subtext"), GroupContent);
            blogPostType.AddPropertyType(NewPropertyType(richtext, "body", "Body"), GroupContent);
            blogPostType.AddPropertyType(NewPropertyType(textbox, "author", "Author"), GroupContent);
            blogPostType.AddPropertyType(NewPropertyType(dateTime, "publishDate", "Publish date"), GroupContent);
            blogPostType.AddPropertyType(NewPropertyType(textarea, "excerpt", "Excerpt"), GroupContent);
            await _contentTypeService.CreateAsync(blogPostType, _actorKey);
            changed = true;
            _logger.LogInformation("Created document type {Alias}.", BlogPostAlias);
        }

        // --- landingPage ---
        IContentType? landingType = _contentTypeService.Get(LandingAlias);
        if (landingType is null)
        {
            landingType = new ContentType(_shortStringHelper, -1) { Alias = LandingAlias, Name = "Landing page", AllowedAsRoot = false };
            landingType.AddPropertyGroup(GroupContent, "Content");
            landingType.AddPropertyType(NewPropertyType(textbox, "headline", "Headline"), GroupContent);
            landingType.AddPropertyType(NewPropertyType(richtext, "body", "Body"), GroupContent);
            landingType.AddPropertyType(NewPropertyType(textbox, "ctaLabel", "CTA label"), GroupContent);
            landingType.AddPropertyType(NewPropertyType(textbox, "ctaLink", "CTA link"), GroupContent);
            await _contentTypeService.CreateAsync(landingType, _actorKey);
            changed = true;
            _logger.LogInformation("Created document type {Alias}.", LandingAlias);
        }

        // --- faqItem (element type for the faqItems block list) ---
        IContentType? faqItemType = _contentTypeService.Get(FaqItemAlias);
        if (faqItemType is null)
        {
            faqItemType = new ContentType(_shortStringHelper, -1) { Alias = FaqItemAlias, Name = "FAQ item", AllowedAsRoot = false, IsElement = true };
            faqItemType.AddPropertyGroup(GroupContent, "Content");
            faqItemType.AddPropertyType(NewPropertyType(textbox, "question", "Question"), GroupContent);
            faqItemType.AddPropertyType(NewPropertyType(textarea, "answer", "Answer"), GroupContent);
            await _contentTypeService.CreateAsync(faqItemType, _actorKey);
            changed = true;
            _logger.LogInformation("Created document type {Alias}.", FaqItemAlias);
        }

        // --- Block List data type that allows faqItem blocks ---
        (IDataType faqBlocks, bool dataTypeCreated) = await EnsureFaqBlocksDataType(faqItemType.Key);
        changed |= dataTypeCreated;

        // --- faq ---
        IContentType? faqType = _contentTypeService.Get(FaqAlias);
        if (faqType is null)
        {
            faqType = new ContentType(_shortStringHelper, -1) { Alias = FaqAlias, Name = "FAQ", AllowedAsRoot = false };
            faqType.AddPropertyGroup(GroupContent, "Content");
            faqType.AddPropertyType(NewPropertyType(textbox, "heading", "Heading"), GroupContent);
            faqType.AddPropertyType(NewPropertyType(textarea, "intro", "Intro"), GroupContent);
            faqType.AddPropertyType(NewPropertyType(faqBlocks, "faqItems", "FAQ items"), GroupContent);
            await _contentTypeService.CreateAsync(faqType, _actorKey);
            changed = true;
            _logger.LogInformation("Created document type {Alias}.", FaqAlias);
        }

        // --- SEO composition on every page type ---
        await EnsureComposedWith(homeType, seo);
        await EnsureComposedWith(blogListType, seo);
        await EnsureComposedWith(blogPostType, seo);
        await EnsureComposedWith(landingType, seo);
        await EnsureComposedWith(faqType, seo);

        // --- allowed children ---
        await EnsureAllowedChildren(homeType, blogListType, landingType, faqType);
        await EnsureAllowedChildren(blogListType, blogPostType);

        return changed;
    }

    private async Task<(IDataType DataType, bool Created)> EnsureFaqBlocksDataType(Guid faqItemKey)
    {
        IDataType? existing = (await _dataTypeService.GetAllAsync())
            .FirstOrDefault(d => d.EditorAlias == Constants.PropertyEditors.Aliases.BlockList && d.Name == FaqBlocksDataTypeName);
        if (existing is not null)
        {
            return (existing, false);
        }

        IDataEditor? editor = _propertyEditorCollection[Constants.PropertyEditors.Aliases.BlockList];
        if (editor is null)
        {
            throw new InvalidOperationException($"The '{Constants.PropertyEditors.Aliases.BlockList}' property editor is not registered.");
        }

        var dataType = new DataType(editor, _configurationEditorJsonSerializer)
        {
            Name = FaqBlocksDataTypeName,
            DatabaseType = ValueStorageType.Ntext,
        };

        // Restrict the editor to a single block type: the faqItem element type.
        var configuration = new Dictionary<string, object>(dataType.ConfigurationData)
        {
            ["blocks"] = new[] { new { contentElementTypeKey = faqItemKey, settingsElementTypeKey = (Guid?)null } },
        };
        dataType.ConfigurationData = configuration;

        await _dataTypeService.CreateAsync(dataType, _actorKey);
        _logger.LogInformation("Created data type '{Name}' ({Editor}).", FaqBlocksDataTypeName, Constants.PropertyEditors.Aliases.BlockList);
        return (dataType, true);
    }

    private async Task<IContentType> EnsureComposedWith(IContentType type, IContentType composition)
    {
        if (type.CompositionKeys().Contains(composition.Key))
        {
            return type;
        }

        type.AddContentType(composition);
        await _contentTypeService.UpdateAsync(type, _actorKey);
        _logger.LogInformation("Added composition {Composition} to {Type}.", composition.Alias, type.Alias);
        return type;
    }

    private async Task<IContentType> EnsureAllowedChildren(IContentType parent, params IContentType[] children)
    {
        var desired = children.Select(c => c.Key).ToHashSet();
        var current = (parent.AllowedContentTypes ?? Enumerable.Empty<ContentTypeSort>()).Select(c => c.Key).ToHashSet();
        if (desired.SetEquals(current))
        {
            return parent;
        }

        parent.AllowedContentTypes = children
            .Select((child, order) => new ContentTypeSort(child.Key, order, child.Alias))
            .ToList();
        await _contentTypeService.UpdateAsync(parent, _actorKey);
        _logger.LogInformation("Updated allowed children of {Type}: {Children}.", parent.Alias, string.Join(", ", children.Select(c => c.Alias)));
        return parent;
    }

    private IDataType RequireDataType(int id, string displayName)
        // The built-in data types are addressed by their integer id (Constants.DataTypes);
        // the Guid-based replacement is only available for custom types.
#pragma warning disable CS0618
        => _dataTypeService.GetDataType(id)
#pragma warning restore CS0618
           ?? throw new InvalidOperationException($"Default data type '{displayName}' (id {id}) was not found. Is this a fresh, correctly installed Umbraco instance?");

    private PropertyType NewPropertyType(IDataType dataType, string alias, string displayName)
        => new(_shortStringHelper, dataType, alias) { Name = displayName };

    // ------------------------------------------------------------------
    // Content
    // ------------------------------------------------------------------

    private bool EnsureContent()
    {
        bool changed = false;

        IContentType homeType = _contentTypeService.Get(HomeAlias) ?? throw new InvalidOperationException($"{HomeAlias} document type missing; content seeding cannot continue.");
        IContentType blogListType = _contentTypeService.Get(BlogListAlias) ?? throw new InvalidOperationException($"{BlogListAlias} document type missing.");
        IContentType blogPostType = _contentTypeService.Get(BlogPostAlias) ?? throw new InvalidOperationException($"{BlogPostAlias} document type missing.");
        IContentType landingType = _contentTypeService.Get(LandingAlias) ?? throw new InvalidOperationException($"{LandingAlias} document type missing.");
        IContentType faqType = _contentTypeService.Get(FaqAlias) ?? throw new InvalidOperationException($"{FaqAlias} document type missing.");

        // Home (/)
        var (home, homeCreated) = EnsurePage("Home", null, HomeAlias, c =>
        {
            c.SetValue("heroHeadline", "Hire the best Filipino virtual assistants");
            c.SetValue("heroSubtext", "Connect with pre-vetted Filipino VAs for admin, customer support, bookkeeping, social media and more — direct hires, no agency mark-up.");
            c.SetValue("ctaLabel", "Hire Filipino VAs");
            c.SetValue("ctaLink", "/hire-filipino-virtual-assistants");
            c.SetValue("introBody",
                "<p><strong>JobBoard</strong> helps you find, hire and pay remote talent from the Philippines. " +
                "Create a job post, review applications and start working with your new virtual assistant — usually within a week.</p>" +
                "<h2>Why hire in the Philippines?</h2><ul>" +
                "<li>Excellent English proficiency and a strong service culture</li>" +
                "<li>Large talent pool across admin, support, bookkeeping and IT</li>" +
                "<li>Overlapping time zones with the US, Europe and Australia</li></ul>");
            c.SetValue("metaTitle", "JobBoard — Hire Filipino Virtual Assistants & Remote Talent");
            c.SetValue("metaDescription", "Hire pre-vetted Filipino virtual assistants and remote workers. Post a job, compare candidates and start in days, not weeks.");
        });
        changed |= homeCreated;

        // /blog (blogList)
        var (blog, blogCreated) = EnsurePage("Blog", home, BlogListAlias, c =>
        {
            c.SetValue("pageTitle", "The JobBoard Blog");
            c.SetValue("intro", "Practical guides on hiring, managing and paying remote workers in the Philippines.");
            c.SetValue("metaTitle", "Remote hiring guides — JobBoard Blog");
            c.SetValue("metaDescription", "Guides and templates for hiring Filipino virtual assistants, managing remote teams and running a global payroll.");
        });
        changed |= blogCreated;

        // /blog/{slug} (blogPost) x3
        (string Name, string Excerpt, string Author, DateTime PublishDate, string Body)[] posts =
        [
            ("How to Hire Your First Filipino Virtual Assistant",
                "A step-by-step walkthrough: write the job post, screen applicants, run a paid trial and onboard your first VA.",
                "Maria Santos", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                "<h2>Start with the job post</h2><p>List the tasks you want to delegate, the tools you use and the hours you overlap. " +
                "A clear post attracts clearer applicants.</p><h2>Screen for communication first</h2><p>Ask every candidate for a short " +
                "recorded video reply. You are looking for clear English, attention to detail and honest answers about their setup.</p>" +
                "<h2>Run a paid trial</h2><p>Give your top three candidates a small, paid task that mirrors the real work. Hire the one " +
                "who asks the best questions.</p>"),
            ("5 Tasks You Can Delegate to a VA This Week",
                "Inbox triage, calendar management, invoice chasing, social scheduling and research — quick wins that give you hours back.",
                "John Dela Cruz", new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
                "<p>Delegation fails when it is vague. Pick tasks from this list, document them once, and hand them over with a checklist:</p>" +
                "<ul><li><strong>Inbox triage</strong> — flag what needs you, draft replies for the rest</li>" +
                "<li><strong>Calendar management</strong> — book meetings, send reminders</li>" +
                "<li><strong>Invoice chasing</strong> — follow up on unpaid invoices every Monday</li>" +
                "<li><strong>Social scheduling</strong> — queue a week of posts from one draft session</li>" +
                "<li><strong>Research</strong> — competitor pricing, lead lists, market notes</li></ul>"),
            ("Virtual Assistant vs Personal Assistant: Which Do You Need?",
                "Both titles get used interchangeably, but the work is different. Here is how to decide before you post the job.",
                "Maria Santos", new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
                "<p>A <strong>virtual assistant</strong> supports your business: email, customers, reports, scheduling. " +
                "A <strong>personal assistant</strong> supports your life: travel, errands, household logistics.</p>" +
                "<p>Most small-business owners want a business VA first. If your calendar and inbox are the bottleneck, " +
                "start there and expand the scope later.</p>"),
        ];

        changed |= SeedBlogPost(blog, posts);

        // /hire-filipino-virtual-assistants (landingPage)
        var (landing, landingCreated) = EnsurePage("Hire Filipino Virtual Assistants", home, LandingAlias, c =>
        {
            c.SetValue("headline", "Hire Filipino virtual assistants, hand-picked for your workflow");
            c.SetValue("body",
                "<p>Filipino VAs are known for fluent English, reliability and a service-first mindset. " +
                "With JobBoard you reach the whole market directly — no agency in the middle taking 40%.</p>" +
                "<h2>What you can delegate</h2><ul><li>Customer support (email, chat, phone)</li>" +
                "<li>Bookkeeping and invoicing (Xero, QuickBooks, Wave)</li><li>Sales development and lead research</li>" +
                "<li>Social media and content scheduling</li><li>Executive assistance and calendar management</li></ul>" +
                "<h2>How it works</h2><ol><li>Post your job — it takes 5 minutes</li>" +
                "<li>Review applicants and shortlist in a couple of days</li>" +
                "<li>Interview, run a paid trial and hire</li></ol>");
            c.SetValue("ctaLabel", "Post a job — free");
            c.SetValue("ctaLink", "/signup");
            c.SetValue("metaTitle", "Hire Filipino Virtual Assistants — JobBoard");
            c.SetValue("metaDescription", "Hire pre-vetted Filipino virtual assistants for admin, support and back office. Post a job and start interviewing in 48 hours.");
        });
        changed |= landingCreated;

        // /faq (faq) with 6 Q&A blocks
        var (faq, faqCreated) = EnsurePage("FAQ", home, FaqAlias, c =>
        {
            c.SetValue("heading", "Frequently asked questions");
            c.SetValue("intro", "Everything you need to know about hiring through JobBoard.");
            c.SetValue("faqItems", BuildFaqBlocks(faqType));
            c.SetValue("metaTitle", "FAQ — Hiring Filipino VAs | JobBoard");
            c.SetValue("metaDescription", "Answers about hiring costs, timelines, vetting, working hours and payments for Filipino virtual assistants.");
        });
        changed |= faqCreated;

        return changed;
    }

    private bool SeedBlogPost(IContent blog, (string Name, string Excerpt, string Author, DateTime PublishDate, string Body)[] posts)
    {
        bool changed = false;
        foreach (var (name, excerpt, author, publishDate, body) in posts)
        {
            var (_, created) = EnsurePage(name, blog, BlogPostAlias, c =>
            {
                c.SetValue("title", name);
                c.SetValue("excerpt", excerpt);
                c.SetValue("heroSubtext", excerpt);
                c.SetValue("body", body);
                c.SetValue("author", author);
                // The Date Picker property accepts a DateTime; the old v13 JSON
                // wrapper is no longer a supported value format.
                c.SetValue("publishDate", publishDate);
                c.SetValue("metaTitle", name + " — JobBoard Blog");
                c.SetValue("metaDescription", excerpt);
            });
            changed |= created;
        }

        return changed;
    }

    private string BuildFaqBlocks(IContentType faqType)
    {
        IContentType faqItemType = _contentTypeService.Get(FaqItemAlias)
            ?? throw new InvalidOperationException($"{FaqItemAlias} document type missing.");

        (string Question, string Answer)[] items =
        [
            ("How much does it cost to hire a Filipino VA?",
             "Most Filipino VAs charge between $4 and $12 per hour depending on experience and specialization. You pay the worker directly — JobBoard charges no agency mark-up."),
            ("How long does it take to hire someone?",
             "JobBoard employers typically review their first applicants within 24 hours and make a hire within 3–7 days of posting a job."),
            ("Are the candidates pre-vetted?",
             "Every worker can complete identity, skills and English assessments. Look for the verified badges on profiles and always run a short paid trial before committing."),
            ("Can I hire for part-time or full-time work?",
             "Yes. Set the availability on your job post (freelance, part-time or full-time) and candidates will filter themselves accordingly."),
            ("What hours do Filipino VAs work?",
             "Most VAs work Philippine business hours (GMT+8) but are used to shifting hours for US or European teams. State your required overlap hours in the job post."),
            ("How do payments work?",
             "Payments are agreed directly between you and the worker — bank transfer, Wise, PayPal or Payoneer are common. JobBoard does not hold your funds."),
        ];

        var blockValue = new BlockListValue();
        var layout = new List<IBlockLayoutItem>();
        foreach (var (question, answer) in items)
        {
            var key = Guid.NewGuid();
            layout.Add(new BlockListLayoutItem(key));

            var data = new BlockItemData
            {
                Key = key,
                ContentTypeKey = faqItemType.Key,
                ContentTypeAlias = FaqItemAlias,
            };
            data.Values.Add(new BlockPropertyValue { Alias = "question", Value = question });
            data.Values.Add(new BlockPropertyValue { Alias = "answer", Value = answer });
            blockValue.ContentData.Add(data);

            // Blocks must be exposed explicitly, otherwise the value converter treats them as hidden.
            blockValue.Expose.Add(new BlockItemVariation(key, null, null));
        }

        blockValue.Layout[Constants.PropertyEditors.Aliases.BlockList] = layout;
        return _jsonSerializer.Serialize(blockValue);
    }

    // ------------------------------------------------------------------
    // Page helpers
    // ------------------------------------------------------------------

    private (IContent Content, bool Created) EnsurePage(string name, IContent? parent, string typeAlias, Action<IContent>? setValues)
    {
        IContent? existing = FindChild(parent, typeAlias, name);
        if (existing is not null)
        {
            if (existing.Published)
            {
                return (existing, false);
            }

            // Exists but was never published (e.g. a previous run crashed after Save).
            _contentService.Save(existing);
            _contentService.Publish(existing, PublishedCultures);
            _logger.LogInformation("Published existing content '{Name}'.", name);
            return (existing, true);
        }

        IContent content = parent is null
            ? _contentService.Create(name, -1, typeAlias)
            : _contentService.Create(name, parent.Key, typeAlias);

        setValues?.Invoke(content);
        _contentService.Save(content);
        _contentService.Publish(content, PublishedCultures);
        _logger.LogInformation("Created and published {Type} content '{Name}'.", typeAlias, name);
        return (content, true);
    }

    private IContent? FindChild(IContent? parent, string typeAlias, string name)
    {
        IEnumerable<IContent> candidates;
        if (parent is null)
        {
            candidates = _contentService.GetRootContent();
        }
        else
        {
#pragma warning disable CS0618 // the long overload is replaced by an all-parameters variant scheduled for Umbraco 19
            candidates = _contentService.GetPagedChildren(parent.Id, 0, 100, out _);
#pragma warning restore CS0618
        }

        return candidates.FirstOrDefault(c =>
            c.ContentType.Alias == typeAlias &&
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
