using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Scheduler;
using Amazon.Scheduler.Model;
using ENREclamo.LambdaHTTPFunction;

var ENVIRONMENT = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

var configurationRoot = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{ENVIRONMENT}.json", true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.AddDefaultAWSOptions(configurationRoot.GetAWSOptions());
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddLogging();

builder.Logging.AddConsole();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", () => "ENREClamos 0.1.3");

app.MapGet("/start/{code:guid}", async (Guid code, IConfiguration configuration, ILogger<Program> logger) =>
{
    var codigoValidacion = configuration.GetValue<string>("CODIGO_VALIDACION");
    if (codigoValidacion != code.ToString()) return Results.Unauthorized();
    
    var scheduleInfo = ParseSchedule(configuration);
    
    try
    {
        var results = await ChangeStatus(scheduleInfo, true);
        logger.Log(LogLevel.Information, results.Message);
        
        return results.Success ? Results.Ok(results.Message) : Results.Conflict(results.Message);
    }
    catch (Exception ex)
    {
        var message = $"Error actualizando el schedule: {ex.Message}";
        logger.Log(LogLevel.Error, message);
        return Results.Problem(message);
    }
});

app.MapGet("/stop/{code:guid}", async (Guid code, IConfiguration configuration, ILogger<Program> logger) =>
{
    var codigoValidacion = configuration.GetValue<string>("CODIGO_VALIDACION");
    if (codigoValidacion != code.ToString()) return Results.Unauthorized();
    
    var scheduleInfo = ParseSchedule(configuration);
    
    try
    {
        var results = await ChangeStatus(scheduleInfo, false);
        logger.Log(LogLevel.Information, results.Message);
        
        return results.Success ? Results.Ok(results.Message) : Results.Conflict(results.Message);
    }
    catch (Exception ex)
    {
        var message = $"Error actualizando el schedule: {ex.Message}";
        logger.Log(LogLevel.Error, message);
        return Results.Problem(message);
    }
});

app.MapGet("/list", async (IConfiguration configuration) =>
{
    var tablaRequest = configuration.GetValue<string>("TABLA_RECLAMOS");
    var dynamoClient = GetDynamoClient();

    //Si, ya se que un scan esta mal y yadayada, pero son 10 registros de una tabla chica
    var request = new QueryRequest()
    {
        TableName = tablaRequest,
        KeyConditionExpression = "Procesado = :p",
        Limit = 10,
        IndexName = "FechaOrdenada",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            { ":p", new AttributeValue { N = "1"} } //Que hack espantoso
        },
        ScanIndexForward = false,
        ProjectionExpression = "DryRun, Fecha, FechaUTC, Id"
    };

    var response = await dynamoClient.QueryAsync(request);
    
    var items = response.Items
        .Select(item =>
            item.Where(kvp => kvp.Key != "Procesado")
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => ProcessAttributeValue(kvp.Key, kvp.Value)
                )
        )
        .ToList();

    return Results.Ok(items);
});


app.Run();
return;

IAmazonDynamoDB GetDynamoClient()
{
    if (InLambdaMode()) return new AmazonDynamoDBClient();

    var credentials = GetCredentials();
    return new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);
}

AmazonSchedulerClient GetSchedulerClient()
{
    if (InLambdaMode()) return new AmazonSchedulerClient();
    
    var credentials = GetCredentials();
    return new AmazonSchedulerClient(credentials, RegionEndpoint.USEast1);
}


AWSCredentials GetCredentials()
{
    var options = configurationRoot.GetAWSOptions();
    
    var sharedFile = new SharedCredentialsFile();
    sharedFile.TryGetProfile(options.Profile, out var profile);
    AWSCredentialsFactory.TryGetAWSCredentials(profile, sharedFile, out var credentials);
    return credentials;
}

UpdateScheduleRequest CreateUpdateScheduleRequest(GetScheduleResponse res, bool enabled)
{
    var updateRequest  = new UpdateScheduleRequest()
    {
        Name = res.Name,
        GroupName = res.GroupName,
        FlexibleTimeWindow = res.FlexibleTimeWindow,
        ScheduleExpression = res.ScheduleExpression,
        Target = res.Target,
        ScheduleExpressionTimezone = res.ScheduleExpressionTimezone,
        Description = res.Description,
        State = enabled ? ScheduleState.ENABLED : ScheduleState.DISABLED
    };

    //Le ponemos el startDate en 1 minuto mas que ahora para que pueda correr la primera vez "relativamente" rapido
    if (enabled)
        updateRequest.StartDate = DateTime.UtcNow.AddMinutes(1);

    return updateRequest;
}

ScheduleInformation ParseSchedule(IConfiguration configuration)
{
    var scheduleArn = configuration.GetValue<string>("SCHEDULE_ARN");
    
    var scheduleName = scheduleArn.Split('/')[^1]; // Name
    var scheduleGroup = scheduleArn.Split('/')[^2]; //Group

    return new ScheduleInformation()
    {
        Name = scheduleName,
        Group = scheduleGroup,
        ARN = scheduleArn
    };
}

async Task<Status> ChangeStatus(ScheduleInformation scheduleInfo, bool enabled)
{
    
    var results = new Status();
    try
    {
        using var client = GetSchedulerClient();

        var getRequest = new GetScheduleRequest()
        {
            Name = scheduleInfo.Name,
            GroupName = scheduleInfo.Group,
        };
        var schedule = await client.GetScheduleAsync(getRequest);

        if (enabled && schedule.State == ScheduleState.ENABLED)
        {
            results.Message = "El schedule ya estaba habilitado";
            results.Success = false;
            return results;
        }
        
        if (!enabled && schedule.State == ScheduleState.DISABLED)
        {
            results.Message = "El schedule ya estaba deshabilitado";
            results.Success = false;
            return results;
        }

        var updateRequest = CreateUpdateScheduleRequest(schedule, enabled); 
        await client.UpdateScheduleAsync(updateRequest);
        
        var status = "habilitado";
        if (!enabled) status = "deshabilitado";
        
        results.Success = true;
        results.Message = $"El schedule '{scheduleInfo.Name}' fue {status}";
        
        if (enabled) results.Message += $", y va a correr a las {updateRequest.StartDate:t}";
    }
    catch (Exception ex)
    {
        results.Message = ex.Message;
        results.Success = false;
    }
    
    return results;
}

bool InLambdaMode()
{   
    var lambdaFunctionName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME");
    if (!string.IsNullOrEmpty(lambdaFunctionName)) return true;

    return false;
}

// Helper method to process the AttributeValue
string ProcessAttributeValue(string key, AttributeValue value)
{
    if (value.S != null) 
        return value.S;  
    
    if (value.IsBOOLSet)
        return value.BOOL.ToString(); 
    
    if (value.N != null)
    {
        if (int.TryParse(value.N, out var intNum))
            return intNum.ToString(); 
    }

    return value.ToString();
}