using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Bi;
using AlloyDbCrudApi.Application.Validators.Bi;

namespace AlloyDbCrudApi.Endpoints;

public static class BiEndpoints
{
    private static readonly HashSet<string> SupportedBreakdowns =
    [
        "category",
        "store",
        "customer-city",
        "supplier",
        "season",
        "discount",
        "return-category",
        "return-store",
        "return-supplier",
        "return-size",
    ];

    public static IEndpointRouteBuilder MapBiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/bi").WithTags("BI");

        g.MapGet("/dashboard", async (
            [AsParameters] BiDashboardQuery query,
            IBiService service,
            BiDashboardQueryValidator validator,
            CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(query, ct);
            if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());
            return Results.Ok(await service.GetDashboardAsync(query, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"));

        g.MapGet("/products/abc", async (
            [AsParameters] BiProductAbcQuery query,
            IBiService service,
            BiProductAbcQueryValidator validator,
            CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(query, ct);
            if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());
            return Results.Ok(await service.GetProductAbcAsync(query, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"));

        g.MapGet("/customers/rfm", async (
            [AsParameters] BiCustomerRfmQuery query,
            IBiService service,
            BiCustomerRfmQueryValidator validator,
            CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(query, ct);
            if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());
            return Results.Ok(await service.GetCustomerRfmAsync(query, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"));

        g.MapGet("/breakdowns/{dimension}", async (
            string dimension,
            [AsParameters] BiBreakdownQuery query,
            IBiService service,
            BiBreakdownQueryValidator validator,
            CancellationToken ct) =>
        {
            if (!SupportedBreakdowns.Contains(dimension))
                return Results.BadRequest(new { error = $"Unsupported breakdown dimension '{dimension}'." });

            var result = await validator.ValidateAsync(query, ct);
            if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());
            return Results.Ok(await service.GetBreakdownAsync(dimension, query, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"));

        return app;
    }
}
