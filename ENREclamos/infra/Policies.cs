using System;
using Pulumi;
using Pulumi.Aws.Iam;

namespace ENREclamos.Infrastructure;

public class Policies
{
	internal static string ReadPolicyForReclamosTable(string dynamodbTable)
	{
		var policyText = $@"{{{{
			""Version"": ""2012-10-17"",
			""Statement"": [{{{{
				""Effect"": ""Allow"",
				""Action"": [
					""dynamodb:DescribeTable"",
					""dynamodb:Scan"",
					""dynamodb:Query"",
					""dynamodb:UpdateItem"",
					""dynamodb:PutItem"",
					""dynamodb:BatchWriteItem""
				],
				""Resource"": [""{dynamodbTable}"", ""{dynamodbTable}/index/FechaOrdenada""]
			}}}}]
		}}}}";

		var parsedText = string.Format(policyText, dynamodbTable);

		return parsedText;
	}
	

	internal static string ReadAsumePolicyForLambda()
	{
		var policyText = @"{
			""Version"": ""2012-10-17"",
			""Statement"": [
				{
					""Action"": ""sts:AssumeRole"",
					""Principal"": {
						""Service"": ""lambda.amazonaws.com""
					},
					""Effect"": ""Allow"",
					""Sid"": """"
				}
			]
		}";

		return policyText;
	}
	
	internal static string ReadPassRolePolicyForLambda(string roleArn)
	{
		var policyDocument = new
		{
			Version = "2012-10-17",
			Statement = new[]
			{
				new
				{
					Action = new[]
					{
						"iam:PassRole"
					},
					Effect = "Allow",
					Resource = new[]
					{
						roleArn
					} 
				}
			}
		};
		
		var policyText = System.Text.Json.JsonSerializer.Serialize(policyDocument);
		
		return policyText;
	}

	internal static string ReadAsumePolicyForSchedule()
	{
		var policyText = @"{
					""Version"": ""2012-10-17"",
					""Statement"": [
						{
							""Action"": ""sts:AssumeRole"",
							""Principal"": {
								""Service"": ""scheduler.amazonaws.com""
							},
							""Effect"": ""Allow"",
							""Sid"": """"
						}
					]
				}";

		return policyText;
	}

	internal static string ReadPolicyForSchedule(string functionName)
	{
		var current = Pulumi.Aws.GetCallerIdentity.InvokeAsync();

		var region = "us-east-1"; //TODO dynamic
		var accountId = current.Result.AccountId;

		var policyText = @"{{
			""Version"": ""2012-10-17"",
			""Statement"": [
			{{
				""Effect"": ""Allow"",
				""Action"": [
				""lambda:InvokeFunction"",
				""lambda:InvokeFunctionUrl""
				],
				""Resource"": [
				""arn:aws:lambda:{0}:{1}:function:{2}:*"",
				""arn:aws:lambda:{0}:{1}:function:{2}""
				]
			}}
			]
		}}";

		var parsedText = string.Format(policyText, region, accountId, functionName);

		return parsedText;
	}

	internal static string ReadPolicyForCloudwatch()
	{
		var policyText = @"{
			""Version"": ""2012-10-17"",
			""Statement"": [{
				""Effect"": ""Allow"",
				""Action"": [
					""logs:CreateLogGroup"",
					""logs:CreateLogStream"",
					""logs:PutLogEvents""
				],
				""Resource"": ""arn:aws:logs:*:*:*""
			}]
		}";

		return policyText;
	}
	
	/// <summary>
	/// If your schedule name is daily-task, the group is default, and it's in the us-east-1 region with an account ID of 123456789012, the ARN would look like this:
	/// arn:aws:scheduler:us-east-1:123456789012:schedule/default/daily-task
	/// </summary>
	/// <param name="scheduleArn"></param>
	/// <returns></returns>
	internal static string ReadPolicyForSchedulerUpdates(string scheduleArn)
	{
		var policyText = $@"{{
			""Version"": ""2012-10-17"",
			""Statement"": [{{
				""Effect"": ""Allow"",
				""Action"": [
					""scheduler:GetSchedule"",
					""scheduler:UpdateSchedule""
				],
				""Resource"": [""{scheduleArn}"", ""{scheduleArn}/*""]
			}}]
		}}";

		return policyText;
	}

	internal static string ReadPolicyForS3Bucket(string bucketName)
	{
		var policyText = $@"{{
			""Version"": ""2012-10-17"",
			""Statement"": [{{
				""Effect"": ""Allow"",
				""Action"": [
					""s3:PutObject"",
					""s3:GetObject"",
					""s3:DeleteObject""
				],
				""Resource"": ""{bucketName}/*""
			}}]
		}}";

		return policyText;
	}
}