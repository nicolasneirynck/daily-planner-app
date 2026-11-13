using Planning.Shared.Products;
using System.Net.Http.Json;
using Planning.Shared.Common;

namespace Planning.Client.Products;

public class ProductService(HttpClient httpClient) : IProductService
{

    public async Task<Result<ProductResponse.Create>> CreateAsync(ProductRequest.Create request, CancellationToken ctx = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/products", request, ctx);
        var result = await response.Content.ReadFromJsonAsync<Result<ProductResponse.Create>>(cancellationToken: ctx);
        return result!;
    }

    public async Task<Result<ProductResponse.Index>> GetIndexAsync(QueryRequest.SkipTake request, CancellationToken ctx = default)
    {
        return (await httpClient.GetFromJsonAsync<Result<ProductResponse.Index>>(
            $"/api/products{request}",
            cancellationToken: ctx))!;
    }
}
