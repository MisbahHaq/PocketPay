using InventoryApp.Application.Common;
using InventoryApp.Application.Dtos;
using InventoryApp.Application.Validators;
using InventoryApp.Domain.Enums;

namespace InventoryApp.Tests.Unit;

/// <summary>
/// Paging, sorting and the paged envelope. These are pure functions of the query object, which is
/// precisely why they were put in their own type rather than inline in the service.
/// </summary>
public class ProductQueryTests
{
    [Theory]
    [InlineData(0, 20, 1, 20)]        // page below 1
    [InlineData(-5, 20, 1, 20)]
    [InlineData(1, 20, 1, 20)]        // defaults untouched
    [InlineData(3, 50, 3, 50)]
    [InlineData(1, 0, 1, 1)]          // pageSize below 1
    [InlineData(1, -10, 1, 1)]
    [InlineData(1, 101, 1, 100)]      // above the ceiling
    [InlineData(1, 100000, 1, 100)]   // the denial-of-service attempt
    public void Normalize_clamps_paging(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var (actualPage, actualPageSize) = new ProductQuery { Page = page, PageSize = pageSize }.Normalize();

        Assert.Equal(expectedPage, actualPage);
        Assert.Equal(expectedPageSize, actualPageSize);
    }

    [Theory]
    [InlineData("name", true)]
    [InlineData("sku", true)]
    [InlineData("price", true)]
    [InlineData("createdAt", true)]
    [InlineData("NAME", true)]     // case-insensitive
    [InlineData("Sku", true)]
    public void SortField_recognises_the_allowed_columns(string value, bool expected) =>
        Assert.Equal(expected, ProductSortFields.TryParse(value, out _));

    [Theory]
    [InlineData("id")]             // not offered: ordering by identity is rarely what a user means
    [InlineData("costPrice")]
    [InlineData("; DROP TABLE Products")]
    [InlineData("")]
    [InlineData(null)]
    public void SortField_rejects_anything_else(string? value) =>
        Assert.False(ProductSortFields.TryParse(value, out _));

    [Theory]
    [InlineData("asc", false)]
    [InlineData("ASC", false)]
    [InlineData("desc", true)]
    [InlineData("DESC", true)]
    public void Direction_parses(string value, bool expectedDescending)
    {
        Assert.True(SortDirections.TryParse(value, out var descending));
        Assert.Equal(expectedDescending, descending);
    }

    [Theory]
    [InlineData("ascending")]
    [InlineData("sideways")]
    [InlineData(null)]
    public void Direction_rejects_anything_else(string? value) =>
        Assert.False(SortDirections.TryParse(value, out _));

    [Fact]
    public void PagedResult_rounds_total_pages_up()
    {
        // 25 rows at 10 per page is 3 pages, not 2 - the difference between a pager that works and
        // one that hides the last page.
        var result = PagedResult<string>.Create([], totalCount: 25, page: 1, pageSize: 10);

        Assert.Equal(3, result.TotalPages);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(100, 10, 10)]
    public void PagedResult_maths(int totalCount, int pageSize, int expectedPages) =>
        Assert.Equal(expectedPages, PagedResult<string>.Create([], totalCount, 1, pageSize).TotalPages);

    [Fact]
    public void PagedResult_echoes_back_the_values_it_was_given()
    {
        // Deliberately no clamping here. PagedResult is the response envelope: it reports what was
        // asked for so the client can tell a clamped page from a full one. Clamping belongs to
        // ProductQuery.Normalize, which runs before the query is ever executed - a second clamp
        // here would hide that the two disagree.
        var result = PagedResult<string>.Create(["a"], totalCount: 1, page: 2, pageSize: 100000);

        Assert.Equal(2, result.Page);
        Assert.Equal(100000, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public void The_clamped_page_size_is_what_reaches_the_envelope()
    {
        // The two halves of the contract together: the service normalises, then reports.
        var query = new ProductQuery { Page = 1, PageSize = 100000 };
        var (page, pageSize) = query.Normalize();

        var result = PagedResult<string>.Create(["a"], totalCount: 1, page, pageSize);

        Assert.Equal(100, result.PageSize);
    }
}