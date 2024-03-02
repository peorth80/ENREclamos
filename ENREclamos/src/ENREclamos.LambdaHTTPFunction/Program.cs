using System.Globalization;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

const string AWS_PROFILE = "yourawsprofile";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

builder.Services.AddAWSService<IAmazonDynamoDB>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", () => "ENREClamos 0.1");

app.MapGet("/list", async () =>
{
    var dynamoClient = GetDynamoClient();
    var tablaRequest = Environment.GetEnvironmentVariable("TABLA_RECLAMOS");

    //Si, ya se que un scan esta mal y yadayada, pero son 10 registros de una tabla chica
    var request = new ScanRequest()
    {
        TableName = tablaRequest,
        Limit = 10,
        IndexName = "FechaIndex"
    };

    var response = await dynamoClient.ScanAsync(request);
    
    var items = response.Items.Select(item =>
        item.ToDictionary(
            kvp => kvp.Key,
            kvp => ShowTimeInArgentinaTime(kvp.Key, kvp.Value.S) ?? kvp.Value.BOOL.ToString()
        )
    ).ToList();

    return Results.Ok(items);
});

app.Run();
return;

string? ShowTimeInArgentinaTime(string key, string value)
{
    if (string.IsNullOrEmpty(value) || key != "Fecha") return value;
    
    var utcDateTime = DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ssZ", 
        CultureInfo.InvariantCulture);

    return utcDateTime.ToString("R");
}

//Para que funcione en local
IAmazonDynamoDB GetDynamoClient()
{
    var lambdaFunctionName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
    //Esta corriendo dentro de lambda, ya conoce las credenciales y permisos
    if (!string.IsNullOrEmpty(lambdaFunctionName)) return new AmazonDynamoDBClient();
    
    var credentials = GetCredentials();

    var dynamoDbClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);
    return dynamoDbClient;
}

static AWSCredentials GetCredentials()
{
    var sharedFile = new SharedCredentialsFile();
    sharedFile.TryGetProfile(AWS_PROFILE, out var profile);
    AWSCredentialsFactory.TryGetAWSCredentials(profile, sharedFile, out var credentials);
    return credentials;
}