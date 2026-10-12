using Xunit;

namespace Spreedly.ComposerSdk.Tests;

/// <summary>
/// Collection definition to prevent parallel execution of logger output tests
/// (Console.Out and Logger.FilterLevel are shared static state)
/// </summary>
[CollectionDefinition("Logger Output Tests", DisableParallelization = true)]
public class LoggerTestCollection
{
}
