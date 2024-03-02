using System;
using Pulumi;
using Pulumi.Aws.DynamoDB.Inputs;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Lambda;
using Pulumi.Aws.Lambda.Inputs;
using Pulumi.Aws.S3;
using Aws = Pulumi.Aws;
using Config = ENREclamos.Infrastructure.Config;

namespace ENREclamos.Infrastructure;

class ENREStack : Stack
{
	private const string DEV = "dev";
	private const string PROD = "prod";

	[Output] public Output<string> DynamodDBReclamosTable { get; set; }
	[Output] public Output<string> S3Bucket { get; set; }
	[Output] public Output<string> LambdaReclamo { get; set; }
	[Output] public Output<string> LambdaReclamoHttpFunction { get; set; }
	[Output] public Output<string> Schedule { get; set; }
	
	public ENREStack()
	{

		var STACK_NAME = Deployment.Instance.StackName;

		var reclamosBucket = new Bucket(
			"reclamo-enre-html", new BucketArgs()
			{
				BucketName = $"reclamo-enre-html-{STACK_NAME}"
			}
		);

		S3Bucket = reclamosBucket.Arn;
		
		var reclamosTable = new Aws.DynamoDB.Table(
			"reclamo-enre",
			new Aws.DynamoDB.TableArgs()
			{
				Attributes = new[]
				{
					new TableAttributeArgs
					{
						Name = "Id",
						Type = "S",
					},
					new TableAttributeArgs
					{
						Name = "Fecha",
						Type = "S",
					},
				},
				BillingMode = "PROVISIONED",
				HashKey = "Id",
				ReadCapacity = 1,
				WriteCapacity = 1,
				GlobalSecondaryIndexes =new InputList<TableGlobalSecondaryIndexArgs>()
				{
					new TableGlobalSecondaryIndexArgs
					{
						Name = "FechaIndex",
						HashKey = "Fecha",
						ProjectionType = "ALL",
						ReadCapacity = 1,
						WriteCapacity = 1
					}
				} 
			}
		);

		////// CONFIG //////
		var EnreConfig = new Config();

		Log.Info("Numero cliente y medidor:" + EnreConfig.NumeroCliente + " / " + EnreConfig.NumeroMedidor);

		DynamodDBReclamosTable = reclamosTable.Arn;
		
		var lambdaRole = Output.Tuple(DynamodDBReclamosTable, S3Bucket)
			.Apply(names =>
			{
				var dynamoDbTableName = names.Item1;
				var bucketName = names.Item2;
				return CreateLambdaRolePolicies(dynamoDbTableName, bucketName);
			});

		var envHTMLTable = new FunctionEnvironmentArgs()
		{
			Variables = new InputMap<string>() {
				{"TABLA_RECLAMOS", reclamosTable.Name},
				{"BUCKET_RECLAMOS", reclamosBucket.BucketName},
				{"DISTRIBUIDORA", "EDESUR"},
				{"NRO_CLIENTE", EnreConfig.NumeroCliente},
				{"NRO_MEDIDOR", EnreConfig.NumeroMedidor},
				{"DRY_RUN", EnreConfig.DryRun}
			}
		};

		var lambdaReclamo = new Function("ENREclamos", new FunctionArgs
		{
			Name = "ENREclamos",
			Runtime = "dotnet6",
			Code = new FileArchive("../src/ENREclamos/output.zip"),
			Handler = "ENREclamos::ENREclamos.Functions::Get",
			Role = lambdaRole.Apply(x => x.Arn),
			Environment = envHTMLTable,
			Description = "LAMBDA que envia un reclamo al ENRE",
			Timeout = 20
		});
		LambdaReclamo = lambdaReclamo.Arn;
		
		
		var envHTTPFunction = new FunctionEnvironmentArgs()
		{
			Variables = new InputMap<string>() {
				{"TABLA_RECLAMOS", reclamosTable.Name},
			}
		};
		
		var lambdaHttpFunction = new Function("ENREclamos-HTTPFunction", new FunctionArgs
		{
			Name = "ENREclamos-HTTPFunction",
			Runtime = "dotnet6",
			Code = new FileArchive("../src/ENREclamos.LambdaHTTPFunction/output.zip"),
			Handler = "ENREclamos.LambdaHTTPFunction",
			Role = lambdaRole.Apply(x => x.Arn),
			Environment = envHTTPFunction,
			Description = "HTTP Function LAMBDA para ver la lista de reclamos",
			Timeout = 20
		});

		var lambdaFunctionUrl = new FunctionUrl("testLatest", new()
		{
			FunctionName = lambdaHttpFunction.Name,
			AuthorizationType = "NONE",
		});

		LambdaReclamoHttpFunction = lambdaFunctionUrl.FunctionUrlResult;

		#region Schedule
		var schedulerRole = lambdaReclamo.Name.Apply(x => CreateSchedulerRole(x));

		string startDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

		var scheduleGroup = new Aws.Scheduler.ScheduleGroup("enre");

		var schedule = new Aws.Scheduler.Schedule("enre-reclamo", new()
		{
			GroupName = scheduleGroup.Name,
			FlexibleTimeWindow = new Aws.Scheduler.Inputs.ScheduleFlexibleTimeWindowArgs
			{
				Mode = "FLEXIBLE",
				MaximumWindowInMinutes = 15
			},
			ScheduleExpression = "rate(4 hour)",
			Target = new Aws.Scheduler.Inputs.ScheduleTargetArgs
			{
				Arn = lambdaReclamo.Arn,
				RoleArn = schedulerRole.Apply(x => x.Arn),
			},
			ScheduleExpressionTimezone = "America/Buenos_Aires",
			StartDate = startDate,
			Description = "Scheduled Job para mandar un reclamo al ENRE cada 4 horas",
			State = EnreConfig.ScheduleStatus
		});

		Schedule = schedule.Arn;

		#endregion
	}
	
	private static Role CreateSchedulerRole(string functionName)
	{
		var schedulerRole = new Role("schedulerRole", new RoleArgs
		{
			AssumeRolePolicy = Policies.readAsumePolicyForSchedule()
		});

		var schedulePolicy = new RolePolicy("schedulerRolePolicy", new RolePolicyArgs
		{
			Role = schedulerRole.Id,
			Policy = Policies.readPolicyForSchedule(functionName)
		});
		
		var logPolicy = new RolePolicy("cloudwatchPolicy", new RolePolicyArgs
		{
			Role = schedulerRole.Id,
			Policy = Policies.readPolicyForCloudwatch()
		});

		return schedulerRole;
	}

	private static Role CreateLambdaRolePolicies(string dynamodbTable, string s3Bucket)
	{
		var lambdaRole = new Role("lambdaRole", new RoleArgs
		{
			AssumeRolePolicy = Policies.readAsumePolicyForLambda()
		});

		var logPolicy = new RolePolicy("lambdaLogPolicy", new RolePolicyArgs
		{
			Role = lambdaRole.Id,
			Policy = Policies.readPolicyForCloudwatch()
		});

		var dynamodbPolicyRole = new RolePolicy("dynamoDbPolicy", new RolePolicyArgs
		{
			Role = lambdaRole.Id,
			Policy = Policies.readPolicyForReclamosTable(dynamodbTable)
		});

		var s3BucketPolicy = new RolePolicy("s3Policy", new RolePolicyArgs
		{
			Role = lambdaRole.Id,
			Policy = Policies.readPolicyForS3Bucket(s3Bucket)
		});

		return lambdaRole;
	}

}
