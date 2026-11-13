using Microsoft.AspNetCore.Components;
using Planning.Shared.Common;
using Planning.Shared.Products;

namespace Planning.Client.Products;

public partial class Index
{
    private IEnumerable<ProductDto.Index>? products;

    [Inject] public required IProductService ProductService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var request = new QueryRequest.SkipTake
        {
            Skip = 0,
            Take = 50,
            OrderBy = "Id",
        };

        var result = await ProductService.GetIndexAsync(request);
        products = result.Value.Products;
    }
}

