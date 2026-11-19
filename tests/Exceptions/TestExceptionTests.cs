using ArturRios.Util.Test.Exceptions;

namespace ArturRios.Util.Test.Tests.Exceptions;

[Trait("Category", "Unit")]
public class TestExceptionTests
{
    [Fact]
    public void GivenAMessage_WhenTheExceptionIsCreated_ThenItCarriesTheMessageAndNoInnerException()
    {
        var exception = new TestException("Could not seed the database");

        Assert.Equal("Could not seed the database", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void GivenTheExceptionType_WhenInspected_ThenItDerivesFromExceptionRatherThanAnAssertionFailure()
    {
        // A test that fails on it reports an error from its setup, not a failed assertion.
        Assert.Equal(typeof(Exception), typeof(TestException).BaseType);
    }
}
