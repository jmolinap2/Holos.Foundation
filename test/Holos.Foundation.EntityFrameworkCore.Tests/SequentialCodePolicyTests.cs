using Holos.Foundation.Application;

namespace Holos.Foundation.EntityFrameworkCore.Tests;

public class SequentialCodePolicyTests
{
    [Fact]
    public void Starts_the_series_when_there_are_no_codes() =>
        Assert.Equal("CAJ-001", SequentialCodePolicy.SuggestNext([], "CAJ-"));

    [Fact]
    public void Continues_after_the_highest_and_keeps_the_zero_padding() =>
        Assert.Equal("CAJ-0008", SequentialCodePolicy.SuggestNext(["CAJ-0002", "CAJ-0007", "OTRO"], "CAJ-"));

    [Fact]
    public void Ignores_codes_of_other_formats_and_blank_values() =>
        Assert.Equal("CAJ-001", SequentialCodePolicy.SuggestNext(["X1", "", null, "CAJ-A"], "CAJ-"));

    [Fact]
    public void Requires_a_prefix() =>
        Assert.Throws<ArgumentException>(() => SequentialCodePolicy.SuggestNext([], ""));
}
