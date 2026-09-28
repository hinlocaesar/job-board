using FluentAssertions;
using JobBoard.Domain.Abstractions;

namespace JobBoard.UnitTests;

public sealed class JobSearchResultTests
{
    private static JobSearchResult Result(int totalCount, int pageSize, int page = 1)
        => new(Array.Empty<JobSearchItem>(), totalCount, page, pageSize);

    [Theory]
    [InlineData(0, 10, 0)]   // no results
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]  // exact page
    [InlineData(11, 10, 2)]  // one over => rounds up
    [InlineData(19, 10, 2)]
    [InlineData(100, 10, 10)]
    [InlineData(5, 1, 5)]    // page-per-item
    public void TotalPages_Rounds_Up(int totalCount, int pageSize, int expected)
        => Result(totalCount, pageSize).TotalPages.Should().Be(expected);

    [Theory]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    public void TotalPages_Is_Zero_When_PageSize_Is_Not_Positive(int totalCount, int pageSize)
        => Result(totalCount, pageSize).TotalPages.Should().Be(0);

    [Fact]
    public void Page_And_PageSize_Are_Preserved()
    {
        var result = Result(totalCount: 42, pageSize: 7, page: 3);

        result.Page.Should().Be(3);
        result.PageSize.Should().Be(7);
        result.TotalCount.Should().Be(42);
        result.TotalPages.Should().Be(6);
    }
}
