using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Customers;
using AlloyDbCrudApi.Application.Validators.Customers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;

namespace AlloyDbCrudApi.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/customers").WithTags("Customers");

        g.MapGet("/", async ([AsParameters] CustomerListQuery q, ICustomerService svc, CustomerListQueryValidator val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(q, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            return Results.Ok(await svc.ListAsync(q, ct));
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador")).WithName("ListCustomers");

        g.MapGet("/{customerId}", async (string customerId, ICustomerService svc, CancellationToken ct) =>
            await svc.GetAsync(customerId, ct) is { } c ? Results.Ok(c) : Results.NotFound())
            .RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor", "Visualizador"))
            .WithName("GetCustomer");

        g.MapPost("/", async (CreateCustomerRequest req, ICustomerService svc, IValidator<CreateCustomerRequest> val, CancellationToken ct) =>
        {
            var v = await val.ValidateAsync(req, ct);
            if (!v.IsValid) return Results.ValidationProblem(v.ToDictionary());
            try { var dto = await svc.CreateAsync(req, ct); return Results.Created($"/api/customers/{dto.CustomerId}", dto); }
            catch (Domain.Exceptions.DomainException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(pb => pb.RequireRole("Superadmin", "Vendedor")).WithName("CreateCustomer");

        return app;
    }
}
