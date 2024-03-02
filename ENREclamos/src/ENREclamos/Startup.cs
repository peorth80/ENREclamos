using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace ENREclamos;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        Console.WriteLine("Registering HTTPClientFactory...");
        services.AddHttpClient();
        
        services.AddAWSService<Amazon.S3.IAmazonS3>();
        services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>();
    }
}