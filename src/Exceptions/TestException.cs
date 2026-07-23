namespace ArturRios.Util.Test.Exceptions;

/// <summary>Exception thrown by the test utilities when a test-support operation cannot complete.</summary>
/// <param name="message">A message describing what went wrong.</param>
public class TestException(string message) : Exception(message);
