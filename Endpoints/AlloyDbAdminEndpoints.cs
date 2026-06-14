using Google.Api.Gax.Grpc;
using Google.Cloud.AlloyDb.V1;

namespace AlloyDbCrudApi.Endpoints;

public static class AlloyDbAdminEndpoints
{
    public static IEndpointRouteBuilder MapAlloyDbAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var enabled = app.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetValue<bool>("AlloyDbAdmin:Enabled");

        if (!enabled)
        {
            return app;
        }

        var group = app.MapGroup("/api/admin/alloydb")
            .WithTags("AlloyDB Admin");

        group.MapGet("/clusters", async (
            IConfiguration config,
            CancellationToken cancellationToken) =>
        {
            var projectId = config["AlloyDbAdmin:ProjectId"];
            var location = config["AlloyDbAdmin:Location"] ?? "us-central1";

            if (string.IsNullOrWhiteSpace(projectId))
            {
                return Results.BadRequest(new { error = "AlloyDbAdmin:ProjectId is not configured." });
            }

            try
            {
                var client = await AlloyDBAdminClient.CreateAsync(cancellationToken);
                var parent = $"projects/{projectId}/locations/{location}";
                var callSettings = CallSettings.FromCancellationToken(cancellationToken);

                var clusters = new List<object>();
                var response = client.ListClustersAsync(new ListClustersRequest { Parent = parent }, callSettings);
                await foreach (var cluster in response.ConfigureAwait(false))
                {
                    clusters.Add(new
                    {
                        cluster.Name,
                        cluster.DisplayName,
                        State = cluster.State.ToString(),
                        DatabaseVersion = cluster.DatabaseVersion.ToString(),
                    });
                }

                return Results.Ok(new { projectId, location, clusters });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "AlloyDB Admin API call failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
            .WithName("ListAlloyDbClusters");

        return app;
    }
}
