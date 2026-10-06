using Inventory.Application.Common;

namespace Inventory.UnitTests.Application;

public sealed class PagedQueryTests
{
    [Fact]
    public void Defaults_to_a_sensible_first_page()
    {
        var query = new PagedQuery().Normalize();

        query.Page.Should().Be(1);
        query.PageSize.Should().Be(PagedQuery.DefaultPageSize);
        query.SortDirection.Should().Be(SortDirection.Ascending);
        query.Skip.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Clamps_a_page_below_one(int page)
    {
        // A negative Skip would make the provider throw rather than return nothing.
        var query = new PagedQuery { Page = page }.Normalize();

        query.Page.Should().Be(1);
        query.Skip.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Falls_back_to_the_default_page_size_for_a_nonsense_size(int pageSize)
    {
        new PagedQuery { PageSize = pageSize }.Normalize().PageSize.Should().Be(PagedQuery.DefaultPageSize);
    }

    [Fact]
    public void Caps_the_page_size_so_one_request_cannot_demand_the_whole_table()
    {
        var query = new PagedQuery { PageSize = 100_000 }.Normalize();

        query.PageSize.Should().Be(PagedQuery.MaxPageSize);
    }

    [Fact]
    public void Computes_skip_from_the_page_number()
    {
        var query = new PagedQuery { Page = 4, PageSize = 25 }.Normalize();

        query.Skip.Should().Be(75);
    }

    [Fact]
    public void Falls_back_to_ascending_for_an_undefined_direction()
    {
        var query = new PagedQuery { SortDirection = (SortDirection)99 }.Normalize();

        query.SortDirection.Should().Be(SortDirection.Ascending);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("bolt", true)]
    public void Has_search_only_detects_a_term_with_content(string? search, bool expected)
    {
        new PagedQuery { Search = search }.HasSearch.Should().Be(expected);
    }
}

public sealed class PagedResultTests
{
    private static IReadOnlyList<int> Rows(int count) => Enumerable.Range(1, count).ToList();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 1)]
    [InlineData(26, 2)]
    [InlineData(100, 4)]
    public void Total_pages_rounds_up(int totalCount, int expectedPages)
    {
        var result = new PagedResult<int>([], totalCount, page: 1, pageSize: 25);

        result.TotalPages.Should().Be(expectedPages);
    }

    [Fact]
    public void An_empty_result_reports_no_pages_and_no_navigation()
    {
        var result = new PagedResult<int>([], 0, 1, 25);

        result.TotalPages.Should().Be(0);
        result.HasNext.Should().BeFalse();
        result.HasPrevious.Should().BeFalse();
    }

    [Fact]
    public void The_first_page_of_many_has_no_previous()
    {
        var result = new PagedResult<int>(Rows(25), 100, 1, 25);

        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeTrue();
    }

    [Fact]
    public void The_last_page_of_many_has_no_next()
    {
        var result = new PagedResult<int>(Rows(25), 100, 4, 25);

        result.HasNext.Should().BeFalse();
        result.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void From_slices_the_requested_window()
    {
        var result = PagedResult<int>.From(Rows(100), new PagedQuery { Page = 3, PageSize = 10 });

        result.Items.Should().Equal(Enumerable.Range(21, 10));
        result.TotalCount.Should().Be(100);
        result.Page.Should().Be(3);
    }

    [Fact]
    public void From_normalises_a_hostile_page_before_slicing()
    {
        var result = PagedResult<int>.From(Rows(100), new PagedQuery { Page = -3, PageSize = 5 });

        // Without normalisation the negative Skip would either throw or silently
        // return the wrong window.
        result.Items.Should().Equal(Enumerable.Range(1, 5));
        result.Page.Should().Be(1);
    }

    [Fact]
    public void A_page_past_the_end_returns_nothing_rather_than_throwing()
    {
        var result = PagedResult<int>.From(Rows(10), new PagedQuery { Page = 99, PageSize = 10 });

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(10);
    }
}