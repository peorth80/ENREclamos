using System.Net;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.TestUtilities;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using HtmlAgilityPack;
using Moq;
using Moq.Protected;
using Xunit;

namespace ENREclamos.Tests;

public class FunctionTest
{
    private const string AWS_PROFILE = "yourawsprofile";
    private Mock<HttpMessageHandler> OK_Handler = new();
    private Mock<HttpMessageHandler> Fail_Handler = new();

    private IAmazonS3 _realS3Client;
    private IAmazonDynamoDB _realDynamodb;
    private Mock<IAmazonS3> _mockClient = new();
    
    public FunctionTest()
    {
        _realS3Client = GetClient();
        _realDynamodb = GetDynamoClient();
        
        OK_Handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(File.ReadAllText("Responses/reclamo_OK.html"))
            })
            .Verifiable();
        
        Fail_Handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(File.ReadAllText("Responses/reclamo_activo.html"))
            })
            .Verifiable();

        SetEnvVariables();
    }

    private static void SetEnvVariables()
    {
        Environment.SetEnvironmentVariable("BUCKET_RECLAMOS", "reclamos-html-dev");
        Environment.SetEnvironmentVariable("TABLA_RECLAMOS", "Reclamo-Enre-Html-adb4538");
        Environment.SetEnvironmentVariable("DRY_RUN", "true");
        Environment.SetEnvironmentVariable("AWS_PROFILE", AWS_PROFILE);
        

        Environment.SetEnvironmentVariable("DISTRIBUIDORA", "EDESUR");
        Environment.SetEnvironmentVariable("NRO_CLIENTE", "00666666");
        Environment.SetEnvironmentVariable("NRO_MEDIDOR", "999999");
    }

    private IAmazonS3 GetClient()
    {
        var credentials = GetCredentials();

        var s3Client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
        return s3Client;
    }
    
    private IAmazonDynamoDB GetDynamoClient()
    {
        var credentials = GetCredentials();

        var dynamoDbClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.USEast1);
        return dynamoDbClient;
    }

    private static AWSCredentials GetCredentials()
    {
        var sharedFile = new SharedCredentialsFile();
        sharedFile.TryGetProfile(AWS_PROFILE, out var profile);
        AWSCredentialsFactory.TryGetAWSCredentials(profile, sharedFile, out var credentials);
        return credentials;
    }

    [Fact]
    public async Task Test_OK()
    {
        var httpClient = new HttpClient(OK_Handler.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var context = new TestLambdaContext();
        var functions = new Functions(httpClientFactoryMock.Object, 
            _realS3Client, 
            _realDynamodb);

        var response = await functions.Get(context);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var serializationOptions = new HttpResultSerializationOptions { Format = HttpResultSerializationOptions.ProtocolFormat.RestApi };
        var apiGatewayResponse = new StreamReader(response.Serialize(serializationOptions)).ReadToEnd();
        
        Assert.Contains("W666666", apiGatewayResponse);
    }
    
    [Fact]
    public async Task Test_Fail()
    {
        var httpClient = new HttpClient(Fail_Handler.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var context = new TestLambdaContext();
        var functions = new Functions(httpClientFactoryMock.Object, 
            _realS3Client, 
            _realDynamodb);

        var response = await functions.Get(context);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }    
    
    [Fact]
    public async Task IntegrationTest()
    {
        /*
         This will perform a real call -- use at your own risk
        var httpClientFactory = new RealClientFactory();
        var context = new TestLambdaContext();
        var functions = new Functions(httpClientFactory, 
            _realS3Client, 
            _realDynamodb);

        var response = await functions.Get(context);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var serializationOptions = new HttpResultSerializationOptions { Format = HttpResultSerializationOptions.ProtocolFormat.RestApi };
        var apiGatewayResponse = new StreamReader(response.Serialize(serializationOptions)).ReadToEnd();
        Assert.True(apiGatewayResponse.Length > 0);*/
    }

    [Fact]
    public async Task Mostrar_Nro_Reclamo_De_HTML()
    {
        await Task.CompletedTask;
        const string file = "Responses/reclamo_OK.html";

        var htmlResponse = await File.ReadAllTextAsync(file);
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlResponse);
        
        var xpath = "//div[contains(@class, 'alert') and contains(@class, 'alert-danger')]";
        var targetNode = htmlDoc.DocumentNode.SelectSingleNode(xpath);
        var message = targetNode.InnerText;

        var regex = new Regex(@"el número es: (\S+)");
        var match = regex.Match(message);

        if (match.Success)
        {
            var extractedString = match.Groups[1].Value;
            Assert.True(extractedString.Length > 0);
        } else 
            Assert.Fail("REGEX can't find the number");
    }
}
