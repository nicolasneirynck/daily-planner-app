using System.Net;

namespace Planning.Client.Http;

// definieert een resultaat voor een API-aanroep in de Blazor-client
// T maakt het type herbruikbaar: bv ApiCallResult<DayPlanningResponse> 
internal sealed record ApiCallResult<T>(HttpStatusCode StatusCode, T? Value, ApiProblem? Problem)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
}
