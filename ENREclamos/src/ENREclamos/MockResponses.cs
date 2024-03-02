namespace ENREclamos;

public static class MockResponses
{
    public static string GetMock_OK()
    {
        var content = File.ReadAllText("Mocks/reclamo_OK.html");

        return content;
    }

    public static string GetMock_Fail()
    {
        var content = File.ReadAllText("Mocks/reclamo_activo.html");

        return content;
    }
}