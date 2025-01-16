namespace ENREclamos.Infrastructure;

public class Config
{
    public string ScheduleStatus => _config.Require("scheduledtatus");
    
    public string NumeroCliente => _config.Require("numerocliente");
    
    public string NumeroMedidor => _config.Require("numeromedidor");
    
    public string CodigoValidacion => _config.Require("codigovalidacion");
    
    public string DryRun => _config.Require("dryrun");

    private Pulumi.Config _config;

    public Config() => _config = new Pulumi.Config("enre");
}