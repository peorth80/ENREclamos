using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ENREclamos;

public class Functions
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonDynamoDB _dynamoDbClient;

    private readonly string? _bucketName = Environment.GetEnvironmentVariable("BUCKET_RECLAMOS");
    private readonly string? _dynamoDbTable = Environment.GetEnvironmentVariable("TABLA_RECLAMOS");
    
    private readonly string? _distribuidora = Environment.GetEnvironmentVariable("DISTRIBUIDORA");
    private readonly string? _nroCliente = Environment.GetEnvironmentVariable("NRO_CLIENTE");
    private readonly string? _nroMedidor = Environment.GetEnvironmentVariable("NRO_MEDIDOR");
    private readonly bool _dryRun = bool.Parse(Environment.GetEnvironmentVariable("DRY_RUN") ?? "false");
    
    private readonly string _fileName = $"reclamo-{DateTime.Now:yy-MM-dd_HH-mm}.html";

    private readonly bool _inLambdaRuntime;
    
    public Functions()
    {
        //Por favor que verga esto de las funciones LAMBDA...
        _inLambdaRuntime = true;
        
        var serviceCollection = new ServiceCollection();
        
        serviceCollection.AddHttpClient();
        serviceCollection.AddAWSService<IAmazonS3>();
        serviceCollection.AddAWSService<IAmazonDynamoDB>();
        
        var serviceProvider = serviceCollection.BuildServiceProvider();

        _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        _s3Client = serviceProvider.GetRequiredService<IAmazonS3>();
        _dynamoDbClient = serviceProvider.GetRequiredService<IAmazonDynamoDB>();
    }
    
    public Functions(IHttpClientFactory httpClientFactory, IAmazonS3 s3Client, IAmazonDynamoDB dynamoDbClient)
    {
        _httpClientFactory = httpClientFactory;
        _s3Client = s3Client;
        _dynamoDbClient = dynamoDbClient;
    }
    
    [LambdaFunction(Policies = "AWSLambdaBasicExecutionRole", MemorySize = 512, Timeout = 30)]
    [RestApi(LambdaHttpMethod.Get, "/")]
    public async Task<IHttpResult> Get(ILambdaContext context)
    {
        if (_dryRun)
        {
            var responseBody = MockResponses.GetMock_OK();
            var number = await Recover_Number(responseBody);

            if (number != "")
            {
                await SaveInDynamoDb(number);
                return HttpResults.Ok(number);
            }
            
            //No nos devolvio un numero de reclamo, devolvemos el error que nos mostro
            var message = await Recover_Error(responseBody);
            return HttpResults.Conflict(message);
        }
        else
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Origin", "https://www.enre.gov.ar");
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

            var data = new
            {
                ErroresCampo = "",
                Medidor = _nroMedidor,
                Empresa = _distribuidora,
                IDCliente = _nroCliente,
                MedidorW = _nroMedidor!.Substring(_nroMedidor.Length-3),
                NRecWeb = "",
                Mas = "no",
                ID = "DFBAB062214A523A03258AC00070D3EE",
                Errores = "",
                Fin = "",
                Procesado = "",
                Fecha = $"{DateTime.Now.ToString("MM/dd/yyyy")}",
                HTTP_COOKIE = "",
                HTTP_REFERER = "",
                HTTP_USER_AGENT =
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                PATH_INFO = "/reclamosweb.nsf/Reclamo",
                REMOTE_ADDR = "198.147.22.204",
                REMOTE_USER = "",
                Explorador = "Netscape"
            };

            //Serializamos el json
            var plainData = JsonSerializer.Serialize(data);

            //Ahora lo convertimos a un diccionario para poder hacer el submit
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(plainData)!;

            //Agregamos el __Click
            dict["__Click"] = "8325752000514A90.c0e15788bd73314603258247006adc9a/$Body/0.70";

            var requestMessage = new HttpRequestMessage(HttpMethod.Post,
                "https://www.enre.gov.ar/reclamosweb.nsf/Reclamo?OpenForm&Seq=1");
            requestMessage.Headers.Referrer = new Uri("https://www.enre.gov.ar/reclamosweb.nsf/Reclamo");
            requestMessage.Content = new FormUrlEncodedContent(dict);

            var response = await client.SendAsync(requestMessage);
            
            var responseBody = "";
            try
            {
                responseBody = await response.Content.ReadAsStringAsync();

                await UploadResponseToS3(responseBody, _fileName);

                await SaveLocalFile(responseBody);
            }
            catch (Exception ex)
            {
                context.Logger.LogCritical($"No se pudo leer el response del POST");
                context.Logger.LogCritical($"Error: {ex.Message}");
            }

            if (response.IsSuccessStatusCode)
            {
                var number = await Recover_Number(responseBody);

                if (number != "")
                {
                    await SaveInDynamoDb(number);
                    return HttpResults.Ok(number);
                }
            
                //No nos devolvio un numero de reclamo, devolvemos el error que nos mostro
                var message = await Recover_Error(responseBody);
                return HttpResults.Conflict(message);
            }

            context.Logger.LogDebug($"IsSuccessStatusCode failed! Returned {response.StatusCode}");
            context.Logger.LogDebug($"Body: {responseBody}");
        } 
        
        //Unable to get the data
        return HttpResults.ServiceUnavailable();
    }

    private async Task SaveLocalFile(string responseBody)
    {
        if (_inLambdaRuntime)
        {
            Console.WriteLine("Running in LAMBDA mode, local file not saved");
            return;
        }
        
        await File.WriteAllTextAsync(_fileName,
            responseBody);
        
        Console.WriteLine($"Wrote response to {_fileName}");
    }
    
    private async Task UploadResponseToS3(string content, string fileName)
    {
        try
        {
            // Convert the string to a byte array
            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(byteArray);

            // Create a PutObjectRequest
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                InputStream = stream,
                ContentType = "text/html"
            };

            Console.WriteLine($"Writing to {_bucketName}/{fileName}...");
            
            // Upload the file
            await _s3Client.PutObjectAsync(putRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
        }
    }

    private static async Task<string> Recover_Number(string responseHtml)
    {
        await Task.CompletedTask;
        if (responseHtml.Length == 0) return "";
        
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(responseHtml);
        
        var xpath = "//div[contains(@class, 'alert') and contains(@class, 'alert-danger')]";
        var targetNode = htmlDoc.DocumentNode.SelectSingleNode(xpath);
        var message = targetNode.InnerText;

        var regex = new Regex(@"el número es: (\S+)");
        var match = regex.Match(message);

        if (!match.Success) return "";
        
        var extractedString = match.Groups[1].Value;
        return extractedString;
    }

    private async Task SaveInDynamoDb(string numeroReclamo)
    {
        try
        {
            var item = new Dictionary<string, AttributeValue>()
            {
                { "Id", new AttributeValue() { S = numeroReclamo } },
                { "Fecha", new AttributeValue() { S = ConvertToArgentinaTime(DateTime.UtcNow.ToString("s")) } },
                { "FechaUTC", new AttributeValue() { S = DateTime.UtcNow.ToString("s") } },
                { "Procesado", new AttributeValue() { N = "1"} },
                { "DryRun", new AttributeValue() { BOOL = _dryRun }}
            };
            
            await _dynamoDbClient.PutItemAsync(_dynamoDbTable, item);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR SAVING TO DYNAMODB: " + ex.Message);
        }
    }
    
    private static async Task<string> Recover_Error(string responseHtml)
    {
        await Task.CompletedTask;
        if (responseHtml.Length == 0) return "";
        
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(responseHtml);
        
        var xpath = "//div[contains(@class, 'alert') and contains(@class, 'alert-danger')]";
        var targetNode = htmlDoc.DocumentNode.SelectSingleNode(xpath);
        var message = targetNode.InnerText
            .Replace("\n", "")
            .Replace("\t", "");

        var regex = new Regex(@"ya tiene un reclamo ingresado");
        var match = regex.Match(message);

        return match.Success ? message : "";
    }
    
    private string ConvertToArgentinaTime(string value)
    {
        var utcDateTime = DateTime.Parse(value, null, DateTimeStyles.RoundtripKind);
        var argentinaTimeZone = TimeZoneInfo.CreateCustomTimeZone("GMT-3", TimeSpan.FromHours(-3), "GMT-3", "GMT-3");
        DateTime argentinaDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, argentinaTimeZone);

        return argentinaDateTime.ToString("f");
    }
}

