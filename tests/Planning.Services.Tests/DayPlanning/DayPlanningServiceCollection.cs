using Planning.Services.Tests.Fixtures;

namespace Planning.Services.Tests.DayPlanning;

[CollectionDefinition("DayPlanningService")]
public sealed class DayPlanningServiceCollection
    : ICollectionFixture<PostgreSqlFixture>
{
}
