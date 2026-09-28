using System.Data;
using JobBoard.Domain.Abstractions;
using JobBoard.Domain.Enums;
using JobBoard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobBoard.Infrastructure.Search;

/// <summary>
/// Full-text search over Postgres (tsvector/GIN + websearch_to_tsquery).
/// Deliberately hand-written SQL: it keeps paging, ranking and filtering in a
/// single round-trip and is swappable via <see cref="IJobSearchService"/>.
/// Every value goes through Npgsql parameters — no string interpolation of user input.
/// </summary>
public sealed class PostgresJobSearchService : IJobSearchService
{
    private readonly AppDbContext _db;

    public PostgresJobSearchService(AppDbContext db) => _db = db;

    public async Task<JobSearchResult> SearchAsync(JobSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 50);

        var query = (criteria.Query ?? string.Empty).Trim();
        var hasQuery = query.Length > 0;

        var where = new List<string> { "j.status = @status" };
        var parameters = new List<NpgsqlParameter>
        {
            new("status", (int)JobStatus.Published),
            new("take", pageSize),
            new("skip", (page - 1) * pageSize),
        };

        if (hasQuery)
        {
            where.Add("j.search_vector @@ websearch_to_tsquery('simple', @q)");
            parameters.Add(new NpgsqlParameter("q", query));
        }

        if (criteria.CategoryId is { } categoryId)
        {
            where.Add("j.category_id = @category_id");
            parameters.Add(new NpgsqlParameter("category_id", categoryId));
        }

        if (criteria.JobType is { } jobType)
        {
            where.Add("j.job_type = @job_type");
            parameters.Add(new NpgsqlParameter("job_type", (int)jobType));
        }

        if (criteria.ExperienceLevel is { } level)
        {
            where.Add("j.experience_level = @experience_level");
            parameters.Add(new NpgsqlParameter("experience_level", (int)level));
        }

        if (criteria.PayType is { } payType)
        {
            where.Add("j.pay_type = @pay_type");
            parameters.Add(new NpgsqlParameter("pay_type", (int)payType));
        }

        if (criteria.MinPay is { } minPay)
        {
            where.Add("j.pay_max >= @min_pay");
            parameters.Add(new NpgsqlParameter("min_pay", minPay));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Currency))
        {
            where.Add("upper(j.currency) = upper(@currency)");
            parameters.Add(new NpgsqlParameter("currency", criteria.Currency.Trim()));
        }

        var orderBy = (criteria.Sort?.Trim().ToLowerInvariant(), hasQuery) switch
        {
            ("relevance", true) => "ts_rank(j.search_vector, websearch_to_tsquery('simple', @q)) DESC, j.published_at DESC",
            ("pay_desc", _) => "j.pay_max DESC, j.published_at DESC",
            _ => "j.published_at DESC, j.created_at DESC",
        };

        var sql = $"""
            SELECT j.id, j.title, j.slug, j.category_id, c.name, c.slug,
                   ep.company_name, ep.slug,
                   j.job_type, j.region, j.pay_type, j.pay_min, j.pay_max, j.currency,
                   j.experience_level, j.hours_per_week, j.published_at,
                   count(*) OVER() AS total_count
            FROM jobs j
            INNER JOIN categories c ON c.id = j.category_id
            INNER JOIN employer_profiles ep ON ep.id = j.employer_profile_id
            WHERE {string.Join(" AND ", where)}
            ORDER BY {orderBy}
            LIMIT @take OFFSET @skip
            """;

        var items = new List<JobSearchItem>();
        int totalCount = 0;

        var connectionString = _db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No database connection string configured.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddRange(parameters.ToArray());

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                totalCount = checked((int)reader.GetInt64(17));
                items.Add(new JobSearchItem(
                    Id: reader.GetGuid(0),
                    Title: reader.GetString(1),
                    Slug: reader.GetString(2),
                    CategoryId: reader.GetGuid(3),
                    CategoryName: reader.GetString(4),
                    CategorySlug: reader.GetString(5),
                    CompanyName: reader.GetString(6),
                    CompanySlug: reader.GetString(7),
                    JobType: (JobType)reader.GetInt32(8),
                    Region: (Region)reader.GetInt32(9),
                    PayType: (PayType)reader.GetInt32(10),
                    PayMin: reader.GetDecimal(11),
                    PayMax: reader.GetDecimal(12),
                    Currency: reader.GetString(13),
                    ExperienceLevel: (ExperienceLevel)reader.GetInt32(14),
                    HoursPerWeek: reader.IsDBNull(15) ? null : reader.GetInt16(15),
                    PublishedAt: reader.GetDateTime(16),
                    Skills: Array.Empty<string>()));
            }
        }

        if (items.Count > 0)
        {
            var ids = items.Select(i => i.Id).ToArray();
            await using var skillCommand = new NpgsqlCommand(
                "SELECT job_id, skill_name FROM job_skills WHERE job_id = ANY(@ids)", connection);
            skillCommand.Parameters.AddWithValue("ids", ids);

            await using var skillReader = await skillCommand.ExecuteReaderAsync(cancellationToken);
            var skills = new Dictionary<Guid, List<string>>();
            while (await skillReader.ReadAsync(cancellationToken))
            {
                var jobId = skillReader.GetGuid(0);
                if (!skills.TryGetValue(jobId, out var list))
                    skills[jobId] = list = new List<string>();
                list.Add(skillReader.GetString(1));
            }

            items = items
                .Select(i => skills.TryGetValue(i.Id, out var list)
                    ? i with { Skills = list }
                    : i)
                .ToList();
        }

        return new JobSearchResult(items, totalCount, page, pageSize);
    }
}
